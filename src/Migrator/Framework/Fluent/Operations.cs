using System;
using System.Collections.Generic;
using System.Data;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using System.Linq;
using DotNetProjects.Migrator.Providers;
using Index = DotNetProjects.Migrator.Framework.Index;
namespace DotNetProjects.Migrator.Framework.Fluent;

public abstract record MigrationOperation
{
    public abstract void Apply(ITransformationProvider provider);
    public virtual bool RequiresNoTransaction => false;
    public virtual void Validate(ITransformationProvider provider)
    {
        if (RequiresNoTransaction && provider is TransformationProvider { HasActiveTransaction: true })
            throw new MigrationException("Database administration requires an explicit no-transaction run.");
    }
    public virtual MigrationOperation Reverse() => throw new IrreversibleMigrationException();
    public virtual void ValidateReverse(ITransformationProvider provider) => _ = Reverse();
    public virtual string ToSql(SqlGenerationContext context) => throw new NotSupportedException($"SQL preview is not supported for {GetType().Name}.");
}

public sealed record CreateTableOperation(string Table, string Engine, IDbField[] Fields) : MigrationOperation
{
    public override void Apply(ITransformationProvider p)
    {
        var fields = Fields.Select(Definitions.Copy).ToArray();
        if (Engine == null) p.AddTable(Table, fields); else p.AddTable(Table, Engine, fields);
    }
    public override MigrationOperation Reverse() => new RemoveOperation(RemoveKind.Table, Table);
    public override string ToSql(SqlGenerationContext c)
    {
        if (Engine != null || Fields.Any(f => f is not (Column or PrimaryKeyConstraint or UniqueConstraint or CheckConstraint))) throw new NotSupportedException("This table contains an unsupported preview definition.");
        var columns = Fields.OfType<Column>().Select(Definitions.CopyColumn).ToArray();
        var primary = Fields.OfType<PrimaryKeyConstraint>().SingleOrDefault();
        if (primary != null)
        {
            if (columns.Any(x => x.IsPrimaryKey)) throw new MigrationException("Do not combine primary-key flags and constraints.");
            foreach (var column in columns.Where(x => primary.KeyColumns.Contains(x.Name)))
                column.ColumnProperty = (column.ColumnProperty & ~ColumnProperty.Null) | ColumnProperty.NotNull;
            if (c.Provider == ProviderTypes.SQLite && columns.Any(x => x.IsIdentity))
                throw new NotSupportedException("Named SQLite identity-key preview requires the complete table generator.");
        }
        var pks = columns.Where(x => x.IsPrimaryKey).ToArray();
        if (pks.Length > 1) foreach (var column in pks) column.ColumnProperty &= ~ColumnProperty.PrimaryKey;
        var definitions = columns.Select(c.Column).ToList();
        if (pks.Length > 1) definitions.Add($"PRIMARY KEY ({string.Join(", ", pks.Select(x => c.Quote(x.Name)))})");
        definitions.AddRange(Fields.OfType<TableConstraint>().Select(c.Dialect.GetTableConstraintSql));
        c.AddTable(Table, columns);
        return $"CREATE TABLE {c.Table(Table)} ({string.Join(", ", definitions)});";
    }
}
public sealed record ColumnOperation(string Table, Column Column, bool Alter = false) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) { if (Alter) p.ChangeColumn(Table, Definitions.CopyColumn(Column)); else p.AddColumn(Table, Definitions.CopyColumn(Column)); }
    public override MigrationOperation Reverse() => Alter ? base.Reverse() : new RemoveOperation(RemoveKind.Column, Table, Column.Name);
    public override string ToSql(SqlGenerationContext c)
    {
        c.RequireTable(Table);
        if (Alter) throw new NotSupportedException("Altering columns needs provider-specific schema inspection; use explicit SQL preview.");
        c.AddColumn(Table, Column);
        return $"ALTER TABLE {c.Table(Table)} ADD {c.Column(Column)};";
    }
}
public enum RemoveKind { Table, Column, ForeignKey, Constraint, PrimaryKey, Default, Index, AllIndexes, AllConstraints, ForeignKeysForColumn, Truncate }
public sealed record RemoveOperation(RemoveKind Kind, string Table, string Name = null) : MigrationOperation
{
    public override void Apply(ITransformationProvider p)
    {
        switch (Kind)
        {
            case RemoveKind.Table: p.RemoveTable(Table); break;
            case RemoveKind.Column: p.RemoveColumn(Table, Name); break;
            case RemoveKind.ForeignKey: p.RemoveForeignKey(Table, Name); break;
            case RemoveKind.Constraint: p.RemoveConstraint(Table, Name); break;
            case RemoveKind.PrimaryKey: p.RemovePrimaryKey(Table); break;
            case RemoveKind.Default: p.RemoveColumnDefaultValue(Table, Name); break;
            case RemoveKind.Index: p.RemoveIndex(Table, Name); break;
            case RemoveKind.AllIndexes: p.RemoveAllIndexes(Table); break;
            case RemoveKind.AllConstraints: p.RemoveAllConstraints(Table); break;
            case RemoveKind.ForeignKeysForColumn: p.RemoveAllForeignKeys(Table, Name); break;
            case RemoveKind.Truncate: p.TruncateTable(Table); break;
            default: throw new ArgumentOutOfRangeException();
        }
    }
    public override string ToSql(SqlGenerationContext c)
    {
        c.RequireTable(Table);
        if (Kind == RemoveKind.Table) { c.RemoveTable(Table); return $"DROP TABLE {c.Table(Table)};"; }
        throw new NotSupportedException($"{Kind} requires provider-specific preview support.");
    }
}
public sealed record RenameOperation(string Table, string NewName, string Column = null) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) { if (Column == null) p.RenameTable(Table, NewName); else p.RenameColumn(Table, Column, NewName); }
    public override MigrationOperation Reverse() => Column == null ? new RenameOperation(NewName, Table) : new RenameOperation(Table, Column, NewName);
    public override string ToSql(SqlGenerationContext c)
    {
        c.RequireTable(Table);
        if (c.Provider is not (ProviderTypes.SQLite or ProviderTypes.PostgreSQL)) return base.ToSql(c);
        if (Column == null) { c.RenameTable(Table, NewName); return $"ALTER TABLE {c.Table(Table)} RENAME TO {c.Quote(NewName)};"; }
        c.RenameColumn(Table, Column, NewName);
        return $"ALTER TABLE {c.Table(Table)} RENAME COLUMN {c.Quote(Column)} TO {c.Quote(NewName)};";
    }
}
public enum ConstraintKind { PrimaryKey, NonClusteredPrimaryKey, Unique, Check, ForeignKey }
public sealed record ConstraintOperation(ConstraintKind Kind, string Table, string Name, string[] Columns, string ParentTable = null,
    string[] ParentColumns = null, string Check = null, ForeignKeyConstraintType OnDelete = ForeignKeyConstraintType.NoAction,
    ForeignKeyConstraintType OnUpdate = ForeignKeyConstraintType.NoAction) : MigrationOperation
{
    public override void Apply(ITransformationProvider p)
    {
        switch (Kind)
        {
            case ConstraintKind.PrimaryKey: p.AddPrimaryKey(Name, Table, Columns); break;
            case ConstraintKind.NonClusteredPrimaryKey: p.AddPrimaryKeyNonClustered(Name, Table, Columns); break;
            case ConstraintKind.Unique: p.AddUniqueConstraint(Name, Table, Columns); break;
            case ConstraintKind.Check: p.AddCheckConstraint(Name, Table, Check); break;
            case ConstraintKind.ForeignKey:
                if (p is not IForeignKeyActions actions) throw new NotSupportedException("Provider must implement IForeignKeyActions.");
                actions.AddForeignKey(Name, Table, Columns, ParentTable, ParentColumns, OnDelete, OnUpdate); break;
        }
    }
    public override MigrationOperation Reverse() => new RemoveOperation(Kind is ConstraintKind.PrimaryKey or ConstraintKind.NonClusteredPrimaryKey ? RemoveKind.PrimaryKey : Kind == ConstraintKind.ForeignKey ? RemoveKind.ForeignKey : RemoveKind.Constraint, Table, Name);
}
public sealed record IndexOperation(string Table, Index Index) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => p.AddIndex(Table, (Index)Definitions.Copy(Index));
    public override MigrationOperation Reverse() => new RemoveOperation(RemoveKind.Index, Table, Index.Name);
    public override string ToSql(SqlGenerationContext c)
    {
        c.RequireTable(Table);
        if (Index.FilterItems.Count != 0 || Index.IncludeColumns.Length != 0 || Index.Clustered) return base.ToSql(c);
        return $"CREATE {(Index.Unique ? "UNIQUE " : "")}INDEX {c.Quote(Index.Name)} ON {c.Table(Table)} ({string.Join(", ", Index.KeyColumns.Select(c.Quote))});";
    }
}
public enum DataKind { Insert, InsertIfMissing, Update, Delete }
public sealed record DataOperation(DataKind Kind, string Table, string[] Columns, object[] Values, string[] WhereColumns = null, object[] WhereValues = null, string WhereSql = null) : MigrationOperation
{
    public override void Apply(ITransformationProvider p)
    {
        switch (Kind)
        {
            case DataKind.Insert: p.Insert(Table, Columns, Values); break;
            case DataKind.InsertIfMissing: p.InsertIfNotExists(Table, Columns, Values, WhereColumns, WhereValues); break;
            case DataKind.Update:
                if (WhereSql != null) p.Update(Table, Columns, Values, WhereSql);
                else p.Update(Table, Columns, Values, WhereColumns ?? Array.Empty<string>(), WhereValues ?? Array.Empty<object>()); break;
            case DataKind.Delete:
                if ((Columns?.Length ?? 0) != 0 || (Values?.Length ?? 0) != 0) throw new InvalidOperationException("Delete does not accept row values; use WhereColumns and WhereValues.");
                if (WhereSql != null) throw new NotSupportedException("Delete requires structured Where columns/values.");
                p.Delete(Table, WhereColumns, WhereValues); break;
        }
    }
    public override string ToSql(SqlGenerationContext c)
    {
        c.RequireTable(Table);
        if (Kind == DataKind.Insert) return $"INSERT INTO {c.Table(Table)} ({string.Join(", ", Columns.Select(c.Quote))}) VALUES ({string.Join(", ", Values.Select(c.Literal))});";
        return base.ToSql(c);
    }
}
public sealed record SqlOperation(string Sql, int? Timeout = null, object[] Parameters = null) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => p.ExecuteNonQuery(Sql, Timeout ?? p.CommandTimeout ?? 30, Parameters);
    public override string ToSql(SqlGenerationContext c)
    {
        if (Parameters != null && Parameters.Length != 0) throw new NotSupportedException("Parameterized raw SQL cannot be exported as an executable script.");
        c.InvalidateSchema();
        return Sql.TrimEnd().TrimEnd(';') + ";";
    }
}
public sealed record ScriptOperation(string Sql) : MigrationOperation
{
    public override void Apply(ITransformationProvider provider) => provider.ExecuteSqlScript(Sql);
    public override string ToSql(SqlGenerationContext context)
    {
        if (context.Dialect is Providers.Impl.SqlServer.SqlServerDialect) _ = SqlScriptBatches.SplitSqlServer(Sql);
        context.InvalidateSchema();
        return Sql.TrimEnd() + Environment.NewLine;
    }
}
public sealed record CallbackOperation(string Description, Action<ITransformationProvider> Action) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => Action(p);
}
public sealed record ReversibleOperation(MigrationOperation Forward, MigrationOperation Backward) : MigrationOperation
{
    public override void Validate(ITransformationProvider p) => Forward.Validate(p);
    public override void Apply(ITransformationProvider p) => Forward.Apply(p);
    public override MigrationOperation Reverse() => new ReversibleOperation(Backward, Forward);
    public override string ToSql(SqlGenerationContext c) => Forward.ToSql(c);
}
public sealed record ConditionalOperation(string Provider, MigrationOperation Operation) : MigrationOperation
{
    public override void Validate(ITransformationProvider p) { if (p.IsThisProvider(Provider)) Operation.Validate(p); }
    public override void Apply(ITransformationProvider p) { if (p.IsThisProvider(Provider)) Operation.Apply(p); }
    public override void ValidateReverse(ITransformationProvider p) { if (p.IsThisProvider(Provider)) Operation.ValidateReverse(p); }
    public override MigrationOperation Reverse() => new ConditionalReverseOperation(Provider, Operation);
    public override string ToSql(SqlGenerationContext c) => c.Dialect.GetType().Name.StartsWith(Provider, StringComparison.OrdinalIgnoreCase) ? Operation.ToSql(c) : "";
}
public sealed record ConditionalReverseOperation(string Provider, MigrationOperation Forward) : MigrationOperation
{
    public override void Validate(ITransformationProvider p) { if (p.IsThisProvider(Provider)) Forward.Reverse().Validate(p); }
    public override void Apply(ITransformationProvider p) { if (p.IsThisProvider(Provider)) Forward.Reverse().Apply(p); }
    public override MigrationOperation Reverse() => new ConditionalOperation(Provider, Forward);
    public override string ToSql(SqlGenerationContext c) => c.Dialect.GetType().Name.StartsWith(Provider, StringComparison.OrdinalIgnoreCase) ? Forward.Reverse().ToSql(c) : "";
}
public enum DatabaseOperationKind { Create, Drop, Switch, KillConnections }
public sealed record DatabaseOperation(DatabaseOperationKind Kind, string Name) : MigrationOperation
{
    public override bool RequiresNoTransaction => true;
    public override void Apply(ITransformationProvider p)
    {
        if (p is TransformationProvider { HasActiveTransaction: true })
            throw new MigrationException("Database administration requires an explicit no-transaction run.");
        switch (Kind)
        {
            case DatabaseOperationKind.Create: p.CreateDatabases(Name); break;
            case DatabaseOperationKind.Drop: p.DropDatabases(Name); break;
            case DatabaseOperationKind.Switch: p.SwitchDatabase(Name); break;
            case DatabaseOperationKind.KillConnections: p.KillDatabaseConnections(Name); break;
        }
    }
}
public sealed record CopyDataOperation(string Source, string[] SourceColumns, string Target, string[] TargetColumns, string[] OrderBy) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => p.CopyDataFromTableToTable(Source, SourceColumns.ToList(), Target, TargetColumns.ToList(), OrderBy?.ToList());
}
public sealed record UpdateFromOperation(string Source, string Target, DotNetProjects.Migrator.Framework.Models.ColumnPair[] Copy, DotNetProjects.Migrator.Framework.Models.ColumnPair[] Match) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => p.UpdateTargetFromSource(Source, Target, Copy.Select(Definitions.CopyPair).ToArray(), Match.Select(Definitions.CopyPair).ToArray());
}
public sealed record ViewOperation(string Name, string Table, IViewField[] Fields, IViewElement[] Elements = null) : MigrationOperation
{
    public override void Apply(ITransformationProvider p)
    {
        if (Elements != null) p.AddView(Name, Table, Elements.Select(Definitions.CopyViewElement).ToArray());
        else p.AddView(Name, Table, Fields.Select(Definitions.CopyViewField).ToArray());
    }
}
public static class Definitions
{
    public static DotNetProjects.Migrator.Framework.Models.ColumnPair CopyPair(DotNetProjects.Migrator.Framework.Models.ColumnPair p) => new() { ColumnNameSource = p.ColumnNameSource, ColumnNameTarget = p.ColumnNameTarget };
    public static IViewField CopyViewField(IViewField f) => new ViewField(f.ColumnName, f.TableName, f.KeyColumnName, f.ParentTableName, f.ParentKeyColumnName);
    public static IViewElement CopyViewElement(IViewElement e) => e switch
    {
        ViewColumn c => new ViewColumn(c.Prefix, c.ColumnName),
        ViewJoin j => new ViewJoin(j.TableName, j.TableAlias, j.ColumnName, j.ParentTableName, j.ParentTableAlias, j.ParentColumnName, j.JoinType),
        _ => throw new NotSupportedException("Unknown view element.")
    };
    public static Column CopyColumn(Column c) => new(c.Name, c.Type, c.Size, c.ColumnProperty, c.DefaultValue is byte[] b ? b.Clone() : c.DefaultValue) { Precision = c.Precision, Scale = c.Scale, MigratorDbType = c.MigratorDbType };
    public static IDbField Copy(IDbField field) => field switch
    {
        Column c => CopyColumn(c),
        Index i => new Index { Name = i.Name, Unique = i.Unique, Clustered = i.Clustered, KeyColumns = (string[])i.KeyColumns.Clone(), IncludeColumns = (string[])i.IncludeColumns.Clone(), FilterItems = i.FilterItems.Select(f => new DotNetProjects.Migrator.Providers.Models.Indexes.FilterItem { ColumnName = f.ColumnName, Filter = f.Filter, Value = f.Value }).ToList() },
        ForeignKeyConstraint f => new ForeignKeyConstraint(f.Name, f.ParentTable, (string[])f.ParentColumns.Clone(), f.ChildTable, (string[])f.ChildColumns.Clone()) { OnDelete = f.OnDelete, OnUpdate = f.OnUpdate, Match = f.Match, Id = f.Id },
        PrimaryKeyConstraint k => new PrimaryKeyConstraint(k.Name, k.KeyColumns) { NonClustered = k.NonClustered },
        UniqueConstraint u => new UniqueConstraint { Name = u.Name, KeyColumns = (string[])u.KeyColumns.Clone() },
        CheckConstraint c => new CheckConstraint(c.Name, c.CheckConstraintString),
        _ => throw new NotSupportedException($"Cannot snapshot {field.GetType().Name}.")
    };
}
