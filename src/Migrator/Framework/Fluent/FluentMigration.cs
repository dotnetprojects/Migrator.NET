using System;
using System.Collections.Generic;
using System.Linq;
namespace DotNetProjects.Migrator.Framework.Fluent;

public abstract class FluentMigration : Migration
{
    public abstract void BuildUp(MigrationBuilder migration);
    public virtual void BuildDown(MigrationBuilder migration) => throw new IrreversibleMigrationException();
    public IReadOnlyList<MigrationOperation> GetOperations(bool up)
    {
        var builder = new MigrationBuilder();
        if (up) BuildUp(builder); else BuildDown(builder);
        return builder.Build();
    }
    public override void Up() { foreach (var op in GetOperations(true)) op.Apply(Database); }
    public override void Down() { foreach (var op in GetOperations(false)) op.Apply(Database); }
    protected SchemaInspector Schema => new(Database);
    protected ITransformationProvider Context => Database;
}
public abstract class AutoReversingMigration : FluentMigration
{
    public override void Up()
    {
        var operations = GetOperations(true);
        foreach (var op in operations) _ = op.Reverse(); // Validate before the first change.
        foreach (var op in operations) op.Apply(Database);
    }
    public override void BuildDown(MigrationBuilder migration)
    { foreach (var op in GetOperations(true).Reverse()) migration.Add(op.Reverse()); }
}
public sealed class SchemaInspector(ITransformationProvider provider)
{
    public TableInspector Table(string name) => new(provider, name);
    public bool ViewExists(string name) => provider.ViewExists(name);
    public bool DatabaseExists(string name) => provider.DatabaseExists(name);
    public IEnumerable<string> Databases() => provider.GetDatabases();
    public IEnumerable<string> Tables() => provider.GetTables();
    public IEnumerable<string> Tables(string schema) => provider.GetTables(schema);
    public object Scalar(string sql) => provider.ExecuteScalar(sql);
    public T? NullableScalar<T>(string sql) where T : struct => provider.ExecuteNullableScalar<T>(sql);
    public List<string> Strings(string sql, params object[] args) => provider.ExecuteStringQuery(sql, args);
    public void Query(string sql, Action<System.Data.IDataReader> read)
    { using var command = provider.CreateCommand(); using var reader = provider.ExecuteQuery(command, sql); read(reader); }
    public string QuoteColumn(string name) => provider.QuoteColumnNameIfRequired(name);
    public string QuoteTable(string name) => provider.QuoteTableNameIfRequired(name);
    public string Encode(Guid guid) => provider.Encode(guid);
    public string Concatenate(params string[] strings) => provider.Concatenate(strings);
}
public sealed class TableInspector(ITransformationProvider provider, string table)
{
    public bool Exists() => provider.TableExists(table);
    public bool ColumnExists(string column) => provider.ColumnExists(table, column);
    public bool ConstraintExists(string name) => provider.ConstraintExists(table, name);
    public bool IndexExists(string name) => provider.IndexExists(table, name);
    public bool PrimaryKeyExists(string name) => provider.PrimaryKeyExists(table, name);
    public Column[] Columns() => provider.GetColumns(table);
    public Column Column(string name) => provider.GetColumnByName(table, name);
    public Index[] Indexes() => provider.GetIndexes(table);
    public ForeignKeyConstraint[] ForeignKeys() => provider.GetForeignKeyConstraints(table);
    public string[] Constraints() => (provider as DotNetProjects.Migrator.Providers.TransformationProvider)?.GetConstraints(table) ?? throw new NotSupportedException("Provider does not expose constraint enumeration.");
    public int ContentSize(string column) => provider.GetColumnContentSize(table, column);
    public int? NullableContentSize(string column) => provider.GetNullableColumnContentSize(table, column);
}
