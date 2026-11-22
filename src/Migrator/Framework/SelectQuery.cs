using System;
using System.Collections.Generic;
using System.Linq;

namespace DotNetProjects.Migrator.Framework;

public sealed record SelectItem(SqlExpression Expression, string Alias);
public sealed record QueryJoin(string Table, string Alias, SqlExpression On, bool Left = false);
public sealed record QueryOrder(SqlExpression Expression, bool Descending = false);

/// <summary>A server-side SELECT shared by views and data migrations. Projection order is explicit.</summary>
public sealed class SelectQuery
{
    public string Table { get; }
    public string Alias { get; }
    private readonly SelectItem[] columns;
    private readonly QueryJoin[] joins;
    private readonly QueryOrder[] orderBy;
    public int? Limit { get; }
    public IReadOnlyList<SelectItem> Columns => Array.AsReadOnly(columns);
    public SqlExpression Predicate { get; }
    public SelectQuery(string table, string alias, IEnumerable<SelectItem> columns, SqlExpression predicate = null, IEnumerable<QueryJoin> joins = null)
        : this(table, alias, columns, predicate, joins, null, null) { }
    public static SelectQuery Ordered(string table, string alias, IEnumerable<SelectItem> columns, IEnumerable<QueryOrder> orderBy,
        int? limit = null, SqlExpression predicate = null, IEnumerable<QueryJoin> joins = null)
        => new(table, alias, columns, predicate, joins, orderBy, limit);
    private SelectQuery(string table, string alias, IEnumerable<SelectItem> columns, SqlExpression predicate, IEnumerable<QueryJoin> joins,
        IEnumerable<QueryOrder> orderBy, int? limit)
    {
        if ((table != null || alias != null) && (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(alias))) throw new ArgumentException("A source table and alias are required together.");
        Table = table; Alias = alias; this.columns = columns?.ToArray() ?? throw new ArgumentNullException(nameof(columns));
        this.joins = joins?.ToArray() ?? []; Predicate = predicate;
        this.orderBy = orderBy?.ToArray() ?? []; Limit = limit;
        if (limit is <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        if (this.orderBy.Any(x => x?.Expression == null)) throw new ArgumentException("Order expressions must not be null.");
        if (table == null && this.joins.Length > 0) throw new ArgumentException("Joins require a source table.");
        if (this.columns.Length == 0 || this.columns.Any(x => x?.Expression == null || string.IsNullOrWhiteSpace(x.Alias))) throw new ArgumentException("Each projected expression requires an output alias.");
        if (this.columns.Select(x => x.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count() != this.columns.Length) throw new ArgumentException("Output aliases must be distinct.");
        if (this.joins.Any(j => j?.On == null || string.IsNullOrWhiteSpace(j.Alias) || string.IsNullOrWhiteSpace(j.Table))) throw new ArgumentException("Each join requires a table, alias and predicate.");
        if (this.joins.Select(x => x.Alias).Append(alias).Distinct(StringComparer.OrdinalIgnoreCase).Count() != this.joins.Length + 1) throw new ArgumentException("Source aliases must be distinct.");
    }
    public string ToSql(IDialect dialect) => ToSql(new SqlExpressionContext(dialect));
    internal string ToSql(SqlExpressionContext c)
    {
        var d = c.Dialect;
        var prefix = Limit == null ? "" : d switch
        {
            Providers.Impl.SqlServer.SqlServerDialect or Providers.Impl.Sybase.SybaseDialect => "TOP " + Limit + " ",
            Providers.Impl.Firebird.FirebirdDialect or Providers.Impl.Informix.InformixDialect or Providers.Impl.Ingres.IngresDialect => "FIRST " + Limit + " ",
            _ => ""
        };
        var from = Table == null ? d switch
        {
            Providers.Impl.Oracle.OracleDialect => " FROM DUAL", Providers.Impl.Firebird.FirebirdDialect => " FROM RDB$DATABASE",
            Providers.Impl.DB2.DB2Dialect => " FROM SYSIBM.SYSDUMMY1", Providers.Impl.Hana.HanaDialect => " FROM DUMMY",
            Providers.Impl.Informix.InformixDialect => " FROM systables WHERE tabid=1",
            Providers.Impl.Ingres.IngresDialect => throw new NotSupportedException("Constant-only SELECT source is not implemented for Ingres."),
            _ => ""
        } : " FROM " + c.Table(Table) + " " + c.Qualifier(Alias);
        if (Table == null && d is Providers.Impl.Informix.InformixDialect && Predicate != null) throw new NotSupportedException("Use a named source for filtered Informix constant queries.");
        var sql = "SELECT " + prefix + string.Join(", ", columns.Select(x => x.Expression.ToSql(c) + " AS " + d.QuoteIdentifier(x.Alias)))
            + from
            + string.Concat(joins.Select(j => (j.Left ? " LEFT JOIN " : " INNER JOIN ") + c.Table(j.Table) + " " + c.Qualifier(j.Alias) + " ON " + j.On.ToSql(c)))
            + (Predicate == null ? "" : " WHERE " + Predicate.ToSql(c))
            + (orderBy.Length == 0 ? "" : " ORDER BY " + string.Join(", ", orderBy.Select(x => x.Expression.ToSql(c) + (x.Descending ? " DESC" : " ASC"))));
        if (Limit != null && prefix.Length == 0)
            sql += d is Providers.Impl.Oracle.OracleDialect or Providers.Impl.DB2.DB2Dialect ? " FETCH FIRST " + Limit + " ROWS ONLY" : " LIMIT " + Limit;
        return sql;
    }
}
