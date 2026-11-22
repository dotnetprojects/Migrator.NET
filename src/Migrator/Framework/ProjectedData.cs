using System;
using System.Linq;

namespace DotNetProjects.Migrator.Framework;

public static class ProjectedData
{
    /// <summary>Insert one server-side SELECT with an explicit ordered target mapping, in the active transaction.</summary>
    public static int InsertFromQuery(this ITransformationProvider p, string target, string[] targetColumns, SelectQuery query)
    {
        if (p is Providers.NoOpTransformationProvider) return 0;
        var c = new SqlExpressionContext(p);
        var sql = InsertSql(c, target, targetColumns, query);
        return p.ExecuteNonQuery(sql, p.CommandTimeout ?? 30, c.Parameters.ToArray());
    }
    internal static string InsertSql(SqlExpressionContext c, string target, string[] columns, SelectQuery query)
    {
        ArgumentNullException.ThrowIfNull(query); ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length != query.Columns.Count || columns.Length == 0 || columns.Any(string.IsNullOrWhiteSpace) || columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw new ArgumentException("Target columns must be distinct and match the ordered projection count.");
        return "INSERT INTO " + c.Table(target) + " (" + string.Join(", ", columns.Select(c.Dialect.QuoteColumnNameIfRequired)) + ") " + query.ToSql(c);
    }
}
