namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed record PortableCheckOperation(string Name, string Table, SqlExpression Expression) : MigrationOperation
{
    public override void Validate(ITransformationProvider p) { if (p is not Providers.NoOpTransformationProvider) Expression.ToSql(new SqlExpressionContext(p.Dialect)); }
    public override void Apply(ITransformationProvider p) => p.AddCheckConstraint(Name, Table, Expression);
    public override MigrationOperation Reverse() => new RemoveOperation(RemoveKind.Constraint, Table, Name);
    public override string ToSql(SqlGenerationContext c)
    {
        c.RequireTable(Table);
        if (c.Provider is Providers.ProviderTypes.SQLite or Providers.ProviderTypes.MonoSQLite)
            throw new System.NotSupportedException("SQLite adding CHECK requires a catalog-based table rebuild.");
        return "ALTER TABLE " + c.Table(Table) + (c.Provider == Providers.ProviderTypes.IBM_Informix ? " ADD CONSTRAINT " : " ADD ")
            + c.Dialect.GetTableConstraintSql(CheckConstraint.FromExpression(Name, Expression)) + ";";
    }
}
