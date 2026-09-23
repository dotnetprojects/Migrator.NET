using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DotNetProjects.Migrator.Framework.Fluent;

/// <summary>Column options shared by table definitions, additions and alterations.</summary>
public abstract class ColumnDefinitionBuilder<T> where T : ColumnDefinitionBuilder<T>
{
    private readonly Column column;
    private bool hasType;
    private T Self => (T)this;

    internal ColumnDefinitionBuilder(string name) => column = new Column(BuilderArguments.Name(name));

    internal Column BuildColumn()
    {
        if (!hasType) throw new InvalidOperationException($"Specify a type for column '{column.Name}' using As... or OfType(...).");
        return Definitions.CopyColumn(column);
    }

    public T OfType(DbType type) { column.Type = type; hasType = true; return Self; }
    public T OfType(MigratorDbType type) { column.MigratorDbType = type; hasType = true; return Self; }
    public T AsInt16() => OfType(DbType.Int16);
    public T AsInt32() => OfType(DbType.Int32);
    public T AsInt64() => OfType(DbType.Int64);
    public T AsString(int size = 255) => OfType(DbType.String).WithSize(size);
    public T AsAnsiString(int size = 255) => OfType(DbType.AnsiString).WithSize(size);
    public T AsGuid() => OfType(DbType.Guid);
    public T AsBoolean() => OfType(DbType.Boolean);
    public T AsDate() => OfType(DbType.Date);
    public T AsDateTime() => OfType(DbType.DateTime);
    public T AsDateTime2() => OfType(DbType.DateTime2);
    public T AsDateTimeOffset() => OfType(DbType.DateTimeOffset);
    public T AsDecimal(int precision, int scale) => OfType(DbType.Decimal).WithPrecision(precision, scale);
    public T AsDouble() => OfType(DbType.Double);
    public T AsBinary(int size) => OfType(DbType.Binary).WithSize(size);
    public T WithSize(int size) { column.Size = size; return Self; }
    public T WithPrecision(int precision, int scale)
    {
        if (precision <= 0) throw new ArgumentOutOfRangeException(nameof(precision));
        if (scale < 0 || scale > precision) throw new ArgumentOutOfRangeException(nameof(scale));
        column.Precision = precision; column.Scale = scale; return Self;
    }
    public T WithDefaultValue(object value) { column.DefaultValue = value is byte[] bytes ? bytes.Clone() : value; return Self; }
    public T NotNullable() { column.IsNullable = false; return Self; }
    public T Nullable() { column.IsNullable = true; return Self; }
    public T Unsigned() { column.IsUnsigned = true; return Self; }
    public T WithCollation(Collation collation) { column.Collation = collation; return Self; }
    public T Identity() { column.IsIdentity = true; return Self; }
}

public sealed class ColumnBuilder : ColumnDefinitionBuilder<ColumnBuilder>
{
    internal ColumnBuilder(string name) : base(name) { }
}

public sealed class TableBuilder
{
    private readonly List<Func<IDbField>> fields = new();
    private string engine;

    internal TableBuilder(MigrationBuilder builder, string name)
    {
        BuilderArguments.Name(name);
        builder.Add(() => new CreateTableOperation(name, engine, fields.Select(field => field()).ToArray()));
    }

    public TableColumnBuilder WithColumn(string name)
    {
        var column = new TableColumnBuilder(this, name);
        fields.Add(column.BuildColumn);
        return column;
    }
    public TableBuilder WithFields(params IDbField[] values)
    {
        foreach (var value in values.Select(Definitions.Copy)) fields.Add(() => Definitions.Copy(value));
        return this;
    }
    public TableBuilder WithPrimaryKey(string name, params string[] columns)
        => WithFields(new PrimaryKeyConstraint(BuilderArguments.Name(name), BuilderArguments.Names(columns)));
    public TableBuilder WithUniqueConstraint(string name, params string[] columns)
        => WithFields(new UniqueConstraint(BuilderArguments.Name(name), BuilderArguments.Names(columns)));
    public TableBuilder WithCheckConstraint(string name, string expression)
        => WithFields(new CheckConstraint(BuilderArguments.Name(name), BuilderArguments.Name(expression)));
    public TableBuilder WithEngine(string value) { engine = BuilderArguments.Name(value); return this; }
}

/// <summary>Options for one specific column, followed by the next table definition.</summary>
public sealed class TableColumnBuilder : ColumnDefinitionBuilder<TableColumnBuilder>
{
    private readonly TableBuilder table;
    internal TableColumnBuilder(TableBuilder table, string name) : base(name) => this.table = table;
    public TableColumnBuilder WithColumn(string name) => table.WithColumn(name);
    public TableBuilder WithFields(params IDbField[] fields) => table.WithFields(fields);
    public TableBuilder WithPrimaryKey(string name, params string[] columns) => table.WithPrimaryKey(name, columns);
    public TableBuilder WithUniqueConstraint(string name, params string[] columns) => table.WithUniqueConstraint(name, columns);
    public TableBuilder WithCheckConstraint(string name, string expression) => table.WithCheckConstraint(name, expression);
    public TableBuilder WithEngine(string engine) => table.WithEngine(engine);
}

public sealed class AlterRoot(MigrationBuilder builder)
{
    public OnTableBuilder<ColumnBuilder> Column(string name) => ColumnExpression.Start(builder, name, true);

    /// <summary>Uses a complete existing column definition, copied at authoring time.</summary>
    public ColumnDefinitionOnTableBuilder Column(Column definition) => new(builder, definition, true);
}

public sealed class ColumnDefinitionOnTableBuilder
{
    private readonly PendingOperation pending;
    private readonly Column column;
    private readonly bool alter;
    internal ColumnDefinitionOnTableBuilder(MigrationBuilder builder, Column definition, bool alter)
    {
        column = Definitions.CopyColumn(definition);
        BuilderArguments.Name(column.Name);
        this.alter = alter;
        pending = new PendingOperation(builder, $"Column '{column.Name}' requires OnTable(...)");
    }
    public void OnTable(string table)
    {
        BuilderArguments.Name(table);
        pending.Complete(() => new ColumnOperation(table, Definitions.CopyColumn(column), alter));
    }
}

internal static class ColumnExpression
{
    internal static OnTableBuilder<ColumnBuilder> Start(MigrationBuilder builder, string name, bool alter)
    {
        var column = new ColumnBuilder(name);
        var pending = new PendingOperation(builder, $"Column '{name}' requires OnTable(...)");
        return new(table =>
        {
            pending.Complete(() => new ColumnOperation(table, column.BuildColumn(), alter));
            return column;
        });
    }
}
