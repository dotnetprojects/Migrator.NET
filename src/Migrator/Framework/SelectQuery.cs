using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetProjects.Migrator.Framework;

public sealed record SelectItem(SqlExpression Expression, string Alias);
public sealed record QueryJoin(string Table, string Alias, SqlExpression On, bool Left = false);

/// <summary>A server-side SELECT shared by views and data migrations. Projection order is explicit.</summary>
public sealed class SelectQuery
{
    public string Table { get; }
    public string Alias { get; }
    private readonly SelectItem[] columns;
    private readonly QueryJoin[] joins;
    public IReadOnlyList<SelectItem> Columns => Array.AsReadOnly(columns);
    public SqlExpression Predicate { get; }
    public SelectQuery(string table, string alias, IEnumerable<SelectItem> columns, SqlExpression predicate = null, IEnumerable<QueryJoin> joins = null)
    {
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("A source table and alias are required.");
        Table = table; Alias = alias; this.columns = columns?.ToArray() ?? throw new ArgumentNullException(nameof(columns));
        this.joins = joins?.ToArray() ?? []; Predicate = predicate;
        if (this.columns.Length == 0 || this.columns.Any(x => x?.Expression == null || string.IsNullOrWhiteSpace(x.Alias))) throw new ArgumentException("Each projected expression requires an output alias.");
        if (this.columns.Select(x => x.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count() != this.columns.Length) throw new ArgumentException("Output aliases must be distinct.");
        if (this.joins.Any(j => j?.On == null || string.IsNullOrWhiteSpace(j.Alias) || string.IsNullOrWhiteSpace(j.Table))) throw new ArgumentException("Each join requires a table, alias and predicate.");
        if (this.joins.Select(x => x.Alias).Append(alias).Distinct(StringComparer.OrdinalIgnoreCase).Count() != this.joins.Length + 1) throw new ArgumentException("Source aliases must be distinct.");
    }
    public string ToSql(IDialect dialect) => ToSql(new SqlExpressionContext(dialect));
    internal string ToSql(SqlExpressionContext c)
    {
        var d = c.Dialect;
        return "SELECT " + string.Join(", ", columns.Select(x => x.Expression.ToSql(c) + " AS " + d.QuoteIdentifier(x.Alias)))
            + " FROM " + c.Table(Table) + " " + c.Qualifier(Alias)
            + string.Concat(joins.Select(j => (j.Left ? " LEFT JOIN " : " INNER JOIN ") + c.Table(j.Table) + " " + c.Qualifier(j.Alias) + " ON " + j.On.ToSql(c)))
            + (Predicate == null ? "" : " WHERE " + Predicate.ToSql(c));
    }
}
