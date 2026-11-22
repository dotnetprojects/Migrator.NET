using System;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Sybase;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using DotNetProjects.Migrator.Providers.Impl.Ingres;
using DotNetProjects.Migrator.Providers.Impl.Informix;

namespace DotNetProjects.Migrator.Framework;

public static class QueryViews
{
    public static void CreateView(this ITransformationProvider p, string name, SelectQuery query)
    {
        if (p is NoOpTransformationProvider) return;
        p.ExecuteNonQuery(CreateSql(p.Dialect, ResolveName(p, name), query, p.QuoteTableNameIfRequired));
    }
    internal static string ResolveName(ITransformationProvider p, string name)
    {
        var qualified = p.QuoteTableNameIfRequired(name);
        if (p.Dialect is SQLiteDialect && SqlIdentifier.Parse(qualified).Length == 1)
            return "main." + qualified;
        return qualified;
    }
    /// <summary>Drop fails on a missing view unless ifExists is true. Catalog lookup never deliberately raises a DDL error.</summary>
    public static void DropView(this ITransformationProvider p, string name, bool ifExists = false)
    {
        if (p is NoOpTransformationProvider) return;
        var qualified = ResolveName(p, name);
        if (ifExists && !p.ViewExists(qualified)) return;
        p.ExecuteNonQuery("DROP VIEW " + qualified);
    }
    public static void ReplaceView(this ITransformationProvider p, string name, SelectQuery query)
    {
        if (p is NoOpTransformationProvider) return;
        name = ResolveName(p, name);
        var sql = CreateSql(p.Dialect, name, query, p.QuoteTableNameIfRequired);
        if (p.Dialect is SQLiteDialect)
        {
            string savepoint = "view_" + Guid.NewGuid().ToString("N");
            p.ExecuteNonQuery("SAVEPOINT " + savepoint);
            try
            {
                p.DropView(name, true); p.ExecuteNonQuery(sql);
                // SQLite defers SELECT validation until the view is read.
                p.ExecuteScalar("SELECT * FROM " + name + " WHERE 1=0");
                p.ExecuteNonQuery("RELEASE " + savepoint);
            }
            catch { p.ExecuteNonQuery("ROLLBACK TO " + savepoint); p.ExecuteNonQuery("RELEASE " + savepoint); throw; }
            return;
        }
        if (p.Dialect is SqlServerDialect)
        {
            p.ExecuteNonQuery((p.ViewExists(name) ? "ALTER" : "CREATE") + sql[6..]); return;
        }
        if (p.Dialect is FirebirdDialect) { p.ExecuteNonQuery("CREATE OR ALTER" + sql[6..]); return; }
        if (p.Dialect is IngresDialect or InformixDialect)
        {
            // These engines use transactional DDL. The migration transaction owns rollback.
            bool own = p is TransformationProvider { HasActiveTransaction: false };
            if (own) p.BeginTransaction();
            try { p.DropView(name, true); p.ExecuteNonQuery(sql); if (own) p.Commit(); }
            catch { if (own) p.Rollback(); throw; }
            return;
        }
        p.ExecuteNonQuery("CREATE OR REPLACE" + sql[6..]);
    }
    internal static string CreateSql(IDialect d, string name, SelectQuery query, Func<string, string> tableResolver = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        return "CREATE VIEW " + d.QuoteTableNameIfRequired(name) + " AS " + query.ToSql(new SqlExpressionContext(d) { TableResolver = tableResolver });
    }
}
