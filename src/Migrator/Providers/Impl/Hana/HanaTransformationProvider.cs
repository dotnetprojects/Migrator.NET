using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;

namespace DotNetProjects.Migrator.Providers.Impl.Hana;

/// <summary>SAP HANA 2 provider. DDL uses the engine's default autocommit behavior;
/// whole-session transactional DDL and native migration locks are not advertised.</summary>
public class HanaTransformationProvider : TransformationProvider
{
    public HanaTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        var factory = DbProviderFactoriesHelper.GetFactory(string.IsNullOrEmpty(providerName) ? "Sap.Data.Hana" : providerName,
            "Sap.Data.Hana.Net.v8.0", "Sap.Data.Hana.HanaFactory");
        _connection = factory.CreateConnection();
        _connection.ConnectionString = connectionString;
        _connection.Open();
    }
    public HanaTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope)
        : base(dialect, connection, defaultSchema, scope) { }

    // SAP's ADO.NET driver uses positional parameters.
    public override string GenerateParameterName(int index) => "?";
    public override string GenerateParameterNameParameter(int index) => "p" + index;
    private (string Schema, string Table) Name(string table)
    {
        var parts = table.Split('.');
        if (parts.Length > 2 || parts.Any(string.IsNullOrWhiteSpace) || parts.Any(x => x.Contains('"')))
            throw new NotSupportedException("HANA names must be unquoted table or schema.table names. Embedded dots/quotes require explicit SQL.");
        return (parts.Length == 2 ? parts[0] : _defaultSchema, parts[^1]);
    }
    public override string QuoteTableNameIfRequired(string table)
    {
        var name = Name(table);
        return (name.Schema == null ? "" : Dialect.QuoteIdentifier(name.Schema) + ".") + Dialect.QuoteIdentifier(name.Table);
    }
    public override string QuoteColumnNameIfRequired(string column) => Dialect.QuoteIdentifier(column);
    private IDbCommand Catalog(string sql, params object[] values)
    {
        var command = CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "p" + i; parameter.DbType = DbType.String;
            parameter.Value = values[i] ?? DBNull.Value; command.Parameters.Add(parameter);
        }
        return command;
    }
    private bool Exists(string view, string table)
    {
        var name = Name(table);
        using var command = Catalog("SELECT COUNT(*) FROM SYS." + view + " WHERE SCHEMA_NAME=COALESCE(?,CURRENT_SCHEMA) AND " + (view == "VIEWS" ? "VIEW_NAME" : "TABLE_NAME") + "=?", name.Schema, name.Table);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
    public override bool TableExists(string table) => Exists("TABLES", table);
    public override bool ViewExists(string table) => Exists("VIEWS", table);
    public override string[] GetTables()
    {
        using var command = Catalog("SELECT TABLE_NAME FROM SYS.TABLES WHERE SCHEMA_NAME=COALESCE(?,CURRENT_SCHEMA) ORDER BY TABLE_NAME", _defaultSchema);
        using var reader = command.ExecuteReader();
        var names = new List<string>(); while (reader.Read()) names.Add(reader.GetString(0)); return names.ToArray();
    }
    public override List<string> GetDatabases() => [Convert.ToString(ExecuteScalar("SELECT DATABASE_NAME FROM SYS.M_DATABASE"))];
    public override void SwitchDatabase(string databaseName) => throw new NotSupportedException("Connect to the target HANA tenant explicitly.");
    public override void CreateDatabases(string databaseName) => throw new NotSupportedException("HANA tenant administration requires an explicit SYSTEMDB connection and operation.");
    public override void DropDatabases(string databaseName) => throw new NotSupportedException("HANA tenant administration requires an explicit SYSTEMDB connection and operation.");
    public override void KillDatabaseConnections(string databaseName) => throw new NotSupportedException("Use explicit HANA connection administration.");

    public override void AddTable(string table, string engine, string columns)
    {
        if (engine != null && engine is not ("ROW" or "COLUMN")) throw new NotSupportedException("HANA table engine must be ROW or COLUMN.");
        ExecuteNonQuery($"CREATE {engine ?? "ROW"} TABLE {QuoteTableNameIfRequired(table)} ({columns})");
    }
    public override void AddColumn(string table, string definition) => ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} ADD ({definition})");
    public override void ChangeColumn(string table, string definition) => ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} ALTER ({definition})");
    public override void ChangeColumn(string table, Column column)
    {
        if (column.IsIdentity) throw new NotSupportedException("Changing HANA identity properties requires explicit SQL.");
        ChangeColumn(table, _dialect.GetAndMapColumnProperties(column.CopyDefinition()).ColumnSql);
    }
    public override void RemoveColumn(string table, string column) => ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} DROP ({QuoteColumnNameIfRequired(column)})");
    public override void RemoveTable(string table) => ExecuteNonQuery("DROP TABLE " + QuoteTableNameIfRequired(table));
    public override void RenameTable(string table, string name) => ExecuteNonQuery($"RENAME TABLE {QuoteTableNameIfRequired(table)} TO {QuoteTableNameIfRequired(name)}");
    public override void RenameColumn(string table, string column, string name) => ExecuteNonQuery($"RENAME COLUMN {QuoteTableNameIfRequired(table)}.{QuoteColumnNameIfRequired(column)} TO {QuoteColumnNameIfRequired(name)}");
    public override void RemoveColumnDefaultValue(string table, string column) => AddColumnDefaultValue(table, column, RawSql.Insert("NULL"));
    public override void AddColumnDefaultValue(string table, string column, object value)
    {
        var definition = GetColumns(table).SingleOrDefault(c => c.Name == column)
            ?? throw new ArgumentException("HANA column does not exist: " + column, nameof(column));
        if (definition.IsIdentity) throw new NotSupportedException("HANA identity defaults cannot be changed.");
        definition.DefaultValue = value ?? RawSql.Insert("NULL");
        ChangeColumn(table, definition);
    }
    public override int TruncateTable(string table) => ExecuteNonQuery("TRUNCATE TABLE " + QuoteTableNameIfRequired(table));
    public override void AddForeignKey(string name, string child, string[] columns, string parent, string[] parentColumns, ForeignKeyConstraintType onDelete, ForeignKeyConstraintType onUpdate) =>
        base.AddForeignKey(name, child, columns, parent, parentColumns,
            onDelete == ForeignKeyConstraintType.NoAction ? ForeignKeyConstraintType.Restrict : onDelete,
            onUpdate == ForeignKeyConstraintType.NoAction ? ForeignKeyConstraintType.Restrict : onUpdate);
    public override string[] GetConstraints(string table) => GetTableConstraints(table).Select(c => c.Name).ToArray();
    public override bool ConstraintExists(string table, string name) => GetConstraints(table).Contains(name, StringComparer.Ordinal);
    protected override string GetPrimaryKeyConstraintName(string table) => GetTableConstraints(table).OfType<PrimaryKeyConstraint>().SingleOrDefault()?.Name;
    public override bool PrimaryKeyExists(string table, string name) => GetPrimaryKeyConstraintName(table) is string actual && actual == name;
    public override void RemoveAllForeignKeys(string table, string column)
    {
        foreach (var key in GetForeignKeyConstraints(table).Where(k => k.ChildColumns.Contains(column))) RemoveForeignKey(table, key.Name);
    }
    public override Column[] GetColumns(string table)
    {
        var name = Name(table);
        using var command = Catalog("SELECT COLUMN_NAME,DATA_TYPE_NAME,LENGTH,SCALE,IS_NULLABLE,DEFAULT_VALUE,GENERATION_TYPE FROM SYS.TABLE_COLUMNS WHERE SCHEMA_NAME=COALESCE(?,CURRENT_SCHEMA) AND TABLE_NAME=? ORDER BY POSITION", name.Schema, name.Table);
        using var reader = command.ExecuteReader(); var columns = new List<Column>();
        while (reader.Read())
        {
            var typeName = reader.GetString(1);
            var type = typeName switch
            {
                "TINYINT" => DbType.Byte, "SMALLINT" => DbType.Int16, "INTEGER" => DbType.Int32, "BIGINT" => DbType.Int64,
                "BOOLEAN" => DbType.Boolean, "DECIMAL" => DbType.Decimal, "REAL" => DbType.Single, "DOUBLE" => DbType.Double,
                "VARCHAR" or "CLOB" => DbType.AnsiString, "NVARCHAR" or "NCLOB" or "SHORTTEXT" => DbType.String,
                "CHAR" => DbType.AnsiStringFixedLength, "NCHAR" => DbType.StringFixedLength,
                "BINARY" or "VARBINARY" or "BLOB" => DbType.Binary,
                "DATE" => DbType.Date, "TIME" => DbType.Time, "TIMESTAMP" or "SECONDDATE" => DbType.DateTime,
                _ => throw new NotSupportedException("HANA catalog type is not representable: " + typeName)
            };
            var generation = reader.IsDBNull(6) ? null : reader.GetString(6);
            if (!string.IsNullOrEmpty(generation) && !generation.Contains("IDENTITY")) throw new NotSupportedException("HANA computed column metadata requires explicit SQL.");
            var column = new Column(reader.GetString(0), type) { IsNullable = reader.GetString(4) == "TRUE", IsIdentity = generation?.Contains("IDENTITY") == true };
            if (type == DbType.Decimal) { column.Precision = Convert.ToInt32(reader.GetValue(2)); column.Scale = Convert.ToInt32(reader.GetValue(3)); }
            else if (typeName is "NCLOB" or "CLOB") column.Size = int.MaxValue;
            else if (type is DbType.String or DbType.AnsiString or DbType.StringFixedLength or DbType.AnsiStringFixedLength || typeName == "VARBINARY") column.Size = Convert.ToInt32(reader.GetValue(2));
            if (!reader.IsDBNull(5) && !column.IsIdentity) column.DefaultValue = CatalogDefaultValue.Parse(reader.GetString(5), type);
            columns.Add(column);
        }
        return columns.ToArray();
    }
    public override TableConstraint[] GetTableConstraints(string table)
    {
        var name = Name(table);
        var rows = new List<(string Name, string Column, bool Primary, bool Unique, string Check)>();
        using (var command = Catalog("SELECT CONSTRAINT_NAME,COLUMN_NAME,IS_PRIMARY_KEY,IS_UNIQUE_KEY,CHECK_CONDITION FROM SYS.CONSTRAINTS WHERE SCHEMA_NAME=COALESCE(?,CURRENT_SCHEMA) AND TABLE_NAME=? ORDER BY CONSTRAINT_NAME,POSITION", name.Schema, name.Table))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) rows.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), !reader.IsDBNull(2) && reader.GetString(2) == "TRUE", !reader.IsDBNull(3) && reader.GetString(3) == "TRUE", reader.IsDBNull(4) ? null : reader.GetString(4)));
        var constraints = rows.GroupBy(r => r.Name).Select(g => g.First().Primary ? (TableConstraint)new PrimaryKeyConstraint(g.Key, g.Select(r => r.Column).ToArray())
            : g.First().Unique ? new UniqueConstraint(g.Key, g.Select(r => r.Column).ToArray())
            : g.First().Check != null ? new CheckConstraint(g.Key, g.First().Check)
            : throw new NotSupportedException("Unsupported HANA constraint: " + g.Key)).ToList();
        constraints.AddRange(GetForeignKeyConstraints(table)); return constraints.ToArray();
    }
    public override ForeignKeyConstraint[] GetForeignKeyConstraints(string table)
    {
        var name = Name(table);
        var rows = new List<(string Name, string Column, string ParentSchema, string Parent, string ParentColumn, string Delete, string Update)>();
        using (var command = Catalog("SELECT CONSTRAINT_NAME,COLUMN_NAME,REFERENCED_SCHEMA_NAME,REFERENCED_TABLE_NAME,REFERENCED_COLUMN_NAME,DELETE_RULE,UPDATE_RULE FROM SYS.REFERENTIAL_CONSTRAINTS WHERE SCHEMA_NAME=COALESCE(?,CURRENT_SCHEMA) AND TABLE_NAME=? ORDER BY CONSTRAINT_NAME,POSITION", name.Schema, name.Table))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
        return rows.GroupBy(r => r.Name).Select(g => new ForeignKeyConstraint(g.Key, g.First().ParentSchema + "." + g.First().Parent,
            g.Select(r => r.ParentColumn).ToArray(), table, g.Select(r => r.Column).ToArray())
            { OnDelete = g.First().Delete, OnUpdate = g.First().Update }).ToArray();
    }
    public override string AddIndex(string table, Index index)
    {
        if (index.Clustered || index.IncludeColumns?.Length > 0 || index.FilterItems?.Count > 0)
            throw new NotSupportedException("HANA index INCLUDE, clustered and filtered options are not supported by this provider.");
        if (index.KeyColumns?.Length is not > 0) throw new ArgumentException("Index key columns are required.", nameof(index));
        var name = index.Name ?? "IX_" + Name(table).Table + "_" + string.Join("_", index.KeyColumns);
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {Dialect.QuoteIdentifier(name)} ON {QuoteTableNameIfRequired(table)} ({string.Join(", ", index.KeyColumns.Select(QuoteColumnNameIfRequired))})");
        return name;
    }
    public override Index[] GetIndexes(string table)
    {
        var name = Name(table);
        var rows = new List<(string Name, string Column, string Constraint)>();
        using (var command = Catalog("SELECT INDEX_NAME,COLUMN_NAME,CONSTRAINT FROM SYS.INDEX_COLUMNS WHERE SCHEMA_NAME=COALESCE(?,CURRENT_SCHEMA) AND TABLE_NAME=? ORDER BY INDEX_NAME,POSITION", name.Schema, name.Table))
        using (var reader = command.ExecuteReader())
            while (reader.Read()) rows.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2)));
        var constraints = GetTableConstraints(table).ToDictionary(c => c.Name, StringComparer.Ordinal);
        return rows.GroupBy(r => r.Name).Select(g => new Index { Name = g.Key, KeyColumns = g.Select(r => r.Column).ToArray(),
            Unique = g.First().Constraint.Contains("UNIQUE") || g.First().Constraint == "PRIMARY_KEY",
            PrimaryKey = constraints.TryGetValue(g.Key, out var c) && c is PrimaryKeyConstraint,
            UniqueConstraint = constraints.TryGetValue(g.Key, out var u) && u is UniqueConstraint }).ToArray();
    }
    public override bool IndexExists(string table, string name) => GetIndexes(table).Any(i => i.Name == name);
    public override void RemoveIndex(string table, string name)
    {
        var schema = Name(table).Schema;
        ExecuteNonQuery("DROP INDEX " + (schema == null ? "" : Dialect.QuoteIdentifier(schema) + ".") + Dialect.QuoteIdentifier(name));
    }
    public override void RemoveAllIndexes(string table)
    {
        foreach (var index in GetIndexes(table).Where(i => !i.PrimaryKey && !i.UniqueConstraint)) RemoveIndex(table, index.Name);
    }
}
