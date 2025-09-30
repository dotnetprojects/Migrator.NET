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
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL;

/// <summary>
/// Migration transformations provider for PostgreSql (using NPGSql .Net driver)
/// </summary>
public class PostgreSQLTransformationProvider : TransformationProvider
{
    private Regex stripSingleQuoteRegEx = new("(?<=')[^']*(?=')");

    public PostgreSQLTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
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
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT conname FROM pg_constraint WHERE contype = 'p' AND conrelid = (SELECT oid FROM pg_class WHERE relname = lower('{0}'));", table));

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
            includeString = $"INCLUDE ({string.Join(", ", index.IncludeColumns)})";
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
                    string stringValue => $"'{stringValue}'",
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

    public override bool ConstraintExists(string table, string name)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT constraint_name FROM information_schema.table_constraints WHERE table_schema = 'public' AND constraint_name = lower('{0}')", name));

        return reader.Read();
    }

    public override bool ColumnExists(string table, string column)
    {
        if (!TableExists(table))
        {
            return false;
        }

        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = lower('{0}') AND (column_name = lower('{1}') OR column_name = '{1}')", table, column));
        return reader.Read();
    }

    public override bool TableExists(string table)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name = lower('{0}')", table));
        return reader.Read();
    }

    public override bool ViewExists(string view)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT table_name FROM information_schema.views WHERE table_schema = 'public' AND table_name = lower('{0}')", view));

        return reader.Read();
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SELECT datname FROM pg_database WHERE datistemplate = false");
    }

    public override void ChangeColumn(string table, Column column)
    {
        var oldColumn = GetColumnByName(table, column.Name);

        var isUniqueSet = column.ColumnProperty.IsSet(ColumnProperty.Unique);

        column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Unique);

        var mapper = _dialect.GetAndMapColumnProperties(column);

        var change1 = string.Format("{0} TYPE {1}", QuoteColumnNameIfRequired(mapper.Name), mapper.type);

        if ((oldColumn.MigratorDbType == MigratorDbType.Int16 || oldColumn.MigratorDbType == MigratorDbType.Int32 || oldColumn.MigratorDbType == MigratorDbType.Int64 || oldColumn.MigratorDbType == MigratorDbType.Decimal) && column.MigratorDbType == MigratorDbType.Boolean)
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

        if (column.ColumnProperty.HasFlag(ColumnProperty.NotNull))
        {
            var change3 = string.Format("{0} SET NOT NULL", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change3);
        }
        else
        {
            var change3 = string.Format("{0} DROP NOT NULL", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change3);
        }

        if (isUniqueSet)
        {
            AddUniqueConstraint(string.Format("UX_{0}_{1}", table, column.Name), table, [column.Name]);
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
        return tables.ToArray();
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
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("SELECT");
        stringBuilder.AppendLine("  COLUMN_NAME,");
        stringBuilder.AppendLine("  IS_NULLABLE,");
        stringBuilder.AppendLine("  COLUMN_DEFAULT,");
        stringBuilder.AppendLine("  DATA_TYPE,");
        stringBuilder.AppendLine("  DATETIME_PRECISION,");
        stringBuilder.AppendLine("  CHARACTER_MAXIMUM_LENGTH,");
        stringBuilder.AppendLine("  NUMERIC_PRECISION,");
        stringBuilder.AppendLine("  NUMERIC_SCALE");
        stringBuilder.AppendLine($"FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'public' AND TABLE_NAME = lower('{table}');");

        var columns = new List<Column>();

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, stringBuilder.ToString()))
        {
            while (reader.Read())
            {
                var defaultValueOrdinal = reader.GetOrdinal("COLUMN_DEFAULT");
                var characterMaximumLengthOrdinal = reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH");
                var dateTimePrecisionOrdinal = reader.GetOrdinal("DATETIME_PRECISION");
                var numericPrecisionOrdinal = reader.GetOrdinal("NUMERIC_PRECISION");
                var numericScaleOrdinal = reader.GetOrdinal("NUMERIC_SCALE");

                var columnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME"));
                var isNullable = reader.GetString(reader.GetOrdinal("IS_NULLABLE")) == "YES";
                var defaultValueString = reader.IsDBNull(defaultValueOrdinal) ? null : reader.GetString(defaultValueOrdinal);
                var dataTypeString = reader.GetString(reader.GetOrdinal("DATA_TYPE"));
                var dateTimePrecision = reader.IsDBNull(dateTimePrecisionOrdinal) ? null : (int?)reader.GetInt32(dateTimePrecisionOrdinal);
                var characterMaximumLength = reader.IsDBNull(characterMaximumLengthOrdinal) ? null : (int?)reader.GetInt32(characterMaximumLengthOrdinal);
                var numericPrecision = reader.IsDBNull(numericPrecisionOrdinal) ? null : (int?)reader.GetInt32(numericPrecisionOrdinal);
                var numericScale = reader.IsDBNull(numericScaleOrdinal) ? null : (int?)reader.GetInt32(numericScaleOrdinal);

                MigratorDbType dbType = 0;
                int? precision = null;
                int? scale = null;
                int? size = null;

                if (new[] { "timestamptz", "timestamp with time zone" }.Contains(dataTypeString))
                {
                    dbType = MigratorDbType.DateTimeOffset;
                    precision = dateTimePrecision;
                }
                else if (dataTypeString == "double precision")
                {
                    dbType = MigratorDbType.Double;
                    scale = numericScale;
                    precision = numericPrecision;
                }
                else if (dataTypeString == "timestamp" || dataTypeString == "timestamp without time zone")
                {
                    // 6 is the maximum in PostgreSQL
                    if (dateTimePrecision > 5)
                    {
                        dbType = MigratorDbType.DateTime2;
                    }
                    else
                    {
                        dbType = MigratorDbType.DateTime;
                    }

                    precision = dateTimePrecision;
                }
                else if (dataTypeString == "smallint")
                {
                    dbType = MigratorDbType.Int16;
                }
                else if (dataTypeString == "integer")
                {
                    dbType = MigratorDbType.Int32;
                }
                else if (dataTypeString == "bigint")
                {
                    dbType = MigratorDbType.Int64;
                }
                else if (dataTypeString == "numeric")
                {
                    dbType = MigratorDbType.Decimal;
                    precision = numericPrecision;
                    scale = numericScale;
                }
                else if (dataTypeString == "real")
                {
                    dbType = MigratorDbType.Single;
                }
                else if (dataTypeString == "interval")
                {
                    dbType = MigratorDbType.Interval;
                }
                else if (dataTypeString == "money")
                {
                    dbType = MigratorDbType.Currency;
                }
                else if (dataTypeString == "date")
                {
                    dbType = MigratorDbType.Date;
                }
                else if (dataTypeString == "byte")
                {
                    dbType = MigratorDbType.Binary;
                }
                else if (dataTypeString == "uuid")
                {
                    dbType = MigratorDbType.Guid;
                }
                else if (dataTypeString == "xml")
                {
                    dbType = MigratorDbType.Xml;
                }
                else if (dataTypeString == "time")
                {
                    dbType = MigratorDbType.Time;
                }
                else if (dataTypeString == "boolean")
                {
                    dbType = MigratorDbType.Boolean;
                }
                else if (dataTypeString == "text" || dataTypeString == "character varying")
                {
                    dbType = MigratorDbType.String;
                    size = characterMaximumLength;
                }
                else if (dataTypeString == "bytea")
                {
                    dbType = MigratorDbType.Binary;
                }
                else if (dataTypeString == "character" || dataTypeString.StartsWith("character("))
                {
                    throw new NotSupportedException("Data type 'character' detected. 'character' is not supported. Use 'text' or 'character varying' instead.");
                }
                else
                {
                    throw new NotImplementedException("The data type is not implemented. Please file an issue.");
                }

                var column = new Column(columnName, dbType)
                {
                    Precision = precision,
                    Scale = scale,
                    // Size should be nullable
                    Size = size ?? 0
                };

                column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

                if (defaultValueString != null)
                {
                    if (column.MigratorDbType == MigratorDbType.Int16 || column.MigratorDbType == MigratorDbType.Int32 || column.MigratorDbType == MigratorDbType.Int64)
                    {
                        column.DefaultValue = long.Parse(defaultValueString.ToString());
                    }
                    else if (column.MigratorDbType == MigratorDbType.UInt16 || column.MigratorDbType == MigratorDbType.UInt32 || column.MigratorDbType == MigratorDbType.UInt64)
                    {
                        column.DefaultValue = ulong.Parse(defaultValueString.ToString());
                    }
                    else if (column.MigratorDbType == MigratorDbType.Double || column.MigratorDbType == MigratorDbType.Single)
                    {
                        column.DefaultValue = double.Parse(defaultValueString.ToString(), CultureInfo.InvariantCulture);
                    }
                    else if (column.MigratorDbType == MigratorDbType.Interval)
                    {
                        if (defaultValueString.StartsWith("'"))
                        {
                            var match = stripSingleQuoteRegEx.Match(defaultValueString);

                            if (!match.Success)
                            {
                                throw new Exception("Postgre default value for interval: Single quotes around the interval string are expected.");
                            }

                            column.DefaultValue = match.Value;
                            var splitted = match.Value.Split(':');
                            if (splitted.Length != 3)
                            {
                                throw new NotImplementedException($"Cannot interpret {defaultValueString} in column '{column.Name}' unexpected pattern.");
                            }

                            var hours = int.Parse(splitted[0], CultureInfo.InvariantCulture);
                            var minutes = int.Parse(splitted[1], CultureInfo.InvariantCulture);
                            var splitted2 = splitted[2].Split('.');
                            var seconds = int.Parse(splitted2[0], CultureInfo.InvariantCulture);
                            var milliseconds = int.Parse(splitted2[1], CultureInfo.InvariantCulture);

                            column.DefaultValue = new TimeSpan(0, hours, minutes, seconds, milliseconds);
                        }
                        else
                        {
                            // We assume that the value was added using this migrator so we do not interpret things like '2 days 01:02:03' if you
                            // added such format you will run into this exception.
                            throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}' unexpected pattern.");
                        }
                    }
                    else if (column.MigratorDbType == MigratorDbType.Boolean)
                    {
                        var truthy = new[] { "TRUE", "YES", "'true'", "on", "'on'", "t", "'t'" };
                        var falsy = new[] { "FALSE", "NO", "'false'", "off", "'off'", "f", "'f'" };

                        if (truthy.Any(x => x.Equals(defaultValueString.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            column.DefaultValue = true;
                        }
                        else if (falsy.Any(x => x.Equals(defaultValueString.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            column.DefaultValue = false;
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                        }
                    }
                    else if (column.MigratorDbType == MigratorDbType.DateTime || column.MigratorDbType == MigratorDbType.DateTime2)
                    {
                        if (defaultValueString.StartsWith("'"))
                        {
                            var match = stripSingleQuoteRegEx.Match(defaultValueString);

                            if (!match.Success)
                            {
                                throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                            }

                            var timeString = match.Value;

                            // We convert to UTC since we restrict date time default values to UTC on default value definition.
                            var dateTimeExtracted = DateTime.ParseExact(timeString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                            column.DefaultValue = dateTimeExtracted;
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                        }
                    }
                    else if (column.MigratorDbType == MigratorDbType.Guid)
                    {
                        if (defaultValueString.StartsWith("'"))
                        {
                            var match = stripSingleQuoteRegEx.Match(defaultValueString);

                            if (!match.Success)
                            {
                                throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                            }

                            column.DefaultValue = Guid.Parse(match.Value);
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                        }
                    }
                    else if (column.MigratorDbType == MigratorDbType.Decimal)
                    {
                        column.DefaultValue = decimal.Parse(defaultValueString, CultureInfo.InvariantCulture);
                    }
                    else if (column.MigratorDbType == MigratorDbType.String)
                    {
                        if (defaultValueString.StartsWith("'"))
                        {
                            var match = stripSingleQuoteRegEx.Match(defaultValueString);

                            if (!match.Success)
                            {
                                throw new Exception("Postgre default value for date time: Single quotes around the date time string are expected.");
                            }

                            column.DefaultValue = match.Value;
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    else if (column.MigratorDbType == MigratorDbType.Binary)
                    {
                        if (defaultValueString.StartsWith("'"))
                        {
                            var match = stripSingleQuoteRegEx.Match(defaultValueString);

                            if (!match.Success)
                            {
                                throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                            }

                            var singleQuoteString = match.Value;

                            if (!singleQuoteString.StartsWith("\\x"))
                            {
                                throw new Exception(@"Postgre \x notation expected.");
                            }

                            var hexString = singleQuoteString.Substring(2);

                            // Not available in old .NET version: Convert.FromHexString(hexString);

                            column.DefaultValue = Enumerable.Range(0, hexString.Length / 2)
                                .Select(x => Convert.ToByte(hexString.Substring(x * 2, 2), 16))
                                .ToArray();
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse {defaultValueString} in column '{column.Name}'");
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                columns.Add(column);
            }
        }

        return columns.ToArray();
    }

    public override string[] GetConstraints(string table)
    {
        var constraints = new List<string>();

        using (var cmd = CreateCommand())
        using (
            var reader =
                ExecuteQuery(
                    cmd, string.Format(@"select c.conname as constraint_name
from pg_constraint c
join pg_class t on c.conrelid = t.oid
where LOWER(t.relname) = LOWER('{0}')", table)))
        {
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }
        }

        return constraints.ToArray();
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

    public override void UpdateFromTableToTable(string tableSourceNotQuoted, string tableTargetNotQuoted, ColumnPair[] fromSourceToTargetColumnPairs, ColumnPair[] conditionColumnPairs)
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

        if (fromSourceToTargetColumnPairs.Any(x => string.IsNullOrWhiteSpace(x.ColumnNameSourceNotQuoted) || string.IsNullOrWhiteSpace(x.ColumnNameTargetNotQuoted)))
        {
            throw new Exception($"One of the strings in {nameof(fromSourceToTargetColumnPairs)} is null or empty");
        }

        if (conditionColumnPairs.Length == 0)
        {
            throw new Exception($"{nameof(conditionColumnPairs)} is empty.");
        }

        if (conditionColumnPairs.Any(x => string.IsNullOrWhiteSpace(x.ColumnNameSourceNotQuoted) || string.IsNullOrWhiteSpace(x.ColumnNameTargetNotQuoted)))
        {
            throw new Exception($"One of the strings in {nameof(conditionColumnPairs)} is null or empty");
        }

        var tableNameSource = QuoteTableNameIfRequired(tableSourceNotQuoted);
        var tableNameTarget = QuoteTableNameIfRequired(tableTargetNotQuoted);

        var assignStrings = fromSourceToTargetColumnPairs.Select(x => $"{QuoteColumnNameIfRequired(x.ColumnNameTargetNotQuoted)} = {tableNameSource}.{QuoteColumnNameIfRequired(x.ColumnNameSourceNotQuoted)}").ToList();

        var conditionStrings = conditionColumnPairs.Select(x => $"{tableNameSource}.{QuoteColumnNameIfRequired(x.ColumnNameSourceNotQuoted)} = {tableNameTarget}.{QuoteColumnNameIfRequired(x.ColumnNameTargetNotQuoted)}");

        var assignStringsJoined = string.Join(", ", assignStrings);
        var conditionStringsJoined = string.Join(" AND ", conditionStrings);

        var sql = $"UPDATE {tableNameTarget} SET {assignStringsJoined} FROM {tableNameSource} WHERE {conditionStringsJoined}";
        ExecuteNonQuery(sql);
    }

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        if (value is ushort)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = Convert.ToInt32(value);
        }
        else if (value is uint)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = Convert.ToInt64(value);
        }
        else
        {
            base.ConfigureParameterWithValue(parameter, index, value);
        }
    }
}
