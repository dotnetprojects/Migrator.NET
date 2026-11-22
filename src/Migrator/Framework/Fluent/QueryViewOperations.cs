namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed record QueryViewOperation(string Name, SelectQuery Query, bool Replace = false) : MigrationOperation
{
    public override void Validate(ITransformationProvider p) { if (p is not Providers.NoOpTransformationProvider) QueryViews.CreateSql(p.Dialect, QueryViews.ResolveName(p, Name), Query, p.QuoteTableNameIfRequired); }
    public override void Apply(ITransformationProvider p) { if (Replace) p.ReplaceView(Name, Query); else p.CreateView(Name, Query); }
    public override MigrationOperation Reverse() => Replace ? base.Reverse() : new DropViewOperation(Name);
    public override string ToSql(SqlGenerationContext c)
    {
        if (Replace) throw new System.NotSupportedException("Replacing a view requires lifecycle inspection.");
        var sql = QueryViews.CreateSql(c.Dialect, GuardTriggers.ResolveName(c.Dialect, c.Table(Name)), Query);
        return c.Dialect is Providers.Impl.SqlServer.SqlServerDialect
            ? "EXEC(" + new SqlExpressionContext(c.Dialect).Literal(sql) + ");" : sql + ";";
    }
}
public sealed record DropViewOperation(string Name, bool IfExists = false) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => p.DropView(Name, IfExists);
    public override string ToSql(SqlGenerationContext c) => IfExists ? throw new System.NotSupportedException("Conditional drop requires catalog inspection.") : "DROP VIEW " + GuardTriggers.ResolveName(c.Dialect, c.Table(Name)) + ";";
}
