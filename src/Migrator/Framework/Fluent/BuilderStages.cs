using System;
using System.Linq;

namespace DotNetProjects.Migrator.Framework.Fluent;

/// <summary>Selects the table containing the object being defined or renamed.</summary>
public sealed class OnTableBuilder<T>
{
    private readonly Func<string, T> next;
    internal OnTableBuilder(Func<string, T> next) => this.next = next;
    public T OnTable(string table) => next(BuilderArguments.Name(table));
}

/// <summary>Selects the source table of a relationship or view.</summary>
public sealed class FromTableBuilder<T>
{
    private readonly Func<string, T> next;
    internal FromTableBuilder(Func<string, T> next) => this.next = next;
    public T FromTable(string table) => next(BuilderArguments.Name(table));
}

/// <summary>Selects the destination table of a relationship or data copy.</summary>
public sealed class ToTableBuilder<T>
{
    private readonly Func<string, T> next;
    internal ToTableBuilder(Func<string, T> next) => this.next = next;
    public T ToTable(string table) => next(BuilderArguments.Name(table));
}

/// <summary>Supplies an ordered, nonempty set of column names.</summary>
public sealed class ColumnsBuilder<T>
{
    private readonly Func<string[], T> next;
    internal ColumnsBuilder(Func<string[], T> next) => this.next = next;
    public T WithColumns(params string[] columns) => next(BuilderArguments.Names(columns));
}

// Register at the start of a chain, retaining authoring order and rejecting unfinished
// expressions at Build(), before Apply() can execute any earlier operation.
internal sealed class PendingOperation
{
    private Func<MigrationOperation> operation;
    internal PendingOperation(MigrationBuilder builder, string description)
        => builder.Add(() => operation?.Invoke() ?? throw new InvalidOperationException($"Incomplete fluent operation: {description}."));

    internal void Complete(Func<MigrationOperation> value)
    {
        if (operation != null) throw new InvalidOperationException("This fluent operation has already been completed. Start a new expression for another operation.");
        operation = value;
    }
}

internal static class BuilderArguments
{
    internal static string Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A nonempty name or expression is required.", nameof(value));
        return value;
    }

    internal static string[] Names(string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0) throw new ArgumentException("At least one column is required.", nameof(values));
        return values.Select(Name).ToArray();
    }

    internal static object[] Values(string[] names, object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (names.Length != values.Length) throw new ArgumentException("Columns and values must have equal lengths.", nameof(values));
        return values.Select(value => value is byte[] bytes ? bytes.Clone() : value).ToArray();
    }
}
