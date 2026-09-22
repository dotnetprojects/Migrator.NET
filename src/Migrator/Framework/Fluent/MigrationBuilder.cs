using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models;
using DotNetProjects.Migrator.Framework.Models;
using Index = DotNetProjects.Migrator.Framework.Index;
namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed class MigrationBuilder
{
    private readonly List<Func<MigrationOperation>> operations = new();
    public CreateRoot Create => new(this);
    public AlterRoot Alter => new(this);
    public DeleteRoot Delete => new(this);
    public RenameRoot Rename => new(this);
    public DataRoot Insert => new(this, DataKind.Insert);
    public DataRoot Update => new(this, DataKind.Update);
    public ExecuteRoot Execute => new(this);
    public AdministrationRoot Administration => new(this);
    public void Add(MigrationOperation operation) => operations.Add(() => operation);
    internal void Add(Func<MigrationOperation> operation) => operations.Add(operation);
    public IReadOnlyList<MigrationOperation> Build() => operations.Select(x => x()).ToArray();
    public void Apply(ITransformationProvider provider) => ApplyOperations(provider, Build());
    internal static void ApplyOperations(ITransformationProvider provider, IReadOnlyList<MigrationOperation> operations)
    {
        foreach (var operation in operations) operation.Validate(provider);
        foreach (var operation in operations) operation.Apply(provider);
    }
    public IReadOnlyList<string> Preview(SqlGenerationContext context) => Build().Select(op => op.ToSql(context)).Where(sql => sql.Length != 0).ToArray();
    public void IfDatabase(string name, Action<MigrationBuilder> configure)
    {
        var nested = new MigrationBuilder(); configure(nested);
        foreach (var op in nested.Build()) Add(new ConditionalOperation(name, op));
    }
    public void WithReverse(MigrationOperation forward, MigrationOperation backward) => Add(new ReversibleOperation(forward, backward));
}
public sealed class CreateRoot(MigrationBuilder builder)
{
    public TableBuilder Table(string name) => new(builder, name);
    public ColumnBuilder Column(string name, string table) => new(builder, table, name, false);
    public void Index(string table, Index index) => builder.Add(new IndexOperation(table, (Index)Definitions.Copy(index)));
    public void PrimaryKey(string name, string table, params string[] columns) => Constraint(ConstraintKind.PrimaryKey, name, table, columns);
    public void NonClusteredPrimaryKey(string name, string table, params string[] columns) => Constraint(ConstraintKind.NonClusteredPrimaryKey, name, table, columns);
    public void Unique(string name, string table, params string[] columns) => Constraint(ConstraintKind.Unique, name, table, columns);
    private void Constraint(ConstraintKind kind, string name, string table, string[] columns) => builder.Add(new ConstraintOperation(kind, table, name, (string[])columns.Clone()));
    public void Check(string name, string table, string sql) => builder.Add(new ConstraintOperation(ConstraintKind.Check, table, name, Array.Empty<string>(), Check: sql));
    public void ForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns, ForeignKeyConstraintType onDelete = ForeignKeyConstraintType.NoAction, ForeignKeyConstraintType onUpdate = ForeignKeyConstraintType.NoAction)
        => builder.Add(new ConstraintOperation(ConstraintKind.ForeignKey, childTable, name, (string[])childColumns.Clone(), parentTable, (string[])parentColumns.Clone(), OnDelete: onDelete, OnUpdate: onUpdate));
    public void View(string name, string table, params IViewElement[] elements) => builder.Add(new ViewOperation(name, table, null, elements.Select(Definitions.CopyViewElement).ToArray()));
    public void View(string name, string table, params IViewField[] fields) => builder.Add(new ViewOperation(name, table, fields.Select(Definitions.CopyViewField).ToArray()));
}
public sealed class TableBuilder
{
    private readonly List<IDbField> fields = new();
    private Column current;
    private string engine;
    internal TableBuilder(MigrationBuilder builder, string name) => builder.Add(() => new CreateTableOperation(name, engine, fields.Select(Definitions.Copy).ToArray()));
    public TableBuilder WithColumn(string name) { current = new Column(name,DbType.String); fields.Add(current); return this; }
    public TableBuilder WithFields(params IDbField[] values) { fields.AddRange(values.Select(Definitions.Copy)); return this; }
    public TableBuilder WithPrimaryKey(string name, params string[] columns) { fields.Add(new PrimaryKeyConstraint(name, columns)); return this; }
    public TableBuilder WithUniqueConstraint(string name, params string[] columns) { fields.Add(new UniqueConstraint(name, columns)); return this; }
    public TableBuilder WithCheckConstraint(string name, string expression) { fields.Add(new CheckConstraint(name, expression)); return this; }
    public TableBuilder WithEngine(string value) { engine = value; return this; }
    private Column Current => current ?? throw new InvalidOperationException("Call WithColumn first.");
    public TableBuilder OfType(DbType type) { Current.Type = type; return this; }
    public TableBuilder OfType(MigratorDbType type) { Current.MigratorDbType = type; return this; }
    public TableBuilder AsInt32() => OfType(DbType.Int32);
    public TableBuilder AsInt64() => OfType(DbType.Int64);
    public TableBuilder AsString(int size = 255) { OfType(DbType.String); Current.Size = size; return this; }
    public TableBuilder AsGuid() => OfType(DbType.Guid);
    public TableBuilder AsBoolean() => OfType(DbType.Boolean);
    public TableBuilder AsDateTime() => OfType(DbType.DateTime2);
    public TableBuilder WithSize(int size) { Current.Size = size; return this; }
    public TableBuilder WithPrecision(int precision, int scale) { Current.Precision = precision; Current.Scale = scale; return this; }
    public TableBuilder WithDefaultValue(object value) { Current.DefaultValue = value; return this; }
    public TableBuilder NotNullable() { Current.IsNullable = false; return this; }
    public TableBuilder Nullable() { Current.IsNullable = true; return this; }
    public TableBuilder Unsigned() { Current.IsUnsigned = true; return this; }
    public TableBuilder WithCollation(Collation name) { Current.Collation = name; return this; }
    public TableBuilder Identity() { Current.IsIdentity = true; return this; }
}
public sealed class ColumnBuilder
{
    private readonly Column column;
    internal ColumnBuilder(MigrationBuilder builder, string table, string name, bool alter) { column = new Column(name,DbType.String); builder.Add(() => new ColumnOperation(table, Definitions.CopyColumn(column), alter)); }
    public ColumnBuilder OfType(DbType value) { column.Type = value; return this; }
    public ColumnBuilder OfType(MigratorDbType value) { column.MigratorDbType = value; return this; }
    public ColumnBuilder AsInt32() => OfType(DbType.Int32);
    public ColumnBuilder AsInt64() => OfType(DbType.Int64);
    public ColumnBuilder AsString(int size = 255) { column.Size = size; return OfType(DbType.String); }
    public ColumnBuilder WithSize(int value) { column.Size = value; return this; }
    public ColumnBuilder WithPrecision(int precision, int scale) { column.Precision = precision; column.Scale = scale; return this; }
    public ColumnBuilder WithDefaultValue(object value) { column.DefaultValue = value; return this; }
    public ColumnBuilder NotNullable() { column.IsNullable = false; return this; }
    public ColumnBuilder Nullable() { column.IsNullable = true; return this; }
    public ColumnBuilder Unsigned() { column.IsUnsigned = true; return this; }
    public ColumnBuilder WithCollation(Collation name) { column.Collation = name; return this; }
    public ColumnBuilder Identity() { column.IsIdentity = true; return this; }
}
public sealed class AlterRoot(MigrationBuilder builder)
{
    public ColumnBuilder Column(string name, string table) => new(builder, table, name, true);
    public void Column(string table, Column column) => builder.Add(new ColumnOperation(table, Definitions.CopyColumn(column), true));
}
public sealed class DeleteRoot(MigrationBuilder builder)
{
    public void Table(string table) => builder.Add(new RemoveOperation(RemoveKind.Table, table));
    public void Column(string name, string table) => builder.Add(new RemoveOperation(RemoveKind.Column, table, name));
    public void ForeignKey(string name, string table) => builder.Add(new RemoveOperation(RemoveKind.ForeignKey, table, name));
    public void Constraint(string name, string table) => builder.Add(new RemoveOperation(RemoveKind.Constraint, table, name));
    public void PrimaryKey(string table) => builder.Add(new RemoveOperation(RemoveKind.PrimaryKey, table));
    public void Default(string column, string table) => builder.Add(new RemoveOperation(RemoveKind.Default, table, column));
    public void Index(string name, string table) => builder.Add(new RemoveOperation(RemoveKind.Index, table, name));
    public void AllIndexes(string table) => builder.Add(new RemoveOperation(RemoveKind.AllIndexes, table));
    public void AllConstraints(string table) => builder.Add(new RemoveOperation(RemoveKind.AllConstraints, table));
    public void ForeignKeysForColumn(string table, string column) => builder.Add(new RemoveOperation(RemoveKind.ForeignKeysForColumn, table, column));
    public DataBuilder FromTable(string table) => new(builder, DataKind.Delete, table);
}
public sealed class RenameRoot(MigrationBuilder builder)
{
    public void Table(string oldName, string newName) => builder.Add(new RenameOperation(oldName, newName));
    public void Column(string table, string oldName, string newName) => builder.Add(new RenameOperation(table, newName, oldName));
}
public sealed class DataRoot(MigrationBuilder builder, DataKind kind)
{
    public DataBuilder IntoTable(string table) => new(builder, kind, table);
    public DataBuilder Table(string table) => new(builder, kind, table);
}
public sealed class DataBuilder
{
    private DataKind kind;
    private string[] columns = Array.Empty<string>(), whereColumns;
    private object[] values = Array.Empty<object>(), whereValues;
    private string whereSql;
    internal DataBuilder(MigrationBuilder builder, DataKind kind, string table) { this.kind = kind; builder.Add(() => new DataOperation(this.kind, table, columns.ToArray(), values.ToArray(), whereColumns?.ToArray(), whereValues?.ToArray(), whereSql)); }
    public DataBuilder Row(string[] names, object[] data) { if (kind == DataKind.Delete) throw new InvalidOperationException("Delete accepts predicates through Where, not row values."); if (names.Length != data.Length) throw new ArgumentException("Columns and values must have equal lengths."); columns = names.ToArray(); values = data.ToArray(); return this; }
    public DataBuilder Set(string[] names, object[] data) => Row(names, data);
    public DataBuilder Where(string[] names, object[] data) { if (names.Length != data.Length) throw new ArgumentException("Where columns and values must have equal lengths."); whereColumns = names.ToArray(); whereValues = data.ToArray(); return this; }
    public DataBuilder WhereSql(string sql) { if (kind != DataKind.Update) throw new NotSupportedException("Raw predicates are supported only for Update; use Where columns/values for Delete."); whereSql = sql; return this; }
    public DataBuilder IfNotExists(string[] names, object[] data) { if (kind is not (DataKind.Insert or DataKind.InsertIfMissing)) throw new InvalidOperationException("IfNotExists is only valid for Insert."); kind = DataKind.InsertIfMissing; return Where(names, data); }
}
public sealed class ExecuteRoot(MigrationBuilder builder)
{
    public void Sql(string sql, int? timeout = null, params object[] parameters) => builder.Add(new SqlOperation(sql, timeout, parameters.Length == 0 ? null : parameters.ToArray()));
    public void Script(string path) => builder.Add(new ScriptOperation(File.ReadAllText(path)));
    public void EmbeddedScript(Assembly assembly, string name) { using var stream = assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException("Resource not found", name); using var reader = new StreamReader(stream); builder.Add(new ScriptOperation(reader.ReadToEnd())); }
    public void WithProvider(Action<ITransformationProvider> action) => builder.Add(new CallbackOperation("Provider callback (not previewable)", action));
    public void WithCommand(Action<IDbCommand> action) => builder.Add(new CallbackOperation("Command callback", p => { using var command = p.CreateCommand(); action(command); }));
    public void WithConnection(Action<IDbConnection> action) => builder.Add(new CallbackOperation("Connection callback", p => action(p.Connection)));
    public void Truncate(string table) => builder.Add(new RemoveOperation(RemoveKind.Truncate, table));
    public void CopyData(string source, IEnumerable<string> sourceColumns, string target, IEnumerable<string> targetColumns, IEnumerable<string> orderBy = null)
    { builder.Add(new CopyDataOperation(source, sourceColumns.ToArray(), target, targetColumns.ToArray(), orderBy?.ToArray())); }
    public void UpdateFrom(string source, string target, ColumnPair[] copy, ColumnPair[] match)
    { builder.Add(new UpdateFromOperation(source, target, copy.Select(Definitions.CopyPair).ToArray(), match.Select(Definitions.CopyPair).ToArray())); }
}
public sealed class AdministrationRoot(MigrationBuilder builder)
{
    public void CreateDatabase(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.Create, name));
    public void DropDatabase(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.Drop, name));
    public void SwitchDatabase(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.Switch, name));
    public void KillConnections(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.KillConnections, name));
}
