using System;
using System.Linq;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed class CreateRoot(MigrationBuilder builder)
{
    public TableBuilder Table(string name) => new(builder, name);
    public OnTableBuilder<ColumnBuilder> Column(string name) => ColumnExpression.Start(builder, name, false);
    public ColumnDefinitionOnTableBuilder Column(Column definition) => new(builder, definition, false);

    public OnTableBuilder<ConstraintColumnsBuilder> PrimaryKey(string name) => Constraint(ConstraintKind.PrimaryKey, name);
    public OnTableBuilder<ConstraintColumnsBuilder> NonClusteredPrimaryKey(string name) => Constraint(ConstraintKind.NonClusteredPrimaryKey, name);
    public OnTableBuilder<ConstraintColumnsBuilder> UniqueConstraint(string name) => Constraint(ConstraintKind.Unique, name);
    private OnTableBuilder<ConstraintColumnsBuilder> Constraint(ConstraintKind kind, string name)
    {
        BuilderArguments.Name(name);
        var pending = new PendingOperation(builder, $"{kind} '{name}' requires OnTable(...).WithColumns(...)");
        return new(table => new ConstraintColumnsBuilder(pending, kind, name, table));
    }
    public OnTableBuilder<CheckExpressionBuilder> CheckConstraint(string name)
    {
        BuilderArguments.Name(name);
        var pending = new PendingOperation(builder, $"Check '{name}' requires OnTable(...).WithExpression(...)");
        return new(table => new CheckExpressionBuilder(pending, name, table));
    }
    public FromTableBuilder<ColumnsBuilder<ToTableBuilder<ColumnsBuilder<ForeignKeyOptionsBuilder>>>> ForeignKey(string name)
    {
        BuilderArguments.Name(name);
        var pending = new PendingOperation(builder, $"Foreign key '{name}' requires FromTable(...).WithColumns(...).ToTable(...).WithColumns(...)");
        return new(child => new(columns => new(parent => new(parentColumns =>
        {
            if (columns.Length != parentColumns.Length) throw new ArgumentException("Child and parent keys must have equal column counts.");
            var options = new ForeignKeyOptionsBuilder();
            pending.Complete(() => new ConstraintOperation(ConstraintKind.ForeignKey, child, name, columns.ToArray(), parent, parentColumns.ToArray(),
                OnDelete: options.DeleteAction, OnUpdate: options.UpdateAction));
            return options;
        }))));
    }
    public OnTableBuilder<ColumnsBuilder<IndexOptionsBuilder>> Index(string name)
    {
        BuilderArguments.Name(name);
        var pending = new PendingOperation(builder, $"Index '{name}' requires OnTable(...).WithColumns(...)");
        return new(table => new(columns =>
        {
            var options = new IndexOptionsBuilder(new Index { Name = name, KeyColumns = columns });
            pending.Complete(() => new IndexOperation(table, options.BuildIndex()));
            return options;
        }));
    }
    public IndexDefinitionOnTableBuilder Index(Index definition) => new(builder, definition);
    public FromTableBuilder<ViewDefinitionBuilder> View(string name)
    {
        BuilderArguments.Name(name);
        var pending = new PendingOperation(builder, $"View '{name}' requires FromTable(...) and WithFields(...) or WithElements(...)");
        return new(table => new ViewDefinitionBuilder(pending, name, table));
    }
}

public sealed class ConstraintColumnsBuilder
{
    private readonly PendingOperation pending;
    private readonly ConstraintKind kind;
    private readonly string name, table;
    internal ConstraintColumnsBuilder(PendingOperation pending, ConstraintKind kind, string name, string table)
    { this.pending = pending; this.kind = kind; this.name = name; this.table = table; }
    public void WithColumns(params string[] columns)
    {
        var copy = BuilderArguments.Names(columns);
        pending.Complete(() => new ConstraintOperation(kind, table, name, copy.ToArray()));
    }
}

public sealed class CheckExpressionBuilder
{
    private readonly PendingOperation pending;
    private readonly string name, table;
    internal CheckExpressionBuilder(PendingOperation pending, string name, string table)
    { this.pending = pending; this.name = name; this.table = table; }
    public void WithExpression(string sql)
    {
        BuilderArguments.Name(sql);
        pending.Complete(() => new ConstraintOperation(ConstraintKind.Check, table, name, Array.Empty<string>(), Check: sql));
    }
}

public sealed class ForeignKeyOptionsBuilder
{
    internal ForeignKeyConstraintType DeleteAction { get; private set; } = ForeignKeyConstraintType.NoAction;
    internal ForeignKeyConstraintType UpdateAction { get; private set; } = ForeignKeyConstraintType.NoAction;
    internal ForeignKeyOptionsBuilder() { }
    public ForeignKeyOptionsBuilder OnDelete(ForeignKeyConstraintType action) { DeleteAction = action; return this; }
    public ForeignKeyOptionsBuilder OnUpdate(ForeignKeyConstraintType action) { UpdateAction = action; return this; }
}

