using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

public partial class SQLiteTransformationProvider
{
    private static string SQLiteNameKey(string name) => string.Concat(name.Select(c => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c));

    private List<(int Index, string Name)> RebuildSchemas()
    {
        // Reading temp.sqlite_master also registers the temp database if needed.
        ExecuteScalar("SELECT COUNT(*) FROM temp.sqlite_master");
        var schemas = new List<(int, string)>();
        using var command = CreateCommand();
        using var reader = ExecuteQuery(command, "PRAGMA database_list");
        while (reader.Read()) schemas.Add((Convert.ToInt32(reader.GetValue(0)), reader.GetString(1)));
        return schemas;
    }

    private void PrepareRebuildStatement(string sql)
    {
        using var command = CreateCommand();
        command.CommandText = sql + " /* rebuild validation " + Guid.NewGuid().ToString("N") + " */";
        using var reader = command.ExecuteReader();
    }

    private bool CanPrepareRebuildStatement(string sql)
    {
        try { PrepareRebuildStatement(sql); return true; }
        catch (DbException) { return false; } // Preserve pre-existing invalid programs.
    }

    private List<string> PreparedTriggerProgramsBeforeRebuild()
    {
        var schemas = RebuildSchemas();
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var relations = new List<(string Schema, string Name)>();
        foreach (var schema in schemas)
        {
            var catalog = _dialect.QuoteIdentifier(schema.Name) + ".sqlite_master";
            foreach (var target in ExecuteStringQuery("SELECT DISTINCT tbl_name FROM " + catalog + " WHERE type='trigger'")) targets.Add(SQLiteNameKey(target));
            relations.AddRange(ExecuteStringQuery("SELECT name FROM " + catalog + " WHERE type IN ('table','view')").Select(name => (schema.Name, name)));
        }
        var programs = new List<string>();
        // Include every matching namespace, so an unqualified TEMP target never
        // requires guessing which same-named table it was originally bound to.
        foreach (var relation in relations.Where(r => targets.Contains(SQLiteNameKey(r.Name))))
        {
            var table = _dialect.QuoteIdentifier(relation.Schema) + "." + _dialect.QuoteIdentifier(relation.Name);
            var candidates = new List<string> { "EXPLAIN INSERT INTO " + table + " DEFAULT VALUES", "EXPLAIN DELETE FROM " + table };
            var columns = new List<string>();
            using (var command = CreateCommand())
            using (var reader = ExecuteQuery(command, Pragma("table_xinfo", table)))
                while (reader.Read())
                    if (Convert.ToInt32(reader.GetValue(6)) == 0) columns.Add(reader.GetString(1));
            if (columns.Count == 0) columns.AddRange(GetColumns(null, table));
            foreach (var alias in new[] { "rowid", "oid", "_rowid_" })
                if (!columns.Any(c => SQLiteNameKey(c) == alias) && CanPrepareRebuildStatement("SELECT " + alias + " FROM " + table + " LIMIT 0")) columns.Add(alias);
            if (columns.Count > 0)
                candidates.Add("EXPLAIN UPDATE " + table + " SET " + string.Join(", ", columns.Select(c => _dialect.QuoteIdentifier(c) + "=" + _dialect.QuoteIdentifier(c))));
            programs.AddRange(candidates.Where(CanPrepareRebuildStatement));
        }
        return programs;
    }

    private List<string> RebuildBindingStatements(string table, List<string> views, List<string> programs)
    {
        var schema = RebuildSchemas().Single(s => SQLiteNameKey(s.Name) == SQLiteNameKey(Namespace(table)));
        var roots = new HashSet<long>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, "SELECT rootpage FROM " + Catalog(table) + " WHERE tbl_name=" + ObjectSqlLiteral(table) + " COLLATE NOCASE AND rootpage>0"))
            while (reader.Read()) roots.Add(Convert.ToInt64(reader.GetValue(0)));
        var bindings = new List<string>();
        foreach (var view in views)
            if (StatementUsesRebuiltTable("SELECT * FROM " + view, schema.Index, roots)) bindings.Add("SELECT * FROM " + view + " LIMIT 0");
        bindings.AddRange(programs.Where(sql => StatementUsesRebuiltTable(sql, schema.Index, roots)));
        return bindings;
    }

    private bool StatementUsesRebuiltTable(string sql, int database, HashSet<long> roots)
    {
        using var command = CreateCommand();
        command.CommandText = (sql.StartsWith("EXPLAIN ", StringComparison.Ordinal) ? sql : "EXPLAIN " + sql) + " /* dependencies " + Guid.NewGuid().ToString("N") + " */";
        using var reader = command.ExecuteReader();
        // EXPLAIN prepares, but never executes, SELECT/DML or trigger bodies.
        // SQLite's b-tree cursor instructions identify the actual table/index
        // root and database, including through views and covering indexes.
        while (reader.Read())
            if (reader.GetString(1) is "OpenRead" or "ReopenIdx" or "OpenWrite" &&
                (Convert.ToInt32(reader.GetValue(6)) & 0x10) == 0 &&
                Convert.ToInt32(reader.GetValue(4)) == database && roots.Contains(Convert.ToInt64(reader.GetValue(3)))) return true;
        return false;
    }
}
