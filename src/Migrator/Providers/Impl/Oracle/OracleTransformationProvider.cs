using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Models;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Data;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Data.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Models;
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

public class OracleTransformationProvider : TransformationProvider, IOracleTransformationProvider
{
    private IOracleSystemDataLoader _oracleSystemDataLoader;

    public const string TemporaryColumnName = "TEMPCOL";

    public OracleTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        CreateConnection(providerName);
        Initialize();
    }

    public OracleTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope)
    {
        Initialize();
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

    public override ForeignKeyConstraint[] GetForeignKeyConstraints(string table) =>
        ForeignKeyMetadataReader.Read(this, table);

    public override void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable,
                                       string[] refColumns, ForeignKeyConstraintType constraint)
    {
        GuardAgainstMaximumIdentifierLengthForOracle(name);

        AddForeignKey(name, primaryTable, primaryColumns, refTable, refColumns, constraint, ForeignKeyConstraintType.NoAction);
    }

    public override string AddIndex(string table, Index index)
    {
        ValidateIndex(tableName: table, index: index);
        var hasFilterItems = index.FilterItems != null && index.FilterItems.Count > 0;

        if (index.IncludeColumns?.Length > 0 || index.Clustered)
            throw new NotSupportedException("Oracle does not support included columns or SQL Server-style clustered indexes. Use an explicit Oracle operation.");

        if (index.Unique && hasFilterItems)
        {
            throw new MigrationException($"You cannot use unique together with functional expressions in Oracle ({nameof(FilterItem)}).");
        }

        var relation = CatalogRelation(table, true);
        var name = (relation.Schema == null ? "" : _dialect.QuoteIdentifier(relation.Schema) + ".") + QuoteConstraintNameIfRequired(index.Name);
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
                    string stringValue => $"'{stringValue.Replace("'", "''")}'",
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
            throw new MigrationException($"The name '{name}' is {utf8Bytes.Length} bytes in length, but maximum length for Oracle identifiers is 128 bytes for Oracle versions 12.1+.");
        }
    }

    protected override string GetPrimaryKeyname(string tableName)
    {
        return tableName.Length > 27 ? "PK_" + tableName.Substring(0, 27) : "PK_" + tableName;
    }

    public override void ChangeColumn(string table, Column column)
    {
        var existing = GetColumnByName(table, column.Name);
        var definition = column.CopyDefinition();
        if (definition.DefaultValue == null) RemoveColumnDefaultValue(table, definition.Name);
        // Oracle rejects restating an existing NOT NULL constraint. Render type/default
        // separately and change nullability only when its value actually changes.
        definition.IsNullable = true;
        var mapper = _dialect.GetAndMapColumnProperties(definition);
        var sql = mapper.ColumnSql;
        if (sql.EndsWith(" NULL", StringComparison.Ordinal)) sql = sql[..^5];
        if (existing.IsNullable != column.IsNullable)
            sql += column.IsNullable ? " NULL" : " NOT NULL";
        ChangeColumn(table, sql);
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
        var oldRelation = SqlIdentifier.Catalog(QuoteTableNameIfRequired(oldName), true);
        var newRelation = SqlIdentifier.Catalog(_dialect.QuoteTableNameIfRequired(newName), true);
        if (newRelation.Schema != null && newRelation.Schema != oldRelation.Schema)
            throw new NotSupportedException("Oracle RENAME does not move a table between schemas.");
        GuardAgainstMaximumIdentifierLengthForOracle(newRelation.Name);
        var target = (oldRelation.Schema == null ? "" : _dialect.QuoteIdentifier(oldRelation.Schema) + ".") + _dialect.QuoteIdentifier(newRelation.Name);
        GuardAgainstExistingTableWithSameName(target, oldName);
        oldName = QuoteTableNameIfRequired(oldName);
        newName = _dialect.QuoteIdentifier(newRelation.Name);

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
            throw new ArgumentNullException(nameof(table));
        }

        if (string.IsNullOrEmpty(table))
        {
            throw new ArgumentNullException(nameof(sqlColumn));
        }

        table = QuoteTableNameIfRequired(table);

        ExecuteNonQuery(string.Format("ALTER TABLE {0} MODIFY ({1})", table, sqlColumn));
    }

    public override void AddColumn(string table, string sqlColumn)
    {
        foreach (var part in SqlIdentifier.Parse(table)) GuardAgainstMaximumIdentifierLengthForOracle(part.Value);
        table = QuoteTableNameIfRequired(table);

        ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
    }

    public override string[] GetConstraints(string table) => ExecuteStringQuery(
        "SELECT CONSTRAINT_NAME FROM ALL_CONSTRAINTS WHERE " + OracleCatalog.Predicate(this, table)).ToArray();

    protected override string GetPrimaryKeyConstraintName(string table) => ExecuteStringQuery(
        "SELECT CONSTRAINT_NAME FROM ALL_CONSTRAINTS WHERE CONSTRAINT_TYPE='P' AND " + OracleCatalog.Predicate(this, table)).FirstOrDefault();

    public override bool ConstraintExists(string table, string name) =>
        GetConstraints(table).Any(actual => actual == name || actual == name.ToUpperInvariant());

    public override bool ColumnExists(string table, string column) => Convert.ToInt32(ExecuteScalar(
        "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE " + OracleCatalog.Predicate(this, table) +
        " AND COLUMN_NAME=" + OracleCatalog.Literal(SqlIdentifier.Catalog(QuoteColumnNameIfRequired(column), true).Name))) > 0;

    public override bool TableExists(string table) => Convert.ToInt32(ExecuteScalar(
        "SELECT COUNT(*) FROM ALL_TABLES WHERE " + OracleCatalog.Predicate(this, table))) > 0;

    public override bool ViewExists(string view) => Convert.ToInt32(ExecuteScalar(
        "SELECT COUNT(*) FROM ALL_VIEWS WHERE " + OracleCatalog.Predicate(this, view, "VIEW_NAME"))) > 0;

    public override List<string> GetDatabases()
    {
        throw new NotImplementedException();
    }

    public override string[] GetTables() => base.GetTables();

    public override Column[] GetColumns(string table)
    {
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
        stringBuilder.AppendLine("FROM ALL_TAB_COLUMNS WHERE " + OracleCatalog.Predicate(this, table) + " ORDER BY COLUMN_ID");

        var stringBuilder2 = new StringBuilder();
        stringBuilder2.AppendLine("SELECT x.column_name, x.data_default");
        stringBuilder2.AppendLine("FROM XMLTABLE(");
        stringBuilder2.AppendLine("   '/ROWSET/ROW'");
        stringBuilder2.AppendLine("   PASSING DBMS_XMLGEN.GETXMLTYPE(");
        var defaultQuery = "SELECT column_name, data_default FROM all_tab_columns WHERE " + OracleCatalog.Predicate(this, table);
        stringBuilder2.AppendLine("      " + OracleCatalog.Literal(defaultQuery));
        stringBuilder2.AppendLine("   )");
        stringBuilder2.AppendLine("   COLUMNS");
        stringBuilder2.AppendLine("      column_name VARCHAR2(4000) PATH 'COLUMN_NAME',");
        stringBuilder2.AppendLine("      data_default VARCHAR2(4000) PATH 'DATA_DEFAULT'");
        stringBuilder2.AppendLine(") x");

        var userTabIdentityCols = _oracleSystemDataLoader.GetUserTabIdentityCols(tableName: table);
        var primaryKeyItems = _oracleSystemDataLoader.GetPrimaryKeyItems(tableName: table);


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
                    IsNullable = isNullable
                };

                var isIdentity = userTabIdentityCols.Any(x => x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                var isPrimaryKey = primaryKeyItems.Any(x => x.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                if (isIdentity && isPrimaryKey)
                {
                    column.IsIdentity = true;
                }
                else if (isIdentity)
                {
                    column.IsIdentity = true;
                }
                else if (isPrimaryKey)
                {

                }

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
                else if (dataTypeString == "VARCHAR2" || dataTypeString == "CLOB")
                {
                    column.MigratorDbType = MigratorDbType.AnsiString;
                }
                else if (dataTypeString == "CHAR")
                {
                    column.MigratorDbType = MigratorDbType.AnsiStringFixedLength;
                }
                else if (dataTypeString == "NCHAR")
                {
                    column.MigratorDbType = MigratorDbType.StringFixedLength;
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

                if (dataTypeString is "CLOB" or "NCLOB" or "BLOB") column.Size = int.MaxValue;
                else if (dataTypeString is "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR")
                    column.Size = charColDeclLength ?? dataLength ?? 0;
                else if (dataTypeString == "RAW") column.Size = dataLength ?? 0;

                OracleColumnDefault.Apply(column, dataDefaultString);

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
        if (value is float single)
        {
            base.ConfigureParameterWithValue(parameter, index, value);
            // ODP.NET maps DbType.Single to decimal FLOAT, rounding to seven
            // decimal digits. Select its native IEEE type without coupling the
            // provider assembly to either managed or unmanaged ODP.NET.
            var oracleType = parameter.GetType().GetProperty("OracleDbType");
            if (oracleType?.CanWrite == true && oracleType.PropertyType.IsEnum &&
                Enum.IsDefined(oracleType.PropertyType, "BinaryFloat"))
                oracleType.SetValue(parameter, Enum.Parse(oracleType.PropertyType, "BinaryFloat"));
            else
            {
                parameter.DbType = DbType.Double;
                parameter.Value = (double)single;
            }
        }
        else if (value is TimeOnly time)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = OracleDialect.TimeValue(time);
        }
        else if (value is TimeSpan interval)
        {
            // ODP.NET infers IntervalDS from a TimeSpan value.
            parameter.Value = interval;
        }
        else if (value is Guid || value is Guid?)
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
        var sql = string.Format("ALTER TABLE {0} MODIFY ({1} DEFAULT NULL)", QuoteTableNameIfRequired(table), QuoteColumnNameIfRequired(column));
        ExecuteNonQuery(sql);
    }

    public override void AddTable(string name, params IDbField[] fields)
    {
        foreach (var part in SqlIdentifier.Parse(name)) GuardAgainstMaximumIdentifierLengthForOracle(part.Value);
        var columns = fields.OfType<Column>().ToArray();
        GuardAgainstMaximumColumnNameLengthForOracle(name, columns);
        foreach (var identity in columns.Where(c => c.IsIdentity))
            if (identity.Type is not (DbType.Int16 or DbType.Int32 or DbType.Int64 or DbType.UInt16 or DbType.UInt32 or DbType.UInt64))
                throw new MigrationException("Oracle identity columns require an integer type.");
        base.AddTable(name, fields);
    }

    public override void RemoveTable(string name)
    {
        // Oracle drops table-owned triggers and native identity sequences itself.
        // A legacy-looking sequence name is not evidence of ownership.
        base.RemoveTable(name);
    }

    /// <summary>Drop a table and explicitly identified, unquoted legacy sequence names.
    /// The caller must own these sequences. Oracle DDL is not transactional.</summary>
    public void RemoveTableWithOwnedSequences(string name, params string[] ownedSequenceNames)
    {
        ArgumentNullException.ThrowIfNull(ownedSequenceNames);
        var sequences = ownedSequenceNames.Select(sequence =>
        {
            GuardAgainstMaximumIdentifierLengthForOracle(sequence);
            if (!System.Text.RegularExpressions.Regex.IsMatch(sequence, @"^[A-Za-z][A-Za-z0-9_$#]*$"))
                throw new ArgumentException("Legacy sequence cleanup requires simple unquoted sequence names.", nameof(ownedSequenceNames));
            return sequence.ToUpperInvariant();
        }).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var sequence in sequences)
        {
            using var command = CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = :sequenceName";
            var parameter = command.CreateParameter(); parameter.ParameterName = "sequenceName"; parameter.Value = sequence;
            command.Parameters.Add(parameter);
            if (Convert.ToInt32(command.ExecuteScalar()) != 1) throw new MigrationException("Owned legacy sequence was not found: " + sequence);
        }
        if (!TableExists(name)) throw new MigrationException("Table was not found: " + name);
        base.RemoveTable(name);
        foreach (var sequence in sequences) ExecuteNonQuery("DROP SEQUENCE " + _dialect.Quote(sequence));
    }

    private void GuardAgainstMaximumColumnNameLengthForOracle(string name, Column[] columns)
    {
        foreach (var column in columns) GuardAgainstMaximumIdentifierLengthForOracle(column.Name);
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
        var sql = "SELECT COUNT(*) FROM ALL_INDEXES WHERE " + OracleCatalog.Predicate(this, table, "TABLE_NAME", "TABLE_OWNER")
            + " AND INDEX_NAME=" + OracleCatalog.Literal(SqlIdentifier.Catalog(QuoteConstraintNameIfRequired(name), true).Name);
        return Convert.ToInt32(ExecuteScalar(sql)) == 1;
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
        var indexItems = _oracleSystemDataLoader.GetIndexItems(table);

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

    private void Initialize()
    {
        _oracleSystemDataLoader = new OracleSystemDataLoader(this);
    }
}
