using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed class MigrationBuilder
{
    private readonly List<Func<MigrationOperation>> operations = new();
    public CreateRoot Create => new(this);
    public AlterRoot Alter => new(this);
    public DeleteRoot Delete => new(this);
    public RenameRoot Rename => new(this);
    public InsertRoot Insert => new(this);
    public UpdateRoot Update => new(this);
    public ExecuteRoot Execute => new(this);
    public AdministrationRoot Administration => new(this);
    public void Add(MigrationOperation operation) => operations.Add(() => operation);
    internal void Add(Func<MigrationOperation> operation) => operations.Add(operation);
    /// <summary>Snapshots the expressions in authoring order, rejecting incomplete chains.</summary>
    public IReadOnlyList<MigrationOperation> Build() => operations.Select(x => x()).ToArray();
    public void Apply(ITransformationProvider provider) => ApplyOperations(provider, Build());
    internal static void ApplyOperations(ITransformationProvider provider, IReadOnlyList<MigrationOperation> operations)
    {
        foreach (var operation in operations) operation.Validate(provider);
        foreach (var operation in operations) operation.Apply(provider);
    }
    public IReadOnlyList<string> Preview(SqlGenerationContext context) => Build().Select(op => op.ToSql(context)).Where(sql => sql.Length != 0).ToArray();
    /// <summary>Queues operations only for a matching provider name (for example, SQLite).</summary>
    public void IfProvider(string name, Action<MigrationBuilder> configure)
    {
        BuilderArguments.Name(name);
        ArgumentNullException.ThrowIfNull(configure);
        var nested = new MigrationBuilder(); configure(nested);
        foreach (var op in nested.Build()) Add(new ConditionalOperation(name, op));
    }
    public void WithReverse(MigrationOperation forward, MigrationOperation backward) => Add(new ReversibleOperation(forward, backward));
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
    public ToTableBuilder<CopyDataColumnsBuilder> CopyDataFromTable(string source)
    {
        BuilderArguments.Name(source);
        var pending = new PendingOperation(builder, "Copy data requires ToTable(...).WithColumns(...)");
        return new(target => new CopyDataColumnsBuilder(pending, source, target));
    }
    public FromTableBuilder<UpdateFromSetBuilder> UpdateTable(string target)
    {
        BuilderArguments.Name(target);
        var pending = new PendingOperation(builder, "Update from another table requires FromTable(...).Set(...).Match(...)");
        return new(source => new UpdateFromSetBuilder(pending, source, target));
    }
}
public sealed class AdministrationRoot(MigrationBuilder builder)
{
    public void CreateDatabase(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.Create, name));
    public void DropDatabase(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.Drop, name));
    public void SwitchDatabase(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.Switch, name));
    public void KillConnections(string name) => builder.Add(new DatabaseOperation(DatabaseOperationKind.KillConnections, name));
}
