using System.Linq;

namespace DotNetProjects.Migrator.Framework.Fluent;

public sealed record InsertQueryOperation(string Target, string[] Columns, SelectQuery Query) : MigrationOperation
{
    public override void Apply(ITransformationProvider p) => p.InsertFromQuery(Target, Columns, Query);
    public override void Validate(ITransformationProvider p) { if (p is not Providers.NoOpTransformationProvider) ProjectedData.InsertSql(new SqlExpressionContext(p), Target, Columns, Query); }
    public override string ToSql(SqlGenerationContext c) => ProjectedData.InsertSql(new SqlExpressionContext(c.Dialect), Target, Columns, Query) + ";";
}
public static class ProjectedDataBuilder
{
    public static void InsertFromQuery(this MigrationBuilder builder, string target, string[] targetColumns, SelectQuery query) =>
        builder.Add(new InsertQueryOperation(target, targetColumns.ToArray(), query));
}
