using System;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;

namespace DotNetProjects.Migrator.Providers;

internal static class DuplicateRowDeletion
{
    internal static bool Supports(IDialect dialect) => dialect is SQLiteDialect or PostgreSQLDialect or OracleDialect or SqlServerDialect;

    internal static void Validate(string table, string[] keys, DuplicateRowRetention keep, DuplicateNullHandling nulls)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Length == 0 || keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
            throw new ArgumentException("Duplicate keys must be distinct, non-empty column names.", nameof(keys));
        if (keep != DuplicateRowRetention.Any) throw new ArgumentOutOfRangeException(nameof(keep));
        if (nulls is not (DuplicateNullHandling.Equal or DuplicateNullHandling.ExcludeNullKeys)) throw new ArgumentOutOfRangeException(nameof(nulls));
    }

    internal static int Execute(TransformationProvider provider, string table, string[] keys, DuplicateRowRetention keep, DuplicateNullHandling nulls)
    {
        Validate(table, keys, keep, nulls);
        if (!Supports(provider.Dialect)) throw new NotSupportedException("Duplicate-row deletion supports SQLite, PostgreSQL, Oracle and SQL Server.");
        if (!provider.TableExists(table)) throw new MigrationException("Table does not exist: " + table);
        var columns = provider.GetColumns(table).Select(column => column.Name).ToArray();
        if (keys.Any(key => !columns.Contains(key, StringComparer.OrdinalIgnoreCase)))
            throw new MigrationException("A duplicate key column does not exist.");
        var quotedTable = provider.QuoteTableNameIfRequired(table);
        var quotedKeys = keys.Select(provider.QuoteColumnNameIfRequired).ToArray();

        if (provider.Dialect is SqlServerDialect)
        {
            var ordinal = "__migrator_duplicate_ordinal";
            while (columns.Contains(ordinal, StringComparer.OrdinalIgnoreCase)) ordinal += "_";
            var cte = "__migrator_duplicates";
            while (table.Contains(cte, StringComparison.OrdinalIgnoreCase)) cte += "_";
            var filter = nulls == DuplicateNullHandling.ExcludeNullKeys
                ? " WHERE " + string.Join(" AND ", quotedKeys.Select(key => key + " IS NOT NULL")) : "";
            return provider.ExecuteNonQuery($"WITH {cte} AS (SELECT {string.Join(", ", quotedKeys)}, ROW_NUMBER() OVER (PARTITION BY {string.Join(", ", quotedKeys)} ORDER BY (SELECT NULL)) AS {ordinal} FROM {quotedTable}{filter}) DELETE FROM {cte} WHERE {ordinal} > 1");
        }

        string rowOrder;
        var alias = "a";
        if (provider is SQLiteTransformationProvider sqlite)
        {
            var script = sqlite.GetSqlCreateTableScript(table);
            if (string.IsNullOrWhiteSpace(script) || SQLiteConstraintParser.HasKeyword(script, "WITHOUT") || SQLiteConstraintParser.HasKeyword(script, "VIRTUAL"))
                throw new NotSupportedException("Duplicate-row deletion requires an ordinary SQLite rowid table.");
            var rowid = new[] { "_rowid_", "rowid", "oid" }.FirstOrDefault(name => !columns.Contains(name, StringComparer.OrdinalIgnoreCase))
                ?? throw new NotSupportedException("All SQLite physical row identifiers are shadowed by declared columns.");
            rowOrder = $"b.{rowid} < a.{rowid}";
            alias = "AS a";
        }
        else if (provider.Dialect is PostgreSQLDialect)
        {
            // ctid alone is not unique across partitions or inherited child tables.
            rowOrder = "(b.tableoid, b.ctid) < (a.tableoid, a.ctid)";
        }
        else if (provider.Dialect is OracleDialect)
        {
            rowOrder = "b.ROWID < a.ROWID";
        }
        else throw new NotSupportedException("The provider does not expose the required physical row identity.");

        var equality = string.Join(" AND ", quotedKeys.Select(key => nulls == DuplicateNullHandling.Equal
            ? $"(b.{key} = a.{key} OR (b.{key} IS NULL AND a.{key} IS NULL))"
            : $"b.{key} = a.{key}"));
        return provider.ExecuteNonQuery($"DELETE FROM {quotedTable} {alias} WHERE EXISTS (SELECT 1 FROM {quotedTable} b WHERE {equality} AND {rowOrder})");
    }
}
