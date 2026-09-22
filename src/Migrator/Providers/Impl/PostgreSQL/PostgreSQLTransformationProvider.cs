#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Models;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Data;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Data.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Interfaces;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL;

/// <summary>
/// Migration transformations provider for PostgreSql (using NPGSql .Net driver)
/// </summary>
public class PostgreSQLTransformationProvider : TransformationProvider, IPostgreSQLTransformationProvider
{
    private IPostgreSQLSystemDataLoader _postgreSQLSystemDataLoader;

    public PostgreSQLTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        Initialize();

        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "Npgsql";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, "Npgsql", "Npgsql.NpgsqlFactory");
        _connection = fac.CreateConnection(); //new NpgsqlConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }

    public PostgreSQLTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope)
    {
        Initialize();
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        using var command = MetadataCommand(table);
        using var reader = ExecuteQuery(command, "SELECT conname FROM pg_constraint WHERE contype = 'p' AND conrelid = to_regclass(@relation)");
        return reader.Read() ? reader.GetString(0) : null;
    }

    public override string AddIndex(string table, Index index)
    {
        ValidateIndex(tableName: table, index: index);

        var hasIncludedColumns = index.IncludeColumns != null && index.IncludeColumns.Length > 0;
        var name = QuoteConstraintNameIfRequired(index.Name);
        table = QuoteTableNameIfRequired(table);
        var columns = QuoteColumnNamesIfRequired(index.KeyColumns);

        var uniqueString = index.Unique ? "UNIQUE" : null;
        var columnsString = $"({string.Join(", ", columns)})";
        var filterString = string.Empty;
        var includeString = string.Empty;

        if (index.IncludeColumns != null && index.IncludeColumns.Length > 0)
        {
            var includeColumnsQuoted = index.IncludeColumns.Select(x => QuoteColumnNameIfRequired(x)).ToList();

            includeString = $"INCLUDE ({string.Join(", ", includeColumnsQuoted)})";
        }

        if (index.FilterItems != null && index.FilterItems.Count > 0)
        {
            List<string> singleFilterStrings = [];

            foreach (var filterItem in index.FilterItems)
            {
                var comparisonString = _dialect.GetComparisonStringByFilterType(filterItem.Filter);

                var filterColumnQuoted = QuoteColumnNameIfRequired(filterItem.ColumnName);
                string value = null;

                value = filterItem.Value switch
                {
                    bool booleanValue => booleanValue ? "TRUE" : "FALSE",
                    string stringValue => $"'{stringValue.Replace("'", "''")}'",
                    byte or short or int or long => Convert.ToInt64(filterItem.Value).ToString(),
                    sbyte or ushort or uint or ulong => Convert.ToUInt64(filterItem.Value).ToString(),
                    _ => throw new NotImplementedException($"Given type in '{nameof(FilterItem)}' is not implemented. Please file an issue."),
                };

                var singleFilterString = $"{filterColumnQuoted} {comparisonString} {value}";

                singleFilterStrings.Add(singleFilterString);
            }

            filterString = $"WHERE {string.Join(" AND ", singleFilterStrings)}";
        }

        List<string> list = [];
        list.Add("CREATE");
        list.Add(uniqueString);
        list.Add("INDEX");
        list.Add(name);
        list.Add("ON");
        list.Add(table);
        list.Add(columnsString);
        list.Add(filterString);
        list.Add(includeString);

        var sql = string.Join(" ", list.Where(x => !string.IsNullOrWhiteSpace(x)));

        ExecuteNonQuery(sql);

        return sql;
    }

    public override Index[] GetIndexes(string table)
    {
        var columns = GetColumns(table);

        // Since the migrator does not support schemas at this point in time we set the schema to "public"
        var schemaName = "public";

        var indexes = new List<Index>();

        var sql = @$"
            SELECT
                nsp.nspname AS schema_name,
                tbl.relname AS table_name,
                cls.relname AS index_name,
                idx.indisunique AS is_unique,
                idx.indisclustered AS is_clustered,
                con.contype = 'u' AS is_unique_constraint,
                con.contype = 'p' AS is_primary_constraint,
                pg_get_indexdef(idx.indexrelid) AS index_definition,
                (
                    SELECT string_agg(att.attname, ', ')
                    FROM unnest(idx.indkey) WITH ORDINALITY AS cols(attnum, ord)
                    JOIN pg_attribute att
                    ON att.attrelid = idx.indrelid
                    AND att.attnum = cols.attnum
                    WHERE cols.ord <= idx.indnkeyatts
                ) AS index_columns,
                (
                    SELECT string_agg(att.attname, ', ')
                    FROM unnest(idx.indkey) WITH ORDINALITY AS cols(attnum, ord)
                    JOIN pg_attribute att
                    ON att.attrelid = idx.indrelid
                    AND att.attnum = cols.attnum
                    WHERE cols.ord > idx.indnkeyatts
                ) AS include_columns,
                pg_get_expr(idx.indpred, idx.indrelid) AS partial_filter
            FROM pg_index idx
            JOIN pg_class cls ON cls.oid = idx.indexrelid
            JOIN pg_class tbl ON tbl.oid = idx.indrelid
            JOIN pg_namespace nsp ON nsp.oid = tbl.relnamespace
            LEFT JOIN pg_constraint con ON con.conindid = idx.indexrelid
            WHERE 
                lower(tbl.relname) = '{table.ToLowerInvariant()}' AND
                nsp.nspname = '{schemaName}'";

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
        {
            var includeColumnsOrdinal = reader.GetOrdinal("include_columns");
            var indexColumnsOrdinal = reader.GetOrdinal("index_columns");
            var indexDefinitionOrdinal = reader.GetOrdinal("index_definition");
            var indexNameOrdinal = reader.GetOrdinal("index_name");
            var isClusteredOrdinal = reader.GetOrdinal("is_clustered");
            var isPrimaryConstraintOrdinal = reader.GetOrdinal("is_primary_constraint");
            var isUniqueConstraintOrdinal = reader.GetOrdinal("is_unique_constraint");
            var isUniqueOrdinal = reader.GetOrdinal("is_unique");
            var partialFilterOrdinal = reader.GetOrdinal("partial_filter");
            var schemaNameOrdinal = reader.GetOrdinal("schema_name");
            var tableNameOrdinal = reader.GetOrdinal("table_name");

            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    var includeColumns = !reader.IsDBNull(includeColumnsOrdinal) ? reader.GetString(includeColumnsOrdinal) : null;
                    var indexColumns = !reader.IsDBNull(indexColumnsOrdinal) ? reader.GetString(indexColumnsOrdinal) : null;
                    var indexDefinition = reader.GetString(indexDefinitionOrdinal);
                    var partialColumns = !reader.IsDBNull(partialFilterOrdinal) ? reader.GetString(partialFilterOrdinal) : null;
                    List<FilterItem> filterItems = [];

                    if (!string.IsNullOrWhiteSpace(partialColumns))
                    {
                        partialColumns = partialColumns.Substring(1, partialColumns.Length - 2);
                        var comparisonStrings = _dialect.GetComparisonStrings();
                        var partialSplitted = Regex.Split(partialColumns, " AND ").Select(x => x.Trim()).ToList();

                        if (partialSplitted.Count > 1)
                        {
                            partialSplitted = partialSplitted.Select(x => x.Substring(1, x.Length - 2)).ToList();
                        }

                        foreach (var partialItemString in partialSplitted)
                        {
                            string[] splits = [];
                            var filterType = FilterType.None;

                            foreach (var comparisonString in comparisonStrings.OrderByDescending(x => x))
                            {
                                splits = Regex.Split(partialItemString, $" {comparisonString} ");

                                if (splits.Length == 2)
                                {
                                    filterType = _dialect.GetFilterTypeByComparisonString(comparisonString);
                                    break;
                                }
                            }

                            if (splits.Length != 2)
                            {
                                throw new NotImplementedException($"Comparison string not found in '{partialItemString}'");
                            }

                            var columnNameString = splits[0];
                            var columnNameRegex = new Regex(@"(?<=^\().+(?=\)::(text|boolean|integer)$)");

                            if (columnNameRegex.Match(columnNameString) is Match matchColumnName && matchColumnName.Success)
                            {
                                columnNameString = matchColumnName.Value;
                            }

                            var column = columns.First(x => columnNameString.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
                            var valueAsString = splits[1];
                            var stringValueNumericRegex = new Regex(@"(?<=^\()[^\)]+(?=\)::numeric$)");

                            if (stringValueNumericRegex.Match(valueAsString) is Match valueNumericMatch && valueNumericMatch.Success)
                            {
                                valueAsString = valueNumericMatch.Value;
                            }

                            var stringValueRegex = new Regex("(?<=^').+(?='::(text|boolean|integer|bigint)$)");

                            if (stringValueRegex.Match(valueAsString) is Match match && match.Success)
                            {
                                valueAsString = match.Value;
                            }

                            var filterItem = new FilterItem
                            {
                                ColumnName = column.Name,
                                Filter = filterType,
                                Value = column.MigratorDbType switch
                                {
                                    MigratorDbType.Int16 => short.Parse(valueAsString),
                                    MigratorDbType.Int32 => int.Parse(valueAsString),
                                    MigratorDbType.Int64 => long.Parse(valueAsString),
                                    MigratorDbType.UInt16 => ushort.Parse(valueAsString),
                                    MigratorDbType.UInt32 => uint.Parse(valueAsString),
                                    MigratorDbType.UInt64 => ulong.Parse(valueAsString),
                                    MigratorDbType.Decimal => decimal.Parse(valueAsString),
                                    MigratorDbType.Boolean => valueAsString == "1" || valueAsString.Equals("true", StringComparison.OrdinalIgnoreCase),
                                    MigratorDbType.String => valueAsString,
                                    _ => throw new NotImplementedException($"Type '{column.MigratorDbType}' not yet supported - there are many variations. Please file an issue."),
                                }
                            };

                            filterItems.Add(filterItem);
                        }
                    }

                    var index = new Index
                    {
                        Clustered = !reader.IsDBNull(isClusteredOrdinal) && reader.GetBoolean(isClusteredOrdinal),
                        FilterItems = filterItems,
                        IncludeColumns = !string.IsNullOrWhiteSpace(includeColumns) ? [.. includeColumns.Split(',').Select(x => x.Trim())] : null,
                        KeyColumns = !string.IsNullOrWhiteSpace(indexColumns) ? [.. indexColumns.Split(',').Select(x => x.Trim())] : null,
                        Name = reader.GetString(indexNameOrdinal),
                        PrimaryKey = !reader.IsDBNull(isPrimaryConstraintOrdinal) && reader.GetBoolean(isPrimaryConstraintOrdinal),
                        Unique = !reader.IsDBNull(isUniqueOrdinal) && reader.GetBoolean(isUniqueOrdinal),
                        UniqueConstraint = !reader.IsDBNull(isUniqueConstraintOrdinal) && reader.GetBoolean(isUniqueConstraintOrdinal),
                    };

                    indexes.Add(index);
                }
            }
        }

        return [.. indexes];
    }

    public override void RemoveTable(string name)
    {
        if (!TableExists(name))
        {
            throw new MigrationException(string.Format("Table with name '{0}' does not exist to rename", name));
        }

        ExecuteNonQuery(string.Format("DROP TABLE IF EXISTS {0} CASCADE", name));
    }

    private IDbCommand MetadataCommand(string relation, string name = null)
    {
        var command = CreateCommand();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "relation";
        parameter.Value = QuoteTableNameIfRequired(relation);
        command.Parameters.Add(parameter);
        if (name != null)
        {
            parameter = command.CreateParameter();
            parameter.ParameterName = "name";
            parameter.Value = name;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    public override string[] GetConstraints(string table) => GetTableConstraints(table).Select(c => c.Name).Where(n => n != null).ToArray();

    public override bool ConstraintExists(string table, string name)
    {
        using var command = MetadataCommand(table, name);
        using var reader = ExecuteQuery(command, "SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass(@relation) AND (conname = @name OR conname = lower(@name))");
        return reader.Read();
    }

    public override bool ColumnExists(string table, string column)
    {
        using var command = MetadataCommand(table, column);
        using var reader = ExecuteQuery(command, "SELECT 1 FROM pg_attribute WHERE attrelid = to_regclass(@relation) AND attnum > 0 AND NOT attisdropped AND (attname = @name OR attname = lower(@name))");
        return reader.Read();
    }

    public override bool TableExists(string table)
    {
        using var command = MetadataCommand(table);
        using var reader = ExecuteQuery(command, "SELECT 1 FROM pg_class WHERE oid = to_regclass(@relation) AND relkind IN ('r', 'p', 'f')");
        return reader.Read();
    }

    public override bool ViewExists(string view)
    {
        using var command = MetadataCommand(view);
        using var reader = ExecuteQuery(command, "SELECT 1 FROM pg_class WHERE oid = to_regclass(@relation) AND relkind IN ('v', 'm')");
        return reader.Read();
    }

    private (string Table, string Schema) ResolveRelation(string table)
    {
        using var command = MetadataCommand(table);
        using var reader = ExecuteQuery(command, "SELECT c.relname, n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = to_regclass(@relation)");
        if (!reader.Read()) throw new MigrationException("Table does not exist: " + table);
        return (reader.GetString(0), reader.GetString(1));
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SELECT datname FROM pg_database WHERE datistemplate = false");
    }

    public override void ChangeColumn(string table, Column column)
    {
        var oldColumn = GetColumnByName(table, column.Name);


        var mapper = _dialect.GetAndMapColumnProperties(column);

        var change1 = string.Format("{0} TYPE {1}", QuoteColumnNameIfRequired(mapper.Name), mapper.Type);

        if (
            (oldColumn.MigratorDbType == MigratorDbType.Int16 ||
             oldColumn.MigratorDbType == MigratorDbType.Int32 ||
             oldColumn.MigratorDbType == MigratorDbType.Int64 ||
             oldColumn.MigratorDbType == MigratorDbType.Decimal) &&
             column.MigratorDbType == MigratorDbType.Boolean)
        {
            change1 += string.Format(" USING CASE {0} WHEN 1 THEN true ELSE false END", QuoteColumnNameIfRequired(mapper.Name));
        }
        else if (column.MigratorDbType == MigratorDbType.Boolean)
        {
            change1 += string.Format(" USING CASE {0} WHEN '1' THEN true ELSE false END", QuoteColumnNameIfRequired(mapper.Name));
        }

        ChangeColumn(table, change1);

        if (mapper.Default != null)
        {
            var change2 = string.Format("{0} SET {1}", QuoteColumnNameIfRequired(mapper.Name), _dialect.Default(mapper.Default));
            ChangeColumn(table, change2);
        }
        else
        {
            var change2 = string.Format("{0} DROP DEFAULT", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change2);
        }

        if (!column.IsNullable)
        {
            var change3 = string.Format("{0} SET NOT NULL", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change3);
        }
        else
        {
            var change3 = string.Format("{0} DROP NOT NULL", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change3);
        }


    }

    public override void CreateDatabases(string databaseName)
    {
        ExecuteNonQuery(string.Format("CREATE DATABASE {0}", _dialect.Quote(databaseName)));
    }

    public override void SwitchDatabase(string databaseName)
    {
        _connection.ChangeDatabase(_dialect.Quote(databaseName));
    }

    public override void DropDatabases(string databaseName)
    {
        ExecuteNonQuery(string.Format("DROP DATABASE {0}", _dialect.Quote(databaseName)));
    }

    public override string[] GetTables()
    {
        var tables = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
        {
            while (reader.Read())
            {
                tables.Add((string)reader[0]);
            }
        }
        return [.. tables];
    }

    public override int GetColumnContentSize(string table, string columnName)
    {
        if (!TableExists(table))
        {
            throw new Exception($"Table '{table}' not found.");
        }

        if (!ColumnExists(table, columnName, true))
        {
            throw new Exception($"Column '{columnName}' does not exist");
        }

        var column = GetColumnByName(table, columnName);

        if (column.MigratorDbType != MigratorDbType.String)
        {
            throw new Exception($"Column '{columnName}' in table {table} is not of type string");
        }

        var result = ExecuteScalar($"SELECT MAX(LENGTH({QuoteColumnNameIfRequired(columnName)})) FROM {QuoteTableNameIfRequired(table)}");

        if (result == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(result);
    }

    public override Column[] GetColumns(string table)
    {
        var relation = ResolveRelation(table);
        var columnInfos = _postgreSQLSystemDataLoader.GetColumnInfos(relation.Table, relation.Schema);
        var columns = new List<Column>();
        var tableConstraints = _postgreSQLSystemDataLoader.GetTableConstraints(relation.Table, relation.Schema);
        var uniqueColumns = tableConstraints.Where(c => c.ConstraintType == "UNIQUE")
            .GroupBy(c => new { c.TableSchema, c.ConstraintName }).Where(g => g.Count() == 1)
            .Select(g => g.Single().ColumnName).ToHashSet(StringComparer.Ordinal);

        foreach (var columnInfo in columnInfos)
        {
            var isNullable = columnInfo.IsNullable == "YES";
            var isIdentity = columnInfo.IsIdentity == "YES";

            MigratorDbType dbType = 0;
            int? precision = null;
            int? scale = null;
            int? size = null;

            if (new[] { "timestamptz", "timestamp with time zone" }.Contains(columnInfo.DataType))
            {
                dbType = MigratorDbType.DateTimeOffset;
                precision = columnInfo.DateTimePrecision;
            }
            else if (columnInfo.DataType == "double precision")
            {
                dbType = MigratorDbType.Double;
                scale = columnInfo.NumericScale;
                precision = columnInfo.NumericPrecision;
            }
            else if (columnInfo.DataType == "timestamp" || columnInfo.DataType == "timestamp without time zone")
            {
                // 6 is the maximum in PostgreSQL
                if (columnInfo.DateTimePrecision > 5)
                {
                    dbType = MigratorDbType.DateTime2;
                }
                else
                {
                    dbType = MigratorDbType.DateTime;
                }

                precision = columnInfo.DateTimePrecision;
            }
            else if (columnInfo.DataType == "smallint")
            {
                dbType = MigratorDbType.Int16;
            }
            else if (columnInfo.DataType == "integer")
            {
                dbType = MigratorDbType.Int32;
            }
            else if (columnInfo.DataType == "bigint")
            {
                dbType = MigratorDbType.Int64;
            }
            else if (columnInfo.DataType == "numeric")
            {
                dbType = MigratorDbType.Decimal;
                precision = columnInfo.NumericPrecision;
                scale = columnInfo.NumericScale;
            }
            else if (columnInfo.DataType == "real")
            {
                dbType = MigratorDbType.Single;
            }
            else if (columnInfo.DataType == "interval")
            {
                dbType = MigratorDbType.Interval;
            }
            else if (columnInfo.DataType == "money")
            {
                dbType = MigratorDbType.Currency;
            }
            else if (columnInfo.DataType == "date")
            {
                dbType = MigratorDbType.Date;
            }
            else if (columnInfo.DataType == "byte")
            {
                dbType = MigratorDbType.Binary;
            }
            else if (columnInfo.DataType == "uuid")
            {
                dbType = MigratorDbType.Guid;
            }
            else if (columnInfo.DataType == "xml")
            {
                dbType = MigratorDbType.Xml;
            }
            else if (columnInfo.DataType == "time" || columnInfo.DataType == "time without time zone")
            {
                dbType = MigratorDbType.Time;
            }
            else if (columnInfo.DataType == "boolean")
            {
                dbType = MigratorDbType.Boolean;
            }
            else if (columnInfo.DataType == "text" || columnInfo.DataType == "character varying")
            {
                dbType = MigratorDbType.String;
                size = columnInfo.CharacterMaximumLength;
            }
            else if (columnInfo.DataType == "bytea")
            {
                dbType = MigratorDbType.Binary;
            }
            else if (columnInfo.DataType == "character" || columnInfo.DataType.StartsWith("character("))
            {
                throw new NotSupportedException("Data type 'character' detected. 'character' is not supported. Use 'text' or 'character varying' instead.");
            }
            else
            {
                throw new NotImplementedException("The data type is not implemented. Please file an issue.");
            }

            var column = new Column(columnInfo.ColumnName, dbType)
            {
                Precision = precision,
                Scale = scale,
                // Size should be nullable
                Size = size ?? 0
            };

            column.IsNullable = isNullable;

            if (isIdentity)
            {
                column.IsIdentity = true;
            }

            if (!isIdentity) PostgreSqlColumnDefault.Apply(column, columnInfo.ColumnDefault);

            columns.Add(column);
        }

        return columns.ToArray();
    }

    public override Column GetColumnByName(string table, string columnName)
    {
        // Duplicate because of the lower case issue
        return Array.Find(GetColumns(table), x => x.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase) || x.Name == columnName);
    }

    public override bool IndexExists(string table, string name)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT indexname FROM pg_catalog.pg_indexes WHERE indexname = lower('{0}')", name));

        return reader.Read();
    }

    public override void UpdateTargetFromSource(string tableSourceNotQuoted, string tableTargetNotQuoted, ColumnPair[] fromSourceToTargetColumnPairs, ColumnPair[] conditionColumnPairs)
    {
        if (!TableExists(tableSourceNotQuoted))
        {
            throw new Exception($"Table '{tableSourceNotQuoted}' given in '{nameof(tableSourceNotQuoted)}' does not exist");
        }

        if (!TableExists(tableTargetNotQuoted))
        {
            throw new Exception($"Table '{tableTargetNotQuoted}' given in '{nameof(tableTargetNotQuoted)}' does not exist");
        }

        if (fromSourceToTargetColumnPairs.Length == 0)
        {
            throw new Exception($"{nameof(fromSourceToTargetColumnPairs)} is empty.");
        }

        if (fromSourceToTargetColumnPairs.Any(x => string.IsNullOrWhiteSpace(x.ColumnNameSource) || string.IsNullOrWhiteSpace(x.ColumnNameTarget)))
        {
            throw new Exception($"One of the strings in {nameof(fromSourceToTargetColumnPairs)} is null or empty");
        }

        if (conditionColumnPairs.Length == 0)
        {
            throw new Exception($"{nameof(conditionColumnPairs)} is empty.");
        }

        if (conditionColumnPairs.Any(x => string.IsNullOrWhiteSpace(x.ColumnNameSource) || string.IsNullOrWhiteSpace(x.ColumnNameTarget)))
        {
            throw new Exception($"One of the strings in {nameof(conditionColumnPairs)} is null or empty");
        }

        var tableNameSource = QuoteTableNameIfRequired(tableSourceNotQuoted);
        var tableNameTarget = QuoteTableNameIfRequired(tableTargetNotQuoted);

        var assignStrings = fromSourceToTargetColumnPairs.Select(x => $"{QuoteColumnNameIfRequired(x.ColumnNameTarget)} = {tableNameSource}.{QuoteColumnNameIfRequired(x.ColumnNameSource)}").ToList();

        var conditionStrings = conditionColumnPairs.Select(x => $"{tableNameSource}.{QuoteColumnNameIfRequired(x.ColumnNameSource)} = {tableNameTarget}.{QuoteColumnNameIfRequired(x.ColumnNameTarget)}");

        var assignStringsJoined = string.Join(", ", assignStrings);
        var conditionStringsJoined = string.Join(" AND ", conditionStrings);

        var sql = $"UPDATE {tableNameTarget} SET {assignStringsJoined} FROM {tableNameSource} WHERE {conditionStringsJoined}";
        ExecuteNonQuery(sql);
    }

    public override void CopyDataFromTableToTable(string sourceTableName, List<string> sourceColumnNames, string targetTableName, List<string> targetColumnNames, List<string> orderBySourceColumns = null)
    {
        orderBySourceColumns ??= [];

        if (!TableExists(sourceTableName))
        {
            throw new Exception($"Source table '{QuoteTableNameIfRequired(sourceTableName)}' does not exist");
        }

        if (!TableExists(targetTableName))
        {
            throw new Exception($"Target table '{QuoteTableNameIfRequired(targetTableName)}' does not exist");
        }

        var sourceColumnsConcatenated = sourceColumnNames.Concat(orderBySourceColumns);

        foreach (var column in sourceColumnsConcatenated)
        {
            if (!ColumnExists(sourceTableName, column))
            {
                throw new Exception($"Column {column} in source table does not exist.");
            }
        }

        foreach (var column in targetColumnNames)
        {
            if (!ColumnExists(targetTableName, column))
            {
                throw new Exception($"Column {column} in target table does not exist.");
            }
        }

        if (!orderBySourceColumns.All(x => sourceColumnNames.Contains(x)))
        {
            throw new Exception($"All columns in {nameof(orderBySourceColumns)} must be in {nameof(sourceColumnNames)}");
        }

        var sourceTableNameQuoted = QuoteTableNameIfRequired(sourceTableName);
        var targetTableNameQuoted = QuoteTableNameIfRequired(targetTableName);

        var sourceColumnNamesQuoted = sourceColumnNames.Select(QuoteColumnNameIfRequired).ToList();
        var targetColumnNamesQuoted = targetColumnNames.Select(QuoteColumnNameIfRequired).ToList();
        var orderBySourceColumnsQuoted = orderBySourceColumns.Select(QuoteColumnNameIfRequired).ToList();

        var sourceColumnsJoined = string.Join(", ", sourceColumnNamesQuoted);
        var targetColumnsJoined = string.Join(", ", targetColumnNamesQuoted);
        var orderBySourceColumnsJoined = string.Join(", ", orderBySourceColumnsQuoted);

        var orderByComponent = !string.IsNullOrWhiteSpace(orderBySourceColumnsJoined) ? $"ORDER BY {orderBySourceColumnsJoined}" : null;

        List<string> sqlComponents =
        [
            $"INSERT INTO {targetTableNameQuoted} ({targetColumnsJoined}) SELECT {sourceColumnsJoined} FROM {sourceTableNameQuoted}",
            orderByComponent
        ];

        var sql = string.Join(" ", sqlComponents.Where(x => x != null));
        ExecuteNonQuery(sql);
    }

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        if (value is TimeSpan interval)
        {
            // Npgsql infers interval from TimeSpan; setting DbType.Time would change its meaning.
            parameter.Value = interval;
        }
        else if (value is ushort)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = Convert.ToInt32(value);
        }
        else if (value is uint)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = Convert.ToInt64(value);
        }
        else if (value is ulong unsigned)
        {
            // PostgreSQL has no unsigned bigint; the dialect uses numeric(20,0).
            parameter.DbType = DbType.Decimal;
            parameter.Value = (decimal)unsigned;
        }
        else
        {
            base.ConfigureParameterWithValue(parameter, index, value);
        }
    }

    private void Initialize()
    {
        _postgreSQLSystemDataLoader = new PostgreSQLSystemDataLoader(this);
    }
}
