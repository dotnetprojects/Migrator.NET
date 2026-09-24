using System;
using DotNetProjects.Migrator.Providers;

namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed class DuplicateRowsBuilder
{
    private readonly PendingOperation pending;
    private readonly string table;
    internal DuplicateRowsBuilder(PendingOperation pending, string table) { this.pending = pending; this.table = table; }

    public DuplicateRowsRetentionBuilder ByColumns(params string[] columns)
    {
        DuplicateRowDeletion.Validate(table, columns, DuplicateRowRetention.Any, DuplicateNullHandling.Equal);
        return new DuplicateRowsRetentionBuilder(pending, table, (string[])columns.Clone());
    }
}

public sealed class DuplicateRowsRetentionBuilder
{
    private readonly PendingOperation pending;
    private readonly string table;
    private readonly string[] columns;
    internal DuplicateRowsRetentionBuilder(PendingOperation pending, string table, string[] columns)
    { this.pending = pending; this.table = table; this.columns = columns; }

    public void KeepAny(DuplicateNullHandling nulls = DuplicateNullHandling.Equal)
    {
        DuplicateRowDeletion.Validate(table, columns, DuplicateRowRetention.Any, nulls);
        pending.Complete(() => new DeleteDuplicateRowsOperation(table, (string[])columns.Clone(), nulls));
    }
}

public sealed record DeleteDuplicateRowsOperation(string Table, string[] KeyColumns, DuplicateNullHandling Nulls) : MigrationOperation
{
    public override void Validate(ITransformationProvider provider)
    {
        DuplicateRowDeletion.Validate(Table, KeyColumns, DuplicateRowRetention.Any, Nulls);
        if (provider is not NoOpTransformationProvider && !DuplicateRowDeletion.Supports(provider.Dialect))
            throw new NotSupportedException("Duplicate-row deletion is unsupported by this provider.");
    }
    public override void Apply(ITransformationProvider provider) => provider.DeleteDuplicateRows(Table, KeyColumns, DuplicateRowRetention.Any, Nulls);
    // Physical row identity requires live metadata. The base rejects SQL preview and automatic reversal.
}
