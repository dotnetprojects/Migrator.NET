using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Models;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Models;
using DotNetProjects.Migrator.Providers.Models;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle;

public class OracleTransformationProvider : TransformationProvider
{
    public const string TemporaryColumnName = "TEMPCOL";

    public OracleTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        CreateConnection(providerName);
    }

    public OracleTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope)
    {
    }

    protected virtual void CreateConnection(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "Oracle.DataAccess.Client";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection(); // new OracleConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }

    public override void DropDatabases(string databaseName)
    {
        if (string.IsNullOrEmpty(databaseName))
        {
            ExecuteNonQuery(string.Format("DROP DATABASE"));
        }
    }

    public override ForeignKeyConstraint[] GetForeignKeyConstraints(string table)
    {
        var constraints = new List<ForeignKeyConstraint>();
        var sb = new StringBuilder();
        sb.AppendLine("SELECT");
        sb.AppendLine("  a.OWNER AS TABLE_SCHEMA,");
        sb.AppendLine("  c.CONSTRAINT_NAME AS FK_KEY,");
        sb.AppendLine("  a.TABLE_NAME AS CHILD_TABLE,");
        sb.AppendLine("  a.COLUMN_NAME AS CHILD_COLUMN,");
        sb.AppendLine("  c_pk.TABLE_NAME AS PARENT_TABLE,");
        sb.AppendLine("  col_pk.COLUMN_NAME AS PARENT_COLUMN");
        sb.AppendLine("FROM ");
        sb.AppendLine("  ALL_CONS_COLUMNS a ");
        sb.AppendLine("JOIN ALL_CONSTRAINTS c");
        sb.AppendLine("  ON a.owner = c.owner AND a.CONSTRAINT_NAME = c.CONSTRAINT_NAME");
        sb.AppendLine("JOIN ALL_CONSTRAINTS c_pk");
        sb.AppendLine("  ON c.R_OWNER = c_pk.OWNER AND c.R_CONSTRAINT_NAME = c_pk.CONSTRAINT_NAME");
        sb.AppendLine("JOIN ALL_CONS_COLUMNS col_pk");
        sb.AppendLine("  ON c_pk.CONSTRAINT_NAME = col_pk.CONSTRAINT_NAME AND c_pk.OWNER = col_pk.OWNER AND a.POSITION = col_pk.POSITION");
        sb.AppendLine($"WHERE LOWER(a.TABLE_NAME) = LOWER('{table}') AND c.CONSTRAINT_TYPE  = 'R'");
        sb.AppendLine("ORDER BY a.POSITION");

        var sql = sb.ToString();
        List<ForeignKeyConstraintItem> foreignKeyConstraintItems = [];

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, sql))
        {
            while (reader.Read())
            {
                var constraintItem = new ForeignKeyConstraintItem
                {
                    SchemaName = reader.GetString(reader.GetOrdinal("TABLE_SCHEMA")),
                    ForeignKeyName = reader.GetString(reader.GetOrdinal("FK_KEY")),
                    ChildTableName = reader.GetString(reader.GetOrdinal("CHILD_TABLE")),
                    ChildColumnName = reader.GetString(reader.GetOrdinal("CHILD_COLUMN")),
                    ParentTableName = reader.GetString(reader.GetOrdinal("PARENT_TABLE")),
                    ParentColumnName = reader.GetString(reader.GetOrdinal("PARENT_COLUMN"))
                };

                foreignKeyConstraintItems.Add(constraintItem);
            }
        }

        var schemaChildTableGroups = foreignKeyConstraintItems.GroupBy(x => new { x.SchemaName, x.ChildTableName }).Count();

        if (schemaChildTableGroups > 1)
        {
            throw new MigrationException($"Duplicates found (grouping by schema name and child table name). Since we do not offer schemas in '{nameof(GetForeignKeyConstraints)}' at this moment in time we cannot filter your target schema. Your database use the same table name in different schemas.");
        }

        var groups = foreignKeyConstraintItems.GroupBy(x => x.ForeignKeyName);

        foreach (var group in groups)
        {
            var first = group.First();

            var foreignKeyConstraint = new ForeignKeyConstraint
            {
                Name = first.ForeignKeyName,
                ParentTable = first.ParentTableName,
                ParentColumns = [.. group.Select(x => x.ParentColumnName).Distinct()],
                ChildTable = first.ChildTableName,
                ChildColumns = [.. group.Select(x => x.ChildColumnName).Distinct()]
            };

            constraints.Add(foreignKeyConstraint);
        }

        return [.. constraints];
    }

    public override void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable,
                                       string[] refColumns, ForeignKeyConstraintType constraint)
    {
        GuardAgainstMaximumIdentifierLengthForOracle(name);

        primaryTable = QuoteTableNameIfRequired(primaryTable);
        refTable = QuoteTableNameIfRequired(refTable);
        var primaryColumnsSql = string.Join(",", primaryColumns.Select(col => QuoteColumnNameIfRequired(col)).ToArray());
        var refColumnsSql = string.Join(",", refColumns.Select(col => QuoteColumnNameIfRequired(col)).ToArray());

        ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} FOREIGN KEY ({2}) REFERENCES {3} ({4})", primaryTable, name, primaryColumnsSql, refTable, refColumnsSql));
    }

    public override string AddIndex(string table, Index index)
    {
        ValidateIndex(tableName: table, index: index);
        var hasFilterItems = index.FilterItems != null && index.FilterItems.Count > 0;

        // Oracle does not support included columns and clustered indexes. We ignore the values given in the properties SILENTLY for backwards compatibility.

        if (index.Unique && hasFilterItems)
        {
            throw new MigrationException($"You cannot use unique together with functional expressions in Oracle ({nameof(FilterItem)}).");
        }

        var name = QuoteConstraintNameIfRequired(index.Name);
        table = QuoteTableNameIfRequired(table);

        List<string> singleFilterStrings = [];


        if (hasFilterItems)
        {
            // In Oracle functional expressions replace the normal columns so we need to remove them
            if (index.KeyColumns != null && index.KeyColumns.Length > 0)
            {
                var keyColumnsList = index.KeyColumns.ToList();

                for (var i = keyColumnsList.Count - 1; i >= 0; i--)
                {
                    if (index.FilterItems.Any(x => keyColumnsList[i].Equals(x.ColumnName, StringComparison.OrdinalIgnoreCase)))
                    {
                        keyColumnsList.RemoveAt(i);
                    }
                }

                index.KeyColumns = keyColumnsList.ToArray();
            }

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

                var singleFilterString = $"CASE WHEN {filterColumnQuoted} {comparisonString} {value} THEN {filterColumnQuoted} ELSE NULL END";

                singleFilterStrings.Add(singleFilterString);
            }
        }

        var mixedColumnNamesAndFilters = QuoteColumnNamesIfRequired(index.KeyColumns).ToList();
        mixedColumnNamesAndFilters.AddRange(singleFilterStrings);
        var columnNamesAndFiltersString = $"({string.Join(", ", mixedColumnNamesAndFilters)})";

        var uniqueString = index.Unique ? "UNIQUE" : null;

        List<string> list = [];
        list.Add("CREATE");
        list.Add(uniqueString);
        list.Add("INDEX");
        list.Add(name);
        list.Add("ON");
        list.Add(table);
        list.Add(columnNamesAndFiltersString);

        list = [.. list.Where(x => !string.IsNullOrWhiteSpace(x))];

        var sql = string.Join(" ", list);

        ExecuteNonQuery(sql);

        return sql;
    }

    private void GuardAgainstMaximumIdentifierLengthForOracle(string name)
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(name);

        if (utf8Bytes.Length > 128)
        {
            throw new MigrationException($"The name '{name}' is {utf8Bytes.Length} bytes in length, but maximum length for Oracle identifiers is 128 bytes for Oracle versions  12.1+.");
        }
    }

    protected override string getPrimaryKeyname(string tableName)
    {
        return tableName.Length > 27 ? "PK_" + tableName.Substring(0, 27) : "PK_" + tableName;
    }

    public override void ChangeColumn(string table, Column column)
    {
        var existingColumn = GetColumnByName(table, column.Name);

        if (column.Type == DbType.String)
        {
            RenameColumn(table, column.Name, TemporaryColumnName);

            // check if this is not-null
            var isNotNull = (column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull;

            // remove the not-null option
            column.ColumnProperty = (column.ColumnProperty & ~ColumnProperty.NotNull);

            AddColumn(table, column);
            CopyDataFromOneColumnToAnother(table, TemporaryColumnName, column.Name);
            RemoveColumn(table, TemporaryColumnName);
            //RenameColumn(table, TemporaryColumnName, column.Name);

            var columnName = QuoteColumnNameIfRequired(column.Name);

            // now set the column to not-null
            if (isNotNull)
            {
                using var cmd = CreateCommand();
                ExecuteQuery(cmd, string.Format("ALTER TABLE {0} MODIFY ({1} NOT NULL)", table, columnName));
            }
        }
        else
        {
            if (((existingColumn.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull)
                && ((column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull))
            {
                // was not null, 	and is being change to not-null - drop the not-null all together
                column.ColumnProperty = column.ColumnProperty & ~ColumnProperty.NotNull;
            }
            else if
                (((existingColumn.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null)
                && ((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null))
            {
                // was null, and is being changed to null - drop the null all together
                column.ColumnProperty = column.ColumnProperty & ~ColumnProperty.Null;
            }

            var mapper = _dialect.GetAndMapColumnProperties(column);

            ChangeColumn(table, mapper.ColumnSql);
        }
    }

    private void CopyDataFromOneColumnToAnother(string table, string fromColumn, string toColumn)
    {
        table = QuoteTableNameIfRequired(table);
        fromColumn = QuoteColumnNameIfRequired(fromColumn);
        toColumn = QuoteColumnNameIfRequired(toColumn);

        ExecuteNonQuery(string.Format("UPDATE {0} SET {1} = {2}", table, toColumn, fromColumn));
    }

    public override void RenameTable(string oldName, string newName)
    {
        GuardAgainstMaximumIdentifierLengthForOracle(newName);
        GuardAgainstExistingTableWithSameName(newName, oldName);

        oldName = QuoteTableNameIfRequired(oldName);
        newName = QuoteTableNameIfRequired(newName);

        ExecuteNonQuery(string.Format("ALTER TABLE {0} RENAME TO {1}", oldName, newName));
    }

    private void GuardAgainstExistingTableWithSameName(string newName, string oldName)
    {
        if (TableExists(newName))
        {
            throw new MigrationException(string.Format("Can not rename table \"{0}\" to \"{1}\", a table with that name already exists", oldName, newName));
        }
    }

    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        GuardAgainstMaximumIdentifierLengthForOracle(newColumnName);
        GuardAgainstExistingColumnWithSameName(newColumnName, tableName);

        tableName = QuoteTableNameIfRequired(tableName);
        oldColumnName = QuoteColumnNameIfRequired(oldColumnName);
        newColumnName = QuoteColumnNameIfRequired(newColumnName);

        ExecuteNonQuery(string.Format("ALTER TABLE {0} RENAME COLUMN {1} TO {2}", tableName, oldColumnName, newColumnName));
    }

    private void GuardAgainstExistingColumnWithSameName(string newColumnName, string tableName)
    {
        if (ColumnExists(tableName, newColumnName))
        {
            throw new MigrationException(string.Format("A column with the name \"{0}\" already exists in the table \"{1}\"", newColumnName, tableName));
        }
    }

    public override void ChangeColumn(string table, string sqlColumn)
    {
        if (string.IsNullOrEmpty(table))
        {
            throw new ArgumentNullException("table");
        }

        if (string.IsNullOrEmpty(table))
        {
            throw new ArgumentNullException("sqlColumn");
        }

        table = QuoteTableNameIfRequired(table);
        sqlColumn = QuoteColumnNameIfRequired(sqlColumn);
        ExecuteNonQuery(string.Format("ALTER TABLE {0} MODIFY {1}", table, sqlColumn));
    }

    public override void AddColumn(string table, string sqlColumn)
    {
        GuardAgainstMaximumIdentifierLengthForOracle(table);
        table = QuoteTableNameIfRequired(table);
        sqlColumn = QuoteColumnNameIfRequired(sqlColumn);
        ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
    }

    public override string[] GetConstraints(string table)
    {
        var constraints = new List<string>();
        using (var cmd = CreateCommand())
        using (
            var reader =
                ExecuteQuery(cmd,
                    string.Format("SELECT constraint_name FROM user_constraints WHERE lower(table_name) = '{0}'", table.ToLower())))
        {
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }
        }

        return constraints.ToArray();
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        var constraints = new List<string>();

        using (var cmd = CreateCommand())
        using (
            var reader =
                ExecuteQuery(cmd,
                    string.Format("SELECT constraint_name FROM user_constraints WHERE lower(table_name) = '{0}' and constraint_type = 'P'", table.ToLower())))
        {
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }
        }

        return constraints.FirstOrDefault();
    }

    public override bool ConstraintExists(string table, string name)
    {
        var sql =
            string.Format(
                "SELECT COUNT(constraint_name) FROM user_constraints WHERE lower(constraint_name) = '{0}' AND lower(table_name) = '{1}'",
                name.ToLower(), table.ToLower());
        Logger.Log(sql);
        var scalar = ExecuteScalar(sql);
        return Convert.ToInt32(scalar) == 1;
    }

    public override bool ColumnExists(string table, string column)
    {
        if (!TableExists(table))
        {
            return false;
        }

        var sql =
            string.Format(
                "SELECT COUNT(column_name) FROM user_tab_columns WHERE lower(table_name) = '{0}' AND lower(column_name) = '{1}'",
                table.ToLower(), column.ToLower());
        Logger.Log(sql);
        var scalar = ExecuteScalar(sql);
        return Convert.ToInt32(scalar) == 1;
    }

    public override bool TableExists(string table)
    {
        var sql = string.Format("SELECT COUNT(table_name) FROM user_tables WHERE lower(table_name) = '{0}'", table.ToLower());

        if (_defaultSchema != null)
        {
            sql = string.Format("SELECT COUNT(table_name) FROM user_tables WHERE lower(owner) = '{0}' and lower(table_name) = '{1}'", _defaultSchema.ToLower(), table.ToLower());
        }

        Logger.Log(sql);
        var count = ExecuteScalar(sql);
        return Convert.ToInt32(count) == 1;
    }

    public override bool ViewExists(string view)
    {
        var sql = string.Format("SELECT COUNT(view_name) FROM user_views WHERE lower(view_name) = '{0}'", view.ToLower());

        if (_defaultSchema != null)
        {
            sql = string.Format("SELECT COUNT(view_name) FROM user_views WHERE lower(owner) = '{0}' and lower(view_name) = '{1}'", _defaultSchema.ToLower(), view.ToLower());
        }

        Logger.Log(sql);
        var count = ExecuteScalar(sql);
        return Convert.ToInt32(count) == 1;
    }

    public override List<string> GetDatabases()
    {
        throw new NotImplementedException();
    }

    public override string[] GetTables()
    {
        var tables = new List<string>();

        using (var cmd = CreateCommand())
        using (var reader =
            ExecuteQuery(cmd, "SELECT table_name FROM user_tables"))
        {
            while (reader.Read())
            {
                tables.Add(reader[0].ToString());
            }
        }

        return tables.ToArray();
    }

    public override Column[] GetColumns(string table)
    {
        var timestampRegex = new Regex(@"(?<=^TIMESTAMP\s+')[^']+(?=')", RegexOptions.IgnoreCase);
        var hexToRawRegex = new Regex(@"(?<=^HEXTORAW\s*\(')[^']+(?=')", RegexOptions.IgnoreCase);
        var timestampBaseFormat = "yyyy-MM-dd HH:mm:ss";

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("SELECT");
        stringBuilder.AppendLine("  COLUMN_NAME,");
        stringBuilder.AppendLine("  NULLABLE,");
        stringBuilder.AppendLine("  DATA_DEFAULT,");
        stringBuilder.AppendLine("  DATA_TYPE,");
        stringBuilder.AppendLine("  DATA_LENGTH,");
        stringBuilder.AppendLine("  DATA_PRECISION,");
        stringBuilder.AppendLine("  DATA_SCALE,");
        stringBuilder.AppendLine("  CHAR_COL_DECL_LENGTH");
        stringBuilder.AppendLine($"FROM USER_TAB_COLUMNS WHERE LOWER(TABLE_NAME) = LOWER('{table}')");

        var stringBuilder2 = new StringBuilder();
        stringBuilder2.AppendLine("SELECT x.column_name, x.data_default");
        stringBuilder2.AppendLine("FROM XMLTABLE(");
        stringBuilder2.AppendLine("   '/ROWSET/ROW'");
        stringBuilder2.AppendLine("   PASSING DBMS_XMLGEN.GETXMLTYPE(");
        stringBuilder2.AppendLine($"      'SELECT column_name, data_default FROM user_tab_columns WHERE table_name = ''{table.ToUpperInvariant()}'''");
        stringBuilder2.AppendLine("   )");
        stringBuilder2.AppendLine("   COLUMNS");
        stringBuilder2.AppendLine("      column_name VARCHAR2(4000) PATH 'COLUMN_NAME',");
        stringBuilder2.AppendLine("      data_default VARCHAR2(4000) PATH 'DATA_DEFAULT'");
        stringBuilder2.AppendLine(") x");

        List<UserTabColumns> userTabColumns = [];

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, stringBuilder2.ToString()))
        {
            while (reader.Read())
            {
                var columnNameOrdinal = reader.GetOrdinal("COLUMN_NAME");
                var dataDefaultOrdinal = reader.GetOrdinal("DATA_DEFAULT");

                var userTabColumnsItem = new UserTabColumns
                {
                    ColumnName = reader.IsDBNull(columnNameOrdinal) ? null : reader.GetString(columnNameOrdinal),
                    DataDefault = reader.IsDBNull(dataDefaultOrdinal) ? null : reader.GetString(dataDefaultOrdinal).Trim()
                };

                userTabColumns.Add(userTabColumnsItem);
            }
        }

        var columns = new List<Column>();

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, stringBuilder.ToString()))
        {
            while (reader.Read())
            {
                var columnNameOrdinal = reader.GetOrdinal("COLUMN_NAME");
                var nullableOrdinal = reader.GetOrdinal("NULLABLE");
                var dataTypeOrdinal = reader.GetOrdinal("DATA_TYPE");
                var dataLengthOrdinal = reader.GetOrdinal("DATA_LENGTH");
                var dataPrecisionOrdinal = reader.GetOrdinal("DATA_PRECISION");
                var dataScaleOrdinal = reader.GetOrdinal("DATA_SCALE");
                var charColDeclLengthOrdinal = reader.GetOrdinal("CHAR_COL_DECL_LENGTH");

                var columnName = reader.GetString(columnNameOrdinal);
                var isNullable = reader.GetString(nullableOrdinal) == "Y";
                var dataTypeString = reader.GetString(dataTypeOrdinal).ToUpperInvariant();
                var dataLength = reader.IsDBNull(dataLengthOrdinal) ? (int?)null : reader.GetInt32(dataLengthOrdinal);
                var dataPrecision = reader.IsDBNull(dataPrecisionOrdinal) ? (int?)null : reader.GetInt32(dataPrecisionOrdinal);
                var dataScale = reader.IsDBNull(dataScaleOrdinal) ? (int?)null : reader.GetInt32(dataScaleOrdinal);
                var charColDeclLength = reader.IsDBNull(charColDeclLengthOrdinal) ? (int?)null : reader.GetInt32(charColDeclLengthOrdinal);
                var dataDefaultString = userTabColumns.FirstOrDefault(x => x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))?.DataDefault;

                var column = new Column(columnName, DbType.String)
                {
                    ColumnProperty = isNullable ? ColumnProperty.Null : ColumnProperty.NotNull
                };

                // Oracle does not have unsigned types. All NUMBER types can hold positive or negative values so we do not return DbType.UIntX types.
                if (dataTypeString.StartsWith("NUMBER") || dataTypeString.StartsWith("FLOAT"))
                {
                    column.Precision = dataPrecision;

                    if (dataScale > 0)
                    {
                        // Could also be Double
                        column.MigratorDbType = MigratorDbType.Decimal;
                        column.Scale = dataScale;
                    }
                    else
                    {
                        if (dataPrecision.HasValue && dataPrecision == 1)
                        {
                            column.MigratorDbType = MigratorDbType.Boolean;
                        }
                        else if (dataPrecision.HasValue && (dataPrecision == 0 || (2 <= dataPrecision && dataPrecision <= 5)))
                        {
                            column.MigratorDbType = MigratorDbType.Int16;
                        }
                        else if (dataPrecision.HasValue && 6 <= dataPrecision && dataPrecision <= 10)
                        {
                            column.MigratorDbType = MigratorDbType.Int32;
                        }
                        else if (dataPrecision == null || 11 <= dataPrecision)
                        {
                            // Oracle allows up to 38 digits but in C# the maximum is Int64 and in Oracle there is no unsigned data type.
                            column.MigratorDbType = MigratorDbType.Int64;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                else if (dataTypeString.StartsWith("TIMESTAMP"))
                {
                    var timestampNumberRegex = new Regex(@"(?<=^Timestamp\()[\d]+(?=\)$)", RegexOptions.IgnoreCase);
                    var timestampNumberMatch = timestampNumberRegex.Match(dataTypeString);

                    if (timestampNumberMatch.Success)
                    {
                        // n in TIMESTAMP(n) is not retrievable using system tables so we need to extract it via regex.
                        column.Precision = int.Parse(timestampNumberMatch.Value);
                        column.MigratorDbType = column.Precision < 3 ? MigratorDbType.DateTime : MigratorDbType.DateTime2;
                    }
                    else
                    {
                        // 6 is the standard if we use TIMESTAMP without n like in TIMESTAMP(n)
                        column.Precision = 6;
                        column.MigratorDbType = MigratorDbType.DateTime2;
                    }
                }
                else if (dataTypeString == "DATE")
                {
                    column.MigratorDbType = MigratorDbType.Date;
                }
                else if (dataTypeString == "RAW" && dataLength == 16)
                {
                    // ambiguity - cannot distinguish between guid and binary
                    column.MigratorDbType = MigratorDbType.Guid;
                }
                else if (dataTypeString.StartsWith("RAW") || dataTypeString == "BLOB")
                {
                    column.MigratorDbType = MigratorDbType.Binary;
                }
                else if (dataTypeString == "NVARCHAR2")
                {
                    column.MigratorDbType = MigratorDbType.String;
                }
                else if (dataTypeString == "BINARY_FLOAT")
                {
                    column.MigratorDbType = MigratorDbType.Single;
                }
                else if (dataTypeString == "BINARY_DOUBLE")
                {
                    column.MigratorDbType = MigratorDbType.Double;
                }
                else if (dataTypeString == "BOOLEAN")
                {
                    column.MigratorDbType = MigratorDbType.Boolean;
                }
                else if (dataTypeString == "NCLOB")
                {
                    column.MigratorDbType = MigratorDbType.String;
                }
                else if (dataTypeString.StartsWith("INTERVAL"))
                {
                    column.MigratorDbType = MigratorDbType.Interval;
                }
                else
                {
                    throw new NotImplementedException($"The data type '{dataTypeString}' is not implemented yet. Please file an issue.");
                }

                if (!string.IsNullOrWhiteSpace(dataDefaultString))
                {
                    // This is only necessary because older versions of this migrator added single quotes for numerics.
                    var singleQuoteStrippedString = dataDefaultString.Replace("'", "");

                    if (column.Type == DbType.Int16 || column.Type == DbType.Int32 || column.Type == DbType.Int64)
                    {
                        column.DefaultValue = long.Parse(singleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.Double)
                    {
                        column.DefaultValue = double.Parse(singleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.Single)
                    {
                        column.DefaultValue = float.Parse(singleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.Decimal)
                    {
                        column.DefaultValue = decimal.Parse(singleQuoteStrippedString, CultureInfo.InvariantCulture);
                    }
                    else if (column.Type == DbType.Boolean)
                    {
                        column.DefaultValue = dataDefaultString == "1" || dataDefaultString.ToUpper() == "TRUE";
                    }
                    else if (column.Type == DbType.DateTime || column.Type == DbType.DateTime2)
                    {
                        if (dataDefaultString.StartsWith("TO_TIMESTAMP("))
                        {
                            var expectedOracleToTimestampPattern = "YYYY-MM-DD HH24:MI:SS";

                            if (!dataDefaultString.Contains(expectedOracleToTimestampPattern))
                            {
                                throw new NotSupportedException($"Not supported 'TO_TIMESTAMP' pattern. Expected pattern: {expectedOracleToTimestampPattern}");
                            }

                            var toTimestampRegex = new Regex(@"(?<=^TO_TIMESTAMP\(')[^']+(?=')", RegexOptions.IgnoreCase);
                            var toTimestampMatch = toTimestampRegex.Match(dataDefaultString);
                            var toTimestampDateTimeString = toTimestampMatch.Value;

                            List<string> formats = [];

                            // add formats with .F, .FF, .FFF etc.
                            formats = Enumerable.Range(0, 20).Select((x, y) => $"{timestampBaseFormat}.{new string('F', y + 1)}").ToList();
                            formats.Add(timestampBaseFormat);

                            column.DefaultValue = DateTime.ParseExact(toTimestampDateTimeString, [.. formats], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                        }
                        else if (timestampRegex.Match(dataDefaultString) is Match timestampMatch && timestampMatch.Success)
                        {
                            var millisecondsPattern = column.Size == 0 ? string.Empty : $".{new string('F', column.Size)}";
                            column.DefaultValue = DateTime.ParseExact(timestampMatch.Value, $"yyyy-MM-dd HH:mm:ss{millisecondsPattern}", CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            // Could be system time in many variants
                            column.DefaultValue = dataDefaultString;
                        }
                    }
                    else if (column.Type == DbType.Guid)
                    {
                        if (hexToRawRegex.Match(dataDefaultString) is Match hexToRawMatch && hexToRawMatch.Success)
                        {
                            var bytes = Enumerable.Range(0, hexToRawMatch.Value.Length / 2)
                                .Select(x => Convert.ToByte(hexToRawMatch.Value.Substring(x * 2, 2), 16))
                                .ToArray();

                            // Oracle uses Big-Endian
                            Array.Reverse(bytes, 0, 4);
                            Array.Reverse(bytes, 4, 2);
                            Array.Reverse(bytes, 6, 2);

                            column.DefaultValue = new Guid(bytes);
                        }
                        else if (dataDefaultString.StartsWith("'"))
                        {
                            var guidString = dataDefaultString.Substring(1, dataDefaultString.Length - 2);

                            column.DefaultValue = Guid.Parse(guidString);
                        }
                        else
                        {
                            column.DefaultValue = dataDefaultString;
                        }
                    }
                    else if (column.Type == DbType.String)
                    {
                        var contentRegex = new Regex(@"(?<=^').*(?='$)");

                        if (contentRegex.Match(dataDefaultString) is Match contentMatch && contentMatch.Success)
                        {
                            column.DefaultValue = contentMatch.Value;
                        }
                        else
                        {
                            throw new Exception($"Cannot parse string column '{column.Name}'");
                        }
                    }
                    else if (column.Type == DbType.Binary)
                    {
                        if (hexToRawRegex.Match(dataDefaultString) is Match hexToRawMatch && hexToRawMatch.Success)
                        {
                            column.DefaultValue = Enumerable.Range(0, hexToRawMatch.Value.Length / 2)
                                .Select(x => Convert.ToByte(hexToRawMatch.Value.Substring(x * 2, 2), 16))
                                .ToArray();
                        }
                        else
                        {
                            throw new NotImplementedException($"Cannot parse default value in column '{column.Name}'");
                        }
                    }
                    else
                    {
                        column.DefaultValue = dataDefaultString;
                    }
                }

                columns.Add(column);
            }
        }

        return columns.ToArray();
    }

    public override string GenerateParameterNameParameter(int index)
    {
        return "p" + index;
    }

    public override string GenerateParameterName(int index)
    {
        return ":p" + index;
    }

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        if (value is Guid || value is Guid?)
        {
            parameter.DbType = DbType.Binary;

            if (value is Guid? && !((Guid?)value).HasValue)
            {
                return;
            }

            parameter.Value = ((Guid)value).ToByteArray();
        }
        else if (value is bool || value is bool?)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = ((bool)value) ? 1 : 0;
        }
        else if (value is ushort)
        {
            parameter.DbType = DbType.Decimal;
            parameter.Value = value;
        }
        else if (value is uint)
        {
            parameter.DbType = DbType.Decimal;
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

    public override void RemoveColumnDefaultValue(string table, string column)
    {
        var sql = string.Format("ALTER TABLE {0} MODIFY {1} DEFAULT NULL", table, column);
        ExecuteNonQuery(sql);
    }

    public override void AddTable(string name, params IDbField[] fields)
    {
        GuardAgainstMaximumIdentifierLengthForOracle(name);

        var columns = fields.Where(x => x is Column).Cast<Column>().ToArray();

        GuardAgainstMaximumColumnNameLengthForOracle(name, columns);

        base.AddTable(name, fields);

        // Should be refactored
        if (columns.Any(c => c.ColumnProperty == ColumnProperty.PrimaryKeyWithIdentity ||
            (c.ColumnProperty.HasFlag(ColumnProperty.Identity) && c.ColumnProperty.HasFlag(ColumnProperty.PrimaryKey))))
        {
            var identityColumn = columns.First(c => c.ColumnProperty == ColumnProperty.PrimaryKeyWithIdentity ||
                (c.ColumnProperty.HasFlag(ColumnProperty.Identity) && c.ColumnProperty.HasFlag(ColumnProperty.PrimaryKey)));

            var seqTName = name.Length > 21 ? name.Substring(0, 21) : name;
            if (seqTName.EndsWith("_"))
            {
                seqTName = seqTName.Substring(0, seqTName.Length - 1);
            }

            // Create a sequence for the table
            using (var cmd = CreateCommand())
            {
                ExecuteQuery(cmd, string.Format("CREATE SEQUENCE {0}_SEQUENCE NOCACHE", seqTName));
            }

            // Create identity trigger (This all has to be in one line (no whitespace), I learned the hard way :) )
            using (var cmd = CreateCommand())
            {
                ExecuteQuery(cmd, string.Format(
                @"CREATE OR REPLACE TRIGGER {0}_TRIGGER BEFORE INSERT ON {1} FOR EACH ROW BEGIN SELECT {0}_SEQUENCE.NEXTVAL INTO :NEW.{2} FROM DUAL; END;", seqTName, name, identityColumn.Name));
            }
        }
    }
    public override void RemoveTable(string name)
    {
        base.RemoveTable(name);
        try
        {
            using var cmd = CreateCommand();
            ExecuteQuery(cmd, string.Format(@"DROP SEQUENCE {0}_SEQUENCE", name));
        }
        catch (Exception)
        {
            // swallow this because sequence may not have originally existed.
        }
    }
    private void GuardAgainstMaximumColumnNameLengthForOracle(string name, Column[] columns)
    {
        foreach (var column in columns)
        {
            if (column.Name.Length > 30)
            {
                throw new ArgumentException(
                    string.Format("When adding table: \"{0}\", the column: \"{1}\", the name of the column is: {2} characters in length, but maximum length for an oracle identifier is 30 characters", name,
                                  column.Name, column.Name.Length), "columns");
            }
        }
    }

    public override string Encode(Guid guid)
    {
        var bytes = guid.ToByteArray();
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            hex.AppendFormat("{0:X2}", b);
        }

        return hex.ToString();
    }

    public override bool IndexExists(string table, string name)
    {
        var sql =
            string.Format(
                "SELECT COUNT(index_name) FROM user_indexes WHERE lower(index_name) = '{0}' AND lower(table_name) = '{1}'",
                name.ToLower(), table.ToLower());
        Logger.Log(sql);
        var scalar = ExecuteScalar(sql);
        return Convert.ToInt32(scalar) == 1;
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

        var conditionStrings = conditionColumnPairs.Select(x => $"t.{QuoteColumnNameIfRequired(x.ColumnNameTarget)} = s.{QuoteColumnNameIfRequired(x.ColumnNameSource)}");

        var assignStrings = fromSourceToTargetColumnPairs.Select(x => $"{QuoteColumnNameIfRequired(x.ColumnNameTarget)} = s.{QuoteColumnNameIfRequired(x.ColumnNameSource)}").ToList();

        var conditionStringsJoined = string.Join(" AND ", conditionStrings);
        var assignStringsJoined = string.Join(", ", assignStrings);

        var sql = $"MERGE INTO {tableNameTarget} t USING {tableNameSource} s ON ({conditionStringsJoined}) WHEN MATCHED THEN UPDATE SET {assignStringsJoined}";
        ExecuteNonQuery(sql);
    }

    private string SchemaInfoTableName
    {
        get
        {
            if (_defaultSchema == null)
            {
                return "SchemaInfo";
            }

            return string.Format("{0}.{1}", _defaultSchema, "SchemaInfo");
        }
    }

    public override Index[] GetIndexes(string table)
    {
        var sql = @$"SELECT
                        i.table_name,
                        i.index_name,
                        i.uniqueness,
                        ic.column_position,
                        ic.column_name,
                        CASE WHEN c.constraint_type = 'P' THEN 'YES' ELSE 'NO' END AS is_primary_key,
                        CASE WHEN c.constraint_type = 'U' THEN 'YES' ELSE 'NO' END AS is_unique_key
                    FROM
                        user_indexes i
                        JOIN 
                            user_ind_columns ic ON i.index_name = ic.index_name AND 
                            i.table_name = ic.table_name
                        LEFT JOIN
                            user_constraints c ON i.index_name = c.index_name AND
                            i.table_name = c.table_name
                    WHERE
                        UPPER(i.table_name) = '{table.ToUpperInvariant()}' 
                       -- AND
                       -- i.index_type = 'NORMAL'
                    ORDER BY
                        i.table_name, i.index_name, ic.column_position";

        List<IndexItem> indexItems = [];

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, sql))
        {
            while (reader.Read())
            {
                var tableNameOrdinal = reader.GetOrdinal("table_name");
                var indexNameOrdinal = reader.GetOrdinal("index_name");
                var uniquenessOrdinal = reader.GetOrdinal("uniqueness");
                var columnPositionOrdinal = reader.GetOrdinal("column_position");
                var columnNameOrdinal = reader.GetOrdinal("column_name");
                var isPrimaryKeyOrdinal = reader.GetOrdinal("is_primary_key");
                var isUniqueConstraintOrdinal = reader.GetOrdinal("is_unique_key");

                var indexItem = new IndexItem
                {
                    ColumnName = reader.GetString(columnNameOrdinal),
                    ColumnOrder = reader.GetInt32(columnPositionOrdinal),
                    Name = reader.GetString(indexNameOrdinal),
                    PrimaryKey = reader.GetString(isPrimaryKeyOrdinal) == "YES",
                    TableName = reader.GetString(tableNameOrdinal),
                    Unique = reader.GetString(uniquenessOrdinal) == "UNIQUE",
                    UniqueConstraint = reader.GetString(isUniqueConstraintOrdinal) == "YES"
                };

                indexItems.Add(indexItem);
            }
        }

        var indexGroups = indexItems.GroupBy(x => new { x.SchemaName, x.TableName, x.Name });
        List<Index> indexes = [];

        foreach (var indexGroup in indexGroups)
        {
            var first = indexGroup.First();

            var index = new Index
            {
                KeyColumns = [.. indexGroup.OrderBy(x => x.ColumnOrder).Select(x => x.ColumnName).Distinct()],
                Name = first.Name,
                PrimaryKey = first.PrimaryKey,
                UniqueConstraint = first.UniqueConstraint,
                Unique = first.Unique,

                // Oracle does not support clustered indexes at this point in time.
                Clustered = false,

                // Oracle does not support include columns at this point in time.
                IncludeColumns = null,
            };

            // FilterItems is not supported in this migrator at this point in time.

            indexes.Add(index);
        }

        return indexes.ToArray();
    }

    public override string Concatenate(params string[] strings)
    {
        return string.Join(" || ", strings);
    }
}
