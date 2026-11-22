using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed record GuardTriggerOperation(GuardTrigger Definition) : MigrationOperation
{
    public override void Validate(ITransformationProvider p)
    {
        // Validate the definition before any queued table DDL. Native HANA types
        // are read at Apply, when a table created earlier in this plan exists.
        if (p is not Providers.NoOpTransformationProvider)
            GuardTriggers.CreateSql(p.Dialect, Definition, p.QuoteTableNameIfRequired, p.Dialect is Providers.Impl.Hana.HanaDialect ? _ => "INTEGER" : null);
    }
    public override void Apply(ITransformationProvider p) => p.CreateGuardTrigger(Definition);
    public override MigrationOperation Reverse() => new DropTriggerOperation(Definition.Name, Definition.Table);
    public override string ToSql(SqlGenerationContext c)
    {
        var sql = string.Join(";\n", GuardTriggers.CreateSql(c.Dialect, Definition));
        c.ValidateTriggerUpdateColumns(Definition.Table, Definition.UpdateColumns);
        return c.Dialect is Providers.Impl.SqlServer.SqlServerDialect ? "EXEC(" + new SqlExpressionContext(c.Dialect).Literal(sql) + ");" : sql + ";";
    }
}
public sealed record DropTriggerOperation(string Name, string Table) : MigrationOperation
{
    public override void Validate(ITransformationProvider p) { if (p is not Providers.NoOpTransformationProvider) GuardTriggers.DropSql(p.Dialect, Name, p.QuoteTableNameIfRequired(Table)); }
    public override void Apply(ITransformationProvider p) => p.DropGuardTrigger(Name, Table);
    public override string ToSql(SqlGenerationContext c) => string.Join(";\n", GuardTriggers.DropSql(c.Dialect, Name, Table)) + ";";
}
