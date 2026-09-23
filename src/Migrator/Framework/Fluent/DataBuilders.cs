using System;
using System.Linq;
using DotNetProjects.Migrator.Framework.Models;

namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed class InsertRoot(MigrationBuilder builder)
{
    public InsertDataBuilder IntoTable(string table) => new(builder, BuilderArguments.Name(table));
}

public sealed class UpdateRoot(MigrationBuilder builder)
{
    public UpdateDataBuilder Table(string table) => new(builder, BuilderArguments.Name(table));
}

public sealed class InsertDataBuilder
{
    private readonly PendingOperation pending;
    private readonly string table;
    internal InsertDataBuilder(MigrationBuilder builder, string table)
    {
        this.table = table;
        pending = new PendingOperation(builder, $"Insert into '{table}' requires Row(...)");
    }
    /// <summary>Defines one insert. Start another Insert.IntoTable expression for another row.</summary>
    public InsertRowBuilder Row(string[] columns, object[] values)
    {
        var names = BuilderArguments.Names(columns);
        var data = BuilderArguments.Values(names, values);
        var row = new InsertRowBuilder();
        pending.Complete(() => new DataOperation(row.WhereColumns == null ? DataKind.Insert : DataKind.InsertIfMissing,
            table, names.ToArray(), BuilderArguments.Values(names, data), row.WhereColumns?.ToArray(),
            row.WhereColumns == null ? null : BuilderArguments.Values(row.WhereColumns, row.WhereValues)));
        return row;
    }
}

public sealed class InsertRowBuilder
{
    internal string[] WhereColumns { get; private set; }
    internal object[] WhereValues { get; private set; }
    internal InsertRowBuilder() { }
    public void IfNotExists(string[] columns, object[] values)
    {
        var names = BuilderArguments.Names(columns);
        var data = BuilderArguments.Values(names, values);
        WhereColumns = names; WhereValues = data;
    }
}

public sealed class UpdateDataBuilder
{
    private readonly PendingOperation pending;
    private readonly string table;
    internal UpdateDataBuilder(MigrationBuilder builder, string table)
    {
        this.table = table;
        pending = new PendingOperation(builder, $"Update '{table}' requires Set(...) and Where(...), WhereSql(...) or AllRows()");
    }
    public UpdateWhereBuilder Set(string[] columns, object[] values)
    {
        var names = BuilderArguments.Names(columns);
        var data = BuilderArguments.Values(names, values);
        return new UpdateWhereBuilder(pending, table, names, data);
    }
}

public sealed class UpdateWhereBuilder
{
    private readonly PendingOperation pending;
    private readonly string table;
    private readonly string[] columns;
    private readonly object[] values;
    internal UpdateWhereBuilder(PendingOperation pending, string table, string[] columns, object[] values)
    { this.pending = pending; this.table = table; this.columns = columns; this.values = values; }
    public void Where(string[] columns, object[] values)
    {
        var names = BuilderArguments.Names(columns);
        var data = BuilderArguments.Values(names, values);
        Complete(names, data, null);
    }
    public void WhereSql(string sql) => Complete(null, null, BuilderArguments.Name(sql));
    public void AllRows() => Complete(null, null, null);
    private void Complete(string[] whereColumns, object[] whereValues, string sql)
        => pending.Complete(() => new DataOperation(DataKind.Update, table, columns.ToArray(), BuilderArguments.Values(columns, values),
            whereColumns?.ToArray(), whereColumns == null ? null : BuilderArguments.Values(whereColumns, whereValues), sql));
}

public sealed class DeleteDataBuilder
{
    private readonly PendingOperation pending;
    private readonly string table;
    internal DeleteDataBuilder(MigrationBuilder builder, string table)
    {
        this.table = table;
        pending = new PendingOperation(builder, $"Delete from '{table}' requires Where(...) or AllRows()");
    }
    public void Where(string[] columns, object[] values)
    {
        var names = BuilderArguments.Names(columns);
        var data = BuilderArguments.Values(names, values);
        pending.Complete(() => new DataOperation(DataKind.Delete, table, Array.Empty<string>(), Array.Empty<object>(),
            names.ToArray(), BuilderArguments.Values(names, data)));
    }
    public void AllRows() => pending.Complete(() => new DataOperation(DataKind.Delete, table, Array.Empty<string>(), Array.Empty<object>()));
}

public sealed class CopyDataColumnsBuilder
{
    private readonly PendingOperation pending;
    private readonly string source, target;
    internal CopyDataColumnsBuilder(PendingOperation pending, string source, string target)
    { this.pending = pending; this.source = source; this.target = target; }
    public CopyDataOptionsBuilder WithColumns(string[] sourceColumns, string[] targetColumns)
    {
        var from = BuilderArguments.Names(sourceColumns);
        var to = BuilderArguments.Names(targetColumns);
        if (from.Length != to.Length) throw new ArgumentException("Source and target must have equal column counts.");
        var options = new CopyDataOptionsBuilder();
        pending.Complete(() => new CopyDataOperation(source, from.ToArray(), target, to.ToArray(), options.OrderColumns?.ToArray()));
        return options;
    }
}

public sealed class CopyDataOptionsBuilder
{
    internal string[] OrderColumns { get; private set; }
    internal CopyDataOptionsBuilder() { }
    public CopyDataOptionsBuilder OrderBy(params string[] columns) { OrderColumns = BuilderArguments.Names(columns); return this; }
}

public sealed class UpdateFromSetBuilder
{
    private readonly PendingOperation pending;
    private readonly string source, target;
    internal UpdateFromSetBuilder(PendingOperation pending, string source, string target)
    { this.pending = pending; this.source = source; this.target = target; }
    public UpdateFromMatchBuilder Set(params ColumnPair[] columns)
        => new(pending, source, target, CopyPairs(columns));

    internal static ColumnPair[] CopyPairs(ColumnPair[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0) throw new ArgumentException("At least one column pair is required.", nameof(columns));
        return columns.Select(pair => new ColumnPair
        {
            ColumnNameSource = BuilderArguments.Name(pair.ColumnNameSource),
            ColumnNameTarget = BuilderArguments.Name(pair.ColumnNameTarget)
        }).ToArray();
    }
}

public sealed class UpdateFromMatchBuilder
{
    private readonly PendingOperation pending;
    private readonly string source, target;
    private readonly ColumnPair[] copy;
    internal UpdateFromMatchBuilder(PendingOperation pending, string source, string target, ColumnPair[] copy)
    { this.pending = pending; this.source = source; this.target = target; this.copy = copy; }
    public void Match(params ColumnPair[] columns)
    {
        var match = UpdateFromSetBuilder.CopyPairs(columns);
        pending.Complete(() => new UpdateFromOperation(source, target, copy.Select(Definitions.CopyPair).ToArray(), match.Select(Definitions.CopyPair).ToArray()));
    }
}
