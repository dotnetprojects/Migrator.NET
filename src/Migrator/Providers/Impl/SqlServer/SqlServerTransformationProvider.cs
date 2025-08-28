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
using DotNetProjects.Migrator.Providers.Models.Indexes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.SqlServer;

/// <summary>
/// Migration transformations provider for Microsoft SQL Server.
/// </summary>
public class SqlServerTransformationProvider : TransformationProvider
{
    public SqlServerTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        CreateConnection(providerName);
    }

    public SqlServerTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope)
    {
    }


    protected virtual void CreateConnection(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "System.Data.SqlClient";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();

        string collationString = null;
        var collation = ExecuteScalar("SELECT DATABASEPROPERTYEX('" + _connection.Database + "', 'Collation')");

        if (collation != null)
        {
            collationString = collation.ToString();
        }

        if (string.IsNullOrWhiteSpace(collationString))
        {
            collationString = "Latin1_General_CI_AS";
        }

        Dialect.RegisterProperty(ColumnProperty.CaseSensitive, "COLLATE " + collationString.Replace("_CI_", "_CS_"));
    }

    public override bool TableExists(string tableName)
    {
        // This is not clean! Usually you should use schema as well as this query will find tables in other tables as well!

        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"SELECT OBJECT_ID('{tableName}', 'U')");

        if (reader.Read())
        {
            var result = reader.GetValue(0);
            var tableExists = result != DBNull.Value && result != null;

            return tableExists;
        }

        return false;
    }

    public override bool ViewExists(string viewName)
    {
        // This is not clean! Usually you should use schema as well as this query will find views in other tables as well!

        using var cmd = CreateCommand();
        cmd.CommandText = $"SELECT OBJECT_ID(@FullViewName, 'V')";

        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "@FullViewName";
        parameter.Value = viewName;
        cmd.Parameters.Add(parameter);

        var result = cmd.ExecuteScalar();

        var viewExists = result != DBNull.Value && result != null;

        return viewExists;
    }

    public override bool ConstraintExists(string table, string name)
    {
        var retVal = false;
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format("SELECT TOP 1 * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME ='{0}'", name)))
        {
            retVal = reader.Read();
        }

        if (!retVal)
        {
            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, string.Format("SELECT TOP 1 * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('{0}') AND name = '{1}'", table, name));
            return reader.Read();
        }

        return true;
    }

    public override void AddColumn(string table, string sqlColumn)
    {
        table = _dialect.TableNameNeedsQuote ? _dialect.Quote(table) : table;
        ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
    }

    public override void AddPrimaryKeyNonClustered(string name, string table, params string[] columns)
    {
        var nonclusteredString = "NONCLUSTERED";
        ExecuteNonQuery(
        string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} PRIMARY KEY {2} ({3}) ", table, name, nonclusteredString,
                      string.Join(",", QuoteColumnNamesIfRequired(columns))));
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
        var includeString = hasIncludedColumns ? $"INCLUDE ({string.Join(", ", index.IncludeColumns)})" : null;
        var filterString = string.Empty;
        var clusteredString = index.Clustered ? "CLUSTERED" : "NONCLUSTERED";

        if (index.FilterItems != null && index.FilterItems.Count > 0)
        {
            List<string> singleFilterStrings = [];

            foreach (var filterItem in index.FilterItems)
            {
                var comparisonString = _dialect.GetComparisonStringByFilterType(filterItem.Filter);

                var filterColumnQuoted = QuoteColumnNameIfRequired(filterItem.ColumnName);
                string value = null;

                if (filterItem.Value is bool booleanValue)
                {
                    value = booleanValue ? "1" : "0";
                }
                else if (filterItem.Value is string stringValue)
                {
                    value = $"'{stringValue}'";
                }
                else if (filterItem.Value is byte || filterItem.Value is short || filterItem.Value is int || filterItem.Value is long)
                {
                    value = Convert.ToInt64(filterItem.Value).ToString();
                }
                else if (filterItem.Value is sbyte || filterItem.Value is ushort || filterItem.Value is uint || filterItem.Value is ulong)
                {
                    value = Convert.ToUInt64(filterItem.Value).ToString();
                }
                else
                {
                    throw new NotImplementedException("Given type is not implemented. Please file an issue.");
                }

                var singleFilterString = $"{filterColumnQuoted} {comparisonString} {value}";

                singleFilterStrings.Add(singleFilterString);
            }

            filterString = $"WHERE {string.Join(" AND ", singleFilterStrings)}";
        }

        List<string> list = [];
        list.Add("CREATE");
        list.Add(uniqueString);
        list.Add(clusteredString);
        list.Add("INDEX");
        list.Add(name);
        list.Add("ON");
        list.Add(table);
        list.Add(columnsString);
        list.Add(includeString);
        list.Add(filterString);

        list = [.. list.Where(x => !string.IsNullOrWhiteSpace(x))];

        var sql = string.Join(" ", list);

        ExecuteNonQuery(sql);

        return sql;
    }

    public override void ChangeColumn(string table, Column column)
    {
        if (column.DefaultValue == null || column.DefaultValue == DBNull.Value)
        {
            base.ChangeColumn(table, column);
        }
        else
        {
            var def = column.DefaultValue;
            var notNull = column.ColumnProperty.IsSet(ColumnProperty.NotNull);
            column.DefaultValue = null;
            column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.Null);
            column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.NotNull);

            base.ChangeColumn(table, column);

            var mapper = _dialect.GetAndMapColumnPropertiesWithoutDefault(column);
            ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} {2} FOR {3}", this.QuoteTableNameIfRequired(table), "DF_" + table + "_" + column.Name, _dialect.Default(def), this.QuoteColumnNameIfRequired(column.Name)));

            if (notNull)
            {
                column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.NotNull);
                column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Null);
                base.ChangeColumn(table, column);
            }
        }
    }

    public override bool ColumnExists(string table, string column)
    {
        string schema;

        if (!TableExists(table))
        {
            return false;
        }

        var firstIndex = table.IndexOf(".");

        if (firstIndex >= 0)
        {
            schema = table.Substring(0, firstIndex);
            table = table.Substring(firstIndex + 1);
        }
        else
        {
            schema = _defaultSchema;
        }

        using var cmd = CreateCommand();
        using var reader = base.ExecuteQuery(cmd, string.Format("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{0}' AND TABLE_NAME='{1}' AND COLUMN_NAME='{2}'", schema, table, column));
        return reader.Read();
    }

    public override void RemoveColumnDefaultValue(string table, string column)
    {
        var sql = string.Format("SELECT name FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('{0}') AND parent_column_id = (SELECT column_id FROM sys.columns WHERE name = '{1}' AND object_id = OBJECT_ID('{0}'))", table, column);
        var constraintName = ExecuteScalar(sql);
        if (constraintName != null)
        {
            RemoveConstraint(table, constraintName.ToString());
        }
    }

    public override Index[] GetIndexes(string table)
    {
        // This migrator does not support schemas so we fall back to dbo in SQL Server
        var schemaName = "dbo";

        var indexes = new List<Index>();

        var sql = @$"SELECT
                        s.name AS SchemaName,
                        t.name AS TableName,
                        i.name AS IndexName,
                        i.type_desc AS IndexType,
                        i.is_unique AS IsUnique,
                        i.is_primary_key AS IsPrimaryKey,
                        i.is_unique_constraint AS IsUniqueConstraint,
                        ic.index_column_id AS ColumnOrder,
                        col.name AS ColumnName,
                        ic.is_descending_key AS IsDescending,
                        ic.is_included_column AS IsIncludedColumn,
                        i.has_filter AS IsFilteredIndex,
                        i.filter_definition AS FilterDefinition
                    FROM
                        sys.indexes i
                        JOIN sys.tables t ON i.object_id = t.object_id
                        JOIN sys.schemas s ON t.schema_id = s.schema_id
                        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                        JOIN sys.columns col ON ic.object_id = col.object_id AND ic.column_id = col.column_id
                    WHERE
                        LOWER(t.name) = '{table.ToLowerInvariant()}' AND
                        LOWER(s.name) = '{schemaName.ToLowerInvariant()}'
                    ORDER BY
                        s.name, t.name, i.name, ic.index_column_id";

        List<IndexItem> indexItems = [];

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
        {
            var columnNameOrdinal = reader.GetOrdinal("ColumnName");
            var columnOrderOrdinal = reader.GetOrdinal("ColumnOrder");
            var filterDefinitionOrdinal = reader.GetOrdinal("FilterDefinition");
            var indexNameOrdinal = reader.GetOrdinal("IndexName");
            var indexTypeOrdinal = reader.GetOrdinal("IndexType");
            var isDescendingOrdinal = reader.GetOrdinal("IsDescending");
            var isFilteredIndexOrdinal = reader.GetOrdinal("IsFilteredIndex");
            var isIncludedColumnOrdinal = reader.GetOrdinal("IsIncludedColumn");
            var isPrimaryKeyOrdinal = reader.GetOrdinal("IsPrimaryKey");
            var isUniqueConstraintOrdinal = reader.GetOrdinal("IsUniqueConstraint");
            var isUniqueOrdinal = reader.GetOrdinal("IsUnique");
            var schemaNameOrdinal = reader.GetOrdinal("SchemaName");
            var tableNameOrdinal = reader.GetOrdinal("TableName");



            while (reader.Read())
            {
                var indexItem = new IndexItem
                {
                    Clustered = reader.GetString(indexTypeOrdinal) == "CLUSTERED",
                    ColumnName = reader.GetString(columnNameOrdinal),
                    ColumnOrder = reader.GetInt32(columnOrderOrdinal),
                    FilterString = !reader.IsDBNull(filterDefinitionOrdinal) ? reader.GetString(filterDefinitionOrdinal) : null,
                    IsFilteredIndex = reader.GetBoolean(isFilteredIndexOrdinal),
                    IsIncludedColumn = reader.GetBoolean(isIncludedColumnOrdinal),
                    Name = reader.GetString(indexNameOrdinal),
                    PrimaryKey = reader.GetBoolean(isPrimaryKeyOrdinal),
                    SchemaName = reader.GetString(schemaNameOrdinal),
                    TableName = reader.GetString(tableNameOrdinal),
                    Unique = reader.GetBoolean(isUniqueOrdinal),
                    UniqueConstraint = reader.GetBoolean(isUniqueConstraintOrdinal),
                };

                indexItems.Add(indexItem);
            }
        }

        var indexGroups = indexItems.GroupBy(x => new
        {
            x.Name,
            x.SchemaName,
            x.TableName,
        });

        foreach (var indexGroup in indexGroups)
        {
            var first = indexGroup.First();

            List<FilterItem> filterItems = [];

            if (!string.IsNullOrWhiteSpace(first.FilterString))
            {
                const string unexpectedPatternString = "Unexpected pattern in filter string detected. Not implemented yet - please file an issue";
                var comparisonStrings = _dialect.GetComparisonStrings();
                var stripOuterBracesRegex = new Regex(@"(?<=^\().+(?=\)$)");
                var stripBracesMatch = stripOuterBracesRegex.Match(first.FilterString.Trim());

                if (!stripBracesMatch.Success)
                {
                    throw new NotImplementedException(unexpectedPatternString);
                }

                var andSplitted = Regex.Split(stripBracesMatch.Value, @" AND (?=\[)")
                    .Select(x => x.Trim())
                    .ToList();

                var columns = GetColumns(table: table);

                foreach (var andSplittedItem in andSplitted)
                {
                    var filterItem = new FilterItem();
                    // We assume nobody uses column names with brackets in it.
                    var columnRegex = new Regex(@"(?<=^\[)[^\]]+");
                    var columnMatch = columnRegex.Match(andSplittedItem);

                    if (!columnMatch.Success)
                    {
                        throw new NotImplementedException(unexpectedPatternString);
                    }

                    filterItem.ColumnName = columnMatch.Value;
                    var column = columns.OrderByDescending(x => x.Name).First(x => x.Name.Equals(filterItem.ColumnName, StringComparison.OrdinalIgnoreCase));

                    var remainingString = andSplittedItem.Substring(filterItem.ColumnName.Length + 2);
                    var comparisonString = comparisonStrings.OrderByDescending(x => x.Length)
                        .First(x => remainingString.StartsWith(x));

                    filterItem.Filter = _dialect.GetFilterTypeByComparisonString(comparisonString);
                    remainingString = remainingString.Substring(comparisonString.Length);

                    var valueRegex = new Regex(@"(?<=^[\(|']).+(?=[\)|']$)");
                    var valueStringMatch = valueRegex.Match(remainingString);

                    if (!valueStringMatch.Success)
                    {
                        throw new NotImplementedException(unexpectedPatternString);
                    }

                    var valueAsString = valueStringMatch.Value;

                    filterItem.Value = column.MigratorDbType switch
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
                        _ => throw new NotImplementedException("Type not yet supported. Please file an issue."),
                    };

                    filterItems.Add(filterItem);
                }
            }

            var index = new Index
            {
                Clustered = first.Clustered,
                FilterItems = filterItems,
                IncludeColumns = [.. indexGroup.Where(x => x.IsIncludedColumn)
                        .OrderBy(x => x.ColumnOrder)
                        .Select(x => x.ColumnName)
                        .Distinct()],
                KeyColumns = [.. indexGroup.Where(x => !x.IsIncludedColumn)
                       .OrderBy(x => x.ColumnOrder)
                       .Select(x => x.ColumnName)
                       .Distinct()],
                Name = first.Name,
                PrimaryKey = first.PrimaryKey,
                Unique = first.Unique,
                UniqueConstraint = first.UniqueConstraint,
            };

            indexes.Add(index);

        }

        return [.. indexes];
    }

    public override int GetColumnContentSize(string table, string columnName)
    {
        var result = ExecuteScalar("SELECT MAX(LEN(" + this.QuoteColumnNameIfRequired(columnName) + ")) FROM " + this.QuoteTableNameIfRequired(table));

        if (result == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(result);
    }

    public override Column[] GetColumns(string table)
    {
        string schema;

        var firstIndex = table.IndexOf(".");
        if (firstIndex >= 0)
        {
            schema = table.Substring(0, firstIndex);
            table = table.Substring(firstIndex + 1);
        }
        else
        {
            schema = _defaultSchema;
        }

        var pkColumns = new List<string>();
        try
        {
            pkColumns = ExecuteStringQuery("SELECT cu.COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE cu WHERE EXISTS ( SELECT tc.* FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc WHERE tc.TABLE_NAME = '{0}' AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME )", table);
        }
        catch (Exception)
        { }

        var idtColumns = new List<string>();
        try
        {
            idtColumns = ExecuteStringQuery("SELECT COLUMN_NAME from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = '{1}' and TABLE_NAME = '{0}' and COLUMNPROPERTY(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1", table, schema);
        }
        catch (Exception)
        { }

        var columns = new List<Column>();
        using (var cmd = CreateCommand())
        using (
                var reader =
                ExecuteQuery(cmd,
                    string.Format("SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE, ISNULL(CHARACTER_MAXIMUM_LENGTH , NUMERIC_PRECISION), COLUMN_DEFAULT, NUMERIC_SCALE, CHARACTER_MAXIMUM_LENGTH from INFORMATION_SCHEMA.COLUMNS where table_name = '{0}'", table)))
        {
            while (reader.Read())
            {
                var column = new Column(reader.GetString(0), DbType.String);

                var defaultValueOrdinal = reader.GetOrdinal("COLUMN_DEFAULT");
                var dataTypeOrdinal = reader.GetOrdinal("DATA_TYPE");
                var characterMaximumLengthOrdinal = reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH");

                var defaultValueString = reader.IsDBNull(defaultValueOrdinal) ? null : reader.GetString(defaultValueOrdinal).Trim();
                var characterMaximumLength = reader.IsDBNull(characterMaximumLengthOrdinal) ? (int?)null : reader.GetInt32(characterMaximumLengthOrdinal);

                if (pkColumns.Contains(column.Name))
                {
                    column.ColumnProperty |= ColumnProperty.PrimaryKey;
                }

                if (idtColumns.Contains(column.Name))
                {
                    column.ColumnProperty |= ColumnProperty.Identity;
                }

                var nullableStr = reader.GetString(1);
                var isNullable = nullableStr == "YES";

                var dataTypeString = reader.GetString(dataTypeOrdinal);

                if (dataTypeString == "date")
                {
                    column.MigratorDbType = MigratorDbType.Date;
                }
                else if (dataTypeString == "int")
                {
                    column.MigratorDbType = MigratorDbType.Int32;
                }
                else if (dataTypeString == "bigint")
                {
                    column.MigratorDbType = MigratorDbType.Int64;
                }
                else if (dataTypeString == "smallint")
                {
                    column.MigratorDbType = MigratorDbType.Int16;
                }
                else if (dataTypeString == "tinyint")
                {
                    column.MigratorDbType = MigratorDbType.Byte;
                }
                else if (dataTypeString == "bit")
                {
                    column.MigratorDbType = MigratorDbType.Boolean;
                }
                else if (dataTypeString == "money")
                {
                    column.MigratorDbType = MigratorDbType.Currency;
                }
                else if (dataTypeString == "float")
                {
                    column.MigratorDbType = MigratorDbType.Double;
                }
                else if (new[] { "text", "nchar", "ntext", "varchar", "nvarchar" }.Contains(dataTypeString))
                {
                    // We use string for all string-like data types.
                    column.MigratorDbType = MigratorDbType.String;
                    column.Size = characterMaximumLength.Value;
                }
                else if (dataTypeString == "decimal")
                {
                    column.MigratorDbType = MigratorDbType.Decimal;
                }
                else if (dataTypeString == "datetime")
                {
                    column.MigratorDbType = MigratorDbType.DateTime;
                }
                else if (dataTypeString == "datetime2")
                {
                    column.MigratorDbType = MigratorDbType.DateTime2;
                }
                else if (dataTypeString == "datetimeoffset")
                {
                    column.MigratorDbType = MigratorDbType.DateTimeOffset;
                }
                else if (dataTypeString == "binary" || dataTypeString == "varbinary")
                {
                    column.MigratorDbType = MigratorDbType.Binary;
                }
                else if (dataTypeString == "uniqueidentifier")
                {
                    column.MigratorDbType = MigratorDbType.Guid;
                }
                else if (dataTypeString == "real")
                {
                    column.MigratorDbType = MigratorDbType.Single;
                }
                else
                {
                    throw new NotImplementedException($"The data type '{dataTypeString}' is not implemented yet. Please file an issue.");
                }

                if (!reader.IsDBNull(3))
                {
                    column.Size = reader.GetInt32(3);
                }

                if (defaultValueString != null)
                {
                    var bracesStrippedString = defaultValueString.Replace("(", "").Replace(")", "").Trim();
                    var bracesAndSingleQuoteStrippedString = bracesStrippedString.Replace("'", "");

                    if (column.Type == DbType.Int16 || column.Type == DbType.Int32 || column.Type == DbType.Int64)
                    {
                        column.DefaultValue = long.Parse(bracesAndSingleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.UInt16 || column.Type == DbType.UInt32 || column.Type == DbType.UInt64)
                    {
                        column.DefaultValue = ulong.Parse(bracesAndSingleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.Double || column.Type == DbType.Single)
                    {
                        column.DefaultValue = double.Parse(bracesAndSingleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.Boolean)
                    {
                        var truthy = new string[] { "'TRUE'", "1" };
                        var falsy = new string[] { "'FALSE'", "0" };

                        if (truthy.Contains(bracesStrippedString))
                        {
                            column.DefaultValue = true;
                        }
                        else if (falsy.Contains(bracesStrippedString))
                        {
                            column.DefaultValue = false;
                        }
                        else if (bracesStrippedString == "NULL")
                        {
                            column.DefaultValue = null;
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse the boolean default value '{defaultValueString}' of column '{column.Name}'");
                        }
                    }
                    else if (column.Type == DbType.DateTime || column.Type == DbType.DateTime2)
                    {
                        // (CONVERT([datetime],'2000-01-02 03:04:05.000',(121)))
                        // 121 is a pattern: it contains milliseconds
                        // Search for 121 here: https://learn.microsoft.com/de-de/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver17
                        var regexDateTimeConvert121 = new Regex(@"(?<=^\(CONVERT\([\[]+datetime[\]]+,')[^']+(?='\s*,\s*\(121\s*\)\)\)$)");
                        var match121 = regexDateTimeConvert121.Match(defaultValueString);

                        if (match121.Success)
                        {
                            // We convert to UTC since we restrict date time default values to UTC on default value definition.
                            column.DefaultValue = DateTime.ParseExact(match121.Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                        }
                        else if (defaultValueString is string defVal)
                        {
                            // Not tested
                            var dt = defVal;
                            if (defVal.StartsWith("'"))
                            {
                                dt = defVal.Substring(1, defVal.Length - 2);
                            }

                            // We convert to UTC since we restrict date time default values to UTC on default value definition.
                            column.DefaultValue = DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot interpret {column.DefaultValue} in column '{column.Name}' unexpected pattern.");
                        }
                    }
                    else if (column.Type == DbType.Guid)
                    {
                        column.DefaultValue = Guid.Parse(bracesAndSingleQuoteStrippedString);
                    }
                    else if (column.MigratorDbType == MigratorDbType.Decimal)
                    {
                        // We assume ((1.234))
                        column.DefaultValue = decimal.Parse(bracesAndSingleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.MigratorDbType == MigratorDbType.String)
                    {
                        column.DefaultValue = bracesAndSingleQuoteStrippedString;
                    }
                    else if (column.MigratorDbType == MigratorDbType.Binary)
                    {
                        if (bracesStrippedString.StartsWith("0x"))
                        {
                            var hexString = bracesStrippedString.Substring(2);

                            // Not available in old .NET version: Convert.FromHexString(hexString);

                            column.DefaultValue = Enumerable.Range(0, hexString.Length / 2)
                                .Select(x => Convert.ToByte(hexString.Substring(x * 2, 2), 16))
                                .ToArray();
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse the binary default value of '{column.Name}'. The value is '{defaultValueString}'");
                        }
                    }
                    else if (column.MigratorDbType == MigratorDbType.Byte)
                    {
                        column.DefaultValue = byte.Parse(bracesAndSingleQuoteStrippedString);
                    }
                    else
                    {
                        throw new NotImplementedException($"Cannot parse the default value of {column.Name} type '{column.MigratorDbType}'. It is not yet implemented - file an issue.");
                    }
                }
                if (!reader.IsDBNull(5))
                {
                    if (column.Type == DbType.Decimal)
                    {
                        column.Size = reader.GetInt32(5);
                    }
                }

                column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

                columns.Add(column);
            }
        }

        return columns.ToArray();
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SELECT name FROM sys.databases");
    }

    public override void KillDatabaseConnections(string databaseName)
    {
        ExecuteNonQuery(string.Format(
            "USE [master]" + System.Environment.NewLine +
            "ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", databaseName));
    }

    public override void DropDatabases(string databaseName)
    {
        ExecuteNonQuery(string.Format("USE [master]" + System.Environment.NewLine + "DROP DATABASE {0}", databaseName));
    }

    public override void RemoveColumn(string table, string column)
    {
        DeleteColumnConstraints(table, column);
        DeleteColumnIndexes(table, column);
        RemoveColumnDefaultValue(table, column);
        base.RemoveColumn(table, column);
    }

    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        if (!TableExists(tableName))
        {
            throw new MigrationException($"The table '{tableName}' does not exist");
        }

        if (ColumnExists(tableName, newColumnName))
        {
            throw new MigrationException(string.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));
        }

        if (!ColumnExists(tableName, oldColumnName))
        {
            throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", tableName, oldColumnName));
        }

        if (ColumnExists(tableName, oldColumnName))
        {
            ExecuteNonQuery(string.Format("EXEC sp_rename '{0}.{1}', '{2}', 'COLUMN'", tableName, oldColumnName, newColumnName));
        }
    }

    public override void RenameTable(string oldName, string newName)
    {
        if (TableExists(newName))
        {
            throw new MigrationException(string.Format("Table with name '{0}' already exists", newName));
        }

        if (!TableExists(oldName))
        {
            throw new MigrationException(string.Format("Table with name '{0}' does not exist to rename", oldName));
        }

        ExecuteNonQuery(string.Format("EXEC sp_rename '{0}', '{1}'", oldName, newName));
    }

    // Deletes all constraints linked to a column. Sql Server
    // doesn't seems to do this.
    private void DeleteColumnConstraints(string table, string column)
    {
        var sqlContrainte = FindConstraints(table, column);
        var constraints = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, sqlContrainte))
        {
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }
        }
        // Can't share the connection so two phase modif
        foreach (var constraint in constraints)
        {
            RemoveForeignKey(table, constraint);
        }
    }

    private void DeleteColumnIndexes(string table, string column)
    {
        var sqlIndex = this.FindIndexes(table, column);
        var indexes = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, sqlIndex))
        {
            while (reader.Read())
            {
                indexes.Add(reader.GetString(0));
            }
        }
        // Can't share the connection so two phase modif
        foreach (var index in indexes)
        {
            this.RemoveIndex(table, index);
        }
    }

    protected virtual string FindIndexes(string table, string column)
    {
        return string.Format(@"
select
    i.name as IndexName
from sys.indexes i
join sys.objects o on i.object_id = o.object_id
join sys.index_columns ic on ic.object_id = i.object_id
    and ic.index_id = i.index_id
join sys.columns co on co.object_id = i.object_id
    and co.column_id = ic.column_id
where (select count(*) from sys.index_columns ic1 where ic1.object_id = i.object_id and ic1.index_id = i.index_id) = 1
and o.[Name] = '{0}'
and co.[Name] = '{1}'",
                table, column);
    }

    // FIXME: We should look into implementing this with INFORMATION_SCHEMA if possible
    // so that it would be usable by all the SQL Server implementations
    protected virtual string FindConstraints(string table, string column)
    {
        return string.Format(@"SELECT DISTINCT CU.CONSTRAINT_NAME FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE CU
INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
ON CU.CONSTRAINT_NAME = TC.CONSTRAINT_NAME
AND CU.TABLE_NAME = '{0}'
AND CU.COLUMN_NAME = '{1}'",
                table, column);
    }

    public override bool IndexExists(string table, string name)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT top 1 * FROM sys.indexes WHERE object_id = OBJECT_ID('{0}') AND name = '{1}'", table, name));
        return reader.Read();
    }

    public override void RemoveIndex(string table, string name)
    {
        if (TableExists(table) && IndexExists(table, name))
        {
            ExecuteNonQuery(string.Format("DROP INDEX {0} ON {1}", QuoteConstraintNameIfRequired(name), QuoteTableNameIfRequired(table)));
        }
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('{0}') AND is_primary_key = 1", table));
        return reader.Read() ? reader.GetString(0) : null;
    }

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        if (value is ushort)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = value;
        }
        else if (value is uint)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = value;
        }
        else if (value is ulong)
        {
            parameter.DbType = DbType.Decimal;
            parameter.Value = value;
        }
        else
        {
            base.ConfigureParameterWithValue(parameter, index, value);
        }
    }

    public override string Concatenate(params string[] strings)
    {
        return string.Join(" + ", strings);
    }
}
