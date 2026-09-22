using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.DB2;

public class DB2TransformationProvider : TransformationProvider
{
    public DB2TransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        var factory = DbProviderFactoriesHelper.GetFactory(string.IsNullOrEmpty(providerName) ? "IBM.Data.DB2" : providerName, null, null);
        _connection = factory.CreateConnection();
        _connection.ConnectionString = connectionString;
        _connection.Open();
    }

    public DB2TransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
        : base(dialect, connection, null, scope) { }

    private static string Name(string name) => (name.StartsWith('"') ? name.Trim('"').Replace("\"\"", "\"") : name.ToUpperInvariant()).Replace("'", "''");
    private static string Identifier(string name) => name.StartsWith('"') ? name : "\"" + name.ToUpperInvariant().Replace("\"", "\"\"") + "\"";

    public override bool TableExists(string table) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM SYSCAT.TABLES WHERE TABSCHEMA=CURRENT SCHEMA AND TABNAME='{Name(table)}' AND TYPE='T'")) > 0;
    public override bool ViewExists(string view) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM SYSCAT.VIEWS WHERE VIEWSCHEMA=CURRENT SCHEMA AND VIEWNAME='{Name(view)}'")) > 0;
    public override string[] GetTables() => ExecuteStringQuery(
        "SELECT TABNAME FROM SYSCAT.TABLES WHERE TABSCHEMA=CURRENT SCHEMA AND TYPE='T'").ToArray();
    // SQL exposes the current database, not the client's local database directory.
    public override List<string> GetDatabases() => [Convert.ToString(ExecuteScalar("VALUES CURRENT SERVER")).Trim()];
    public override string[] GetConstraints(string table) => ExecuteStringQuery(
        $"SELECT CONSTNAME FROM SYSCAT.TABCONST WHERE TABSCHEMA=CURRENT SCHEMA AND TABNAME='{Name(table)}'").ToArray();
    public override bool ConstraintExists(string table, string name) => GetConstraints(table).Contains(Name(name));
    protected override string GetPrimaryKeyConstraintName(string table) => ExecuteStringQuery(
        $"SELECT CONSTNAME FROM SYSCAT.TABCONST WHERE TABSCHEMA=CURRENT SCHEMA AND TABNAME='{Name(table)}' AND TYPE='P'").FirstOrDefault();

    public override Column[] GetColumns(string table)
    {
        var columns = new List<Column>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT COLNAME, TYPENAME, NULLS, DEFAULT, LENGTH, IDENTITY, KEYSEQ
            FROM SYSCAT.COLUMNS WHERE TABSCHEMA=CURRENT SCHEMA AND TABNAME='{Name(table)}' ORDER BY COLNO
            """);
        while (reader.Read())
        {
            var type = reader.GetString(1).Trim() switch
            {
                "SMALLINT" => DbType.Int16, "INTEGER" => DbType.Int32, "BIGINT" => DbType.Int64,
                "DECIMAL" or "DECFLOAT" => DbType.Decimal, "DOUBLE" => DbType.Double, "REAL" => DbType.Single,
                "DATE" => DbType.Date, "TIME" => DbType.Time, "TIMESTAMP" => DbType.DateTime,
                "BLOB" or "BINARY" or "VARBINARY" => DbType.Binary, "BOOLEAN" => DbType.Boolean, _ => DbType.String
            };
            var column = new Column(reader.GetString(0).Trim(), type)
            {
                ColumnProperty = reader.GetString(2) == "Y" ? ColumnProperty.Null : ColumnProperty.NotNull
            };
            if (!reader.IsDBNull(3)) column.DefaultValue = reader.GetValue(3);
            if (type == DbType.String) column.Size = Convert.ToInt32(reader.GetValue(4));
            if (reader.GetString(5) == "Y") column.ColumnProperty |= ColumnProperty.Identity;
            if (!reader.IsDBNull(6)) column.ColumnProperty |= ColumnProperty.PrimaryKey;
            columns.Add(column);
        }
        return columns.ToArray();
    }

    public override Index[] GetIndexes(string table)
    {
        var indexes = new Dictionary<string, Index>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT i.INDNAME, i.UNIQUERULE, c.COLNAME, d.CONSTNAME FROM SYSCAT.INDEXES i
            JOIN SYSCAT.INDEXCOLUSE c ON c.INDSCHEMA=i.INDSCHEMA AND c.INDNAME=i.INDNAME
            LEFT JOIN SYSCAT.CONSTDEP d ON d.BSCHEMA=i.INDSCHEMA AND d.BNAME=i.INDNAME AND d.BTYPE='I' AND d.TABSCHEMA=i.TABSCHEMA AND d.TABNAME=i.TABNAME
            WHERE i.TABSCHEMA=CURRENT SCHEMA AND i.TABNAME='{Name(table)}'
            ORDER BY i.INDNAME, c.COLSEQ
            """);
        while (reader.Read())
        {
            var name = reader.GetString(0).Trim();
            if (!indexes.TryGetValue(name, out var index))
            {
                index = new Index { Name = name, Unique = reader.GetString(1) != "D", PrimaryKey = reader.GetString(1) == "P", UniqueConstraint = reader.GetString(1) == "U" && !reader.IsDBNull(3) };
                indexes.Add(name, index);
            }
            index.KeyColumns = [..index.KeyColumns, reader.GetString(2).Trim()];
        }
        return indexes.Values.ToArray();
    }

    public override void RemoveAllIndexes(string table)
    {
        // Constraint and backing-index names need not match in Db2.
        var constraints = ExecuteStringQuery($"SELECT CONSTNAME FROM SYSCAT.TABCONST WHERE TABSCHEMA=CURRENT SCHEMA AND TABNAME='{Name(table)}' AND TYPE IN ('P','U')");
        foreach (var name in constraints) RemoveConstraint(table, name);
        foreach (var index in GetIndexes(table)) RemoveIndex(table, index.Name);
    }

    public override bool IndexExists(string table, string name) => GetIndexes(table).Any(i => i.Name == Name(name));
    public override string AddIndex(string table, Index index)
    {
        if (index.KeyColumns.Length == 0) throw new ArgumentException("An index needs key columns.", nameof(index));
        if (index.IncludeColumns.Length != 0 || index.FilterItems.Count != 0 || index.Clustered)
            throw new NotSupportedException("This Db2 provider supports ordinary and unique indexes without INCLUDE, filters or clustering.");
        var name = index.Name ?? $"IX_{table}_{string.Join("_", index.KeyColumns)}";
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {Identifier(name)} ON {Identifier(table)} ({string.Join(", ", index.KeyColumns.Select(Identifier))})");
        return name;
    }

    public override void ChangeColumn(string table, Column column)
    {
        var prefix = $"ALTER TABLE {Identifier(table)} ALTER COLUMN {Identifier(column.Name)}";
        var type = column.Size > 0 ? _dialect.GetTypeName(column.Type, column.Size) : _dialect.GetTypeName(column.Type);
        ExecuteNonQuery($"{prefix} SET DATA TYPE {type}");
        if (column.DefaultValue != null || GetColumns(table).Single(c => c.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase)).DefaultValue != null)
            ExecuteNonQuery($"{prefix} {(column.DefaultValue == null ? "DROP DEFAULT" : "SET " + _dialect.Default(column.DefaultValue))}");
        ExecuteNonQuery($"{prefix} {(column.ColumnProperty.HasFlag(ColumnProperty.NotNull) ? "SET" : "DROP")} NOT NULL");
        Reorganize(table);
    }

    public override void RemoveColumn(string tableName, string column)
    {
        base.RemoveColumn(tableName, column);
        Reorganize(tableName);
    }

    private void Reorganize(string table)
    {
        var schema = Convert.ToString(ExecuteScalar("VALUES CURRENT SCHEMA")).Trim();
        ExecuteNonQuery($"CALL SYSPROC.ADMIN_CMD('REORG TABLE {schema}.{Identifier(table).Replace("'", "''")}')");
    }

    public override void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns, ForeignKeyConstraintType constraint)
    {
        // Db2 supports only NO ACTION/RESTRICT for ON UPDATE.
        var delete = constraint switch
        {
            ForeignKeyConstraintType.Cascade => "CASCADE",
            ForeignKeyConstraintType.SetNull => "SET NULL",
            ForeignKeyConstraintType.NoAction => "NO ACTION",
            ForeignKeyConstraintType.Restrict => "RESTRICT",
            _ => throw new NotSupportedException("This referential action is not supported by Db2.")
        };
        ExecuteNonQuery($"ALTER TABLE {Identifier(childTable)} ADD CONSTRAINT {Identifier(name)} FOREIGN KEY ({string.Join(", ", childColumns.Select(Identifier))}) REFERENCES {Identifier(parentTable)} ({string.Join(", ", parentColumns.Select(Identifier))}) ON DELETE {delete} ON UPDATE NO ACTION");
    }
}