public sealed class IndexOptionsBuilder
{
    private readonly Index index;
    internal IndexOptionsBuilder(Index index) => this.index = index;
    internal Index BuildIndex() => (Index)Definitions.Copy(index);
    public IndexOptionsBuilder Unique() { index.Unique = true; return this; }
    public IndexOptionsBuilder Clustered() { index.Clustered = true; return this; }
    public IndexOptionsBuilder IncludeColumns(params string[] columns) { index.IncludeColumns = BuilderArguments.Names(columns); return this; }
    public IndexOptionsBuilder WithFilter(params FilterItem[] filters)
    {
        index.FilterItems = filters.Select(f => new FilterItem { ColumnName = f.ColumnName, Filter = f.Filter, Value = f.Value }).ToList();
        return this;
    }
}

public sealed class IndexDefinitionOnTableBuilder
{
    private readonly PendingOperation pending;
    private readonly Index index;
    internal IndexDefinitionOnTableBuilder(MigrationBuilder builder, Index definition)
    {
        index = (Index)Definitions.Copy(definition);
        BuilderArguments.Name(index.Name);
        BuilderArguments.Names(index.KeyColumns);
        pending = new PendingOperation(builder, $"Index '{index.Name}' requires OnTable(...)");
    }
    public void OnTable(string table)
    {
        BuilderArguments.Name(table);
        pending.Complete(() => new IndexOperation(table, (Index)Definitions.Copy(index)));
    }
}

public sealed class ViewDefinitionBuilder
{
    private readonly PendingOperation pending;
    private readonly string name, table;
    internal ViewDefinitionBuilder(PendingOperation pending, string name, string table)
    { this.pending = pending; this.name = name; this.table = table; }
    public void WithFields(params IViewField[] fields)
    {
        var copy = fields.Select(Definitions.CopyViewField).ToArray();
        pending.Complete(() => new ViewOperation(name, table, copy.Select(Definitions.CopyViewField).ToArray()));
    }
    public void WithElements(params IViewElement[] elements)
    {
        var copy = elements.Select(Definitions.CopyViewElement).ToArray();
        pending.Complete(() => new ViewOperation(name, table, null, copy.Select(Definitions.CopyViewElement).ToArray()));
    }
}

public sealed class DeleteRoot(MigrationBuilder builder)
{
    public void Table(string table) => builder.Add(new RemoveOperation(RemoveKind.Table, BuilderArguments.Name(table)));
    public RemoveFromTableBuilder Column(string name) => Remove(RemoveKind.Column, BuilderArguments.Name(name));
    public RemoveFromTableBuilder ForeignKey(string name) => Remove(RemoveKind.ForeignKey, BuilderArguments.Name(name));
    public RemoveFromTableBuilder Constraint(string name) => Remove(RemoveKind.Constraint, BuilderArguments.Name(name));
    public RemoveFromTableBuilder PrimaryKey() => Remove(RemoveKind.PrimaryKey);
    public RemoveFromTableBuilder DefaultValue(string column) => Remove(RemoveKind.Default, BuilderArguments.Name(column));
    public RemoveFromTableBuilder Index(string name) => Remove(RemoveKind.Index, BuilderArguments.Name(name));
    public RemoveFromTableBuilder AllIndexes() => Remove(RemoveKind.AllIndexes);
    public RemoveFromTableBuilder AllConstraints() => Remove(RemoveKind.AllConstraints);
    public RemoveFromTableBuilder ForeignKeysForColumn(string column) => Remove(RemoveKind.ForeignKeysForColumn, BuilderArguments.Name(column));
    private RemoveFromTableBuilder Remove(RemoveKind kind, string name = null) => new(builder, kind, name);
    public DeleteDataBuilder FromTable(string table) => new(builder, BuilderArguments.Name(table));
}

public sealed class RemoveFromTableBuilder
{
    private readonly PendingOperation pending;
    private readonly RemoveKind kind;
    private readonly string name;
    internal RemoveFromTableBuilder(MigrationBuilder builder, RemoveKind kind, string name)
    {
        this.kind = kind; this.name = name;
        pending = new PendingOperation(builder, $"Delete {kind} requires FromTable(...)");
    }
    public void FromTable(string table)
    {
        BuilderArguments.Name(table);
        pending.Complete(() => new RemoveOperation(kind, table, name));
    }
}

public sealed class RenameRoot(MigrationBuilder builder)
{
    public RenameToBuilder Table(string name)
    {
        BuilderArguments.Name(name);
        return new(new PendingOperation(builder, $"Rename table '{name}' requires To(...)"), name, null);
    }
    public OnTableBuilder<RenameToBuilder> Column(string name)
    {
        BuilderArguments.Name(name);
        var pending = new PendingOperation(builder, $"Rename column '{name}' requires OnTable(...).To(...)");
        return new(table => new RenameToBuilder(pending, table, name));
    }
}

public sealed class RenameToBuilder
{
    private readonly PendingOperation pending;
    private readonly string table, column;
    internal RenameToBuilder(PendingOperation pending, string table, string column)
    { this.pending = pending; this.table = table; this.column = column; }
    public void To(string name)
    {
        BuilderArguments.Name(name);
        pending.Complete(() => new RenameOperation(table, name, column));
    }
}
