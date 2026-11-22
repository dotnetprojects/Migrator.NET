using System;
using System.Collections.Generic;
using System.Linq;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.DB2;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using DotNetProjects.Migrator.Providers.Impl.Hana;
using DotNetProjects.Migrator.Providers.Impl.Sybase;
using DotNetProjects.Migrator.Providers.Impl.Informix;

namespace DotNetProjects.Migrator.Framework;

public enum TriggerEvent { Insert, Update, Delete }

/// <summary>Rejects a whole statement when any affected row matches Condition. One definition per event.</summary>
public sealed record GuardTrigger(string Name, string Table, TriggerEvent Event, string Message,
    SqlExpression Condition = null, string[] UpdateColumns = null, string[] KeyColumns = null);

public static class GuardTriggers
{
    public static void CreateGuardTrigger(this ITransformationProvider provider, GuardTrigger definition)
    {
        if (provider is NoOpTransformationProvider) return;
        var statements = CreateSql(provider.Dialect, definition, provider.QuoteTableNameIfRequired,
            provider is HanaTransformationProvider hana ? column => hana.GetTriggerColumnType(definition.Table, column) : null);
        ValidateUpdateColumns(provider, definition.Table, definition.UpdateColumns);
        ExecuteDdl(provider, statements);
    }
    public static void DropGuardTrigger(this ITransformationProvider provider, string name, string table)
    {
        if (provider is NoOpTransformationProvider) return;
        ExecuteDdl(provider, DropSql(provider.Dialect, name, provider.QuoteTableNameIfRequired(table)));
    }
    private static void ExecuteDdl(ITransformationProvider p, string[] statements)
    {
        bool own = statements.Length > 1 && p.Dialect is FirebirdDialect && p is TransformationProvider { HasActiveTransaction: false };
        if (own) p.BeginTransaction();
        try { foreach (var sql in statements) p.ExecuteNonQuery(sql); if (own) p.Commit(); }
        catch { if (own) p.Rollback(); throw; }
    }
    internal static string[] DropSql(IDialect dialect, string name, string table)
    {
        RequireSupport(dialect);
        table = ResolveName(dialect, table);
        var trigger = TriggerName(dialect, name, table);
        var t = dialect.QuoteTableNameIfRequired(table);
        if (dialect is FirebirdDialect) return [$"DROP TRIGGER {trigger}", $"DROP EXCEPTION {dialect.QuoteIdentifier(name + "_error")}"];
        if (dialect is InformixDialect) return [$"DROP TRIGGER {trigger}", $"DROP PROCEDURE {FunctionName(dialect, name, table)}()"];
        return dialect is PostgreSQLDialect
            ? [$"DROP TRIGGER {dialect.QuoteIdentifier(name)} ON {t}; DROP FUNCTION {FunctionName(dialect, name, table)}()"]
            : [$"DROP TRIGGER {trigger}"];
    }
    internal static string TriggerName(IDialect d, string name, string table)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A trigger name is required.");
        var parts = SqlIdentifier.Parse(table);
        return (parts.Length > 1 ? string.Join(".", parts[..^1].Select(p => p.Quoted || d.TableNameNeedsQuote || !SqlIdentifier.IsSimple(p.Value) || d.IsReservedWord(p.Value)
            ? d.QuoteIdentifier(p.Value) : p.Value)) + "." : "") + d.QuoteIdentifier(name);
    }
    internal static string FunctionName(IDialect d, string name, string table) => TriggerName(d, name + "_fn", table);
    internal static string ResolveName(IDialect d, string name)
    {
        var qualified = d.QuoteTableNameIfRequired(name);
        return d is SQLiteDialect && SqlIdentifier.Parse(qualified).Length == 1 ? "main." + qualified : qualified;
    }
    internal static Func<string, string> RelatedTableResolver(IDialect d, string subject, Func<string, string> resolver)
        => name =>
        {
            if (d is SQLiteDialect && SqlIdentifier.Parse(name).Length == 1)
            {
                var schema = SqlIdentifier.Parse(subject)[^2].Value;
                // TEMP triggers use SQLite's ordinary temp/main/attached lookup.
                var prefix = schema.Equals("temp", StringComparison.OrdinalIgnoreCase) ? "" : d.QuoteIdentifier(schema) + ".";
                return prefix + d.QuoteIdentifier(SqlIdentifier.Parse(name)[0].Value);
            }
            return resolver?.Invoke(name) ?? d.QuoteTableNameIfRequired(name);
        };
    internal static void ValidateUpdateColumns(ITransformationProvider p, string table, string[] columns)
    {
        if (p.Dialect is not SQLiteDialect || columns == null || columns.Length == 0) return;
        // SQLite folds ASCII identifiers, not arbitrary Unicode case pairs.
        static string Key(string name) => string.Concat(name.Select(c => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c));
        var subject = ResolveName(p.Dialect, p.QuoteTableNameIfRequired(table));
        var actual = new HashSet<string>(p.GetColumns(null, subject).Select(Key), StringComparer.Ordinal);
        foreach (var alias in columns.Select(Key).Distinct().Where(name => name is "rowid" or "oid" or "_rowid_"))
        {
            if (actual.Contains(alias)) continue;
            try
            {
                // Implicit rowid aliases are absent from table_info. Prepare an
                // unquoted, whitelisted alias so SQLite distinguishes rowid tables
                // from WITHOUT ROWID tables (without double-quoted string fallback).
                using var command = p.CreateCommand();
                command.CommandText = "SELECT " + alias + " FROM " + subject + " WHERE 0=1";
                using var reader = command.ExecuteReader();
                actual.Add(alias);
            }
            catch (Exception ex) when (ex is System.Data.Common.DbException or MigrationException) { }
        }
        var missing = columns.Where(column => !actual.Contains(Key(column))).ToArray();
        if (missing.Length > 0) throw new ArgumentException("SQLite UPDATE OF references missing columns: " + string.Join(", ", missing), nameof(columns));
    }
    internal static void RequireSupport(IDialect d)
    {
        if (d is not (SQLiteDialect or PostgreSQLDialect or SqlServerDialect or MysqlDialect or OracleDialect or DB2Dialect or FirebirdDialect or HanaDialect or SybaseDialect or InformixDialect))
            throw new NotSupportedException($"Portable triggers have no implementation for {d.GetType().Name}.");
    }
    internal static string[] CreateSql(IDialect d, GuardTrigger g, Func<string, string> tableResolver = null, Func<string, string> rowTypeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(g);
        RequireSupport(d);
        if (!Enum.IsDefined(g.Event)) throw new ArgumentOutOfRangeException(nameof(g.Event));
        if (string.IsNullOrEmpty(g.Message)) throw new ArgumentException("A domain message is required.");
        if (g.UpdateColumns?.Length > 0 && g.Event != TriggerEvent.Update) throw new ArgumentException("UpdateColumns requires an UPDATE trigger.");
        var table = ResolveName(d, tableResolver?.Invoke(g.Table) ?? g.Table);
        var c = new SqlExpressionContext(d, g.Event) { TableResolver = RelatedTableResolver(d, table, tableResolver), RowTypeResolver = rowTypeResolver };
        var condition = g.Condition?.ToSql(c) ?? "1=1";
        var message = c.Literal(g.Message);
        var trigger = TriggerName(d, g.Name, table);
        var operation = g.Event.ToString().ToUpperInvariant();
        var updateOf = g.UpdateColumns?.Length > 0 ? " OF " + string.Join(", ", g.UpdateColumns.Select(d.QuoteColumnNameIfRequired)) : "";
        if (d is OracleDialect)
            return [$"CREATE TRIGGER {trigger} BEFORE {operation}{updateOf} ON {table} FOR EACH ROW DECLARE guard_block INTEGER; BEGIN SELECT CASE WHEN {condition} THEN 1 ELSE 0 END INTO guard_block FROM DUAL; IF guard_block=1 THEN raise_application_error(-20000, {message}); END IF; END;"];
        string references = (g.Event == TriggerEvent.Insert ? "" : "OLD AS oldrow ") + (g.Event == TriggerEvent.Delete ? "" : "NEW AS newrow ");
        if (d is DB2Dialect)
            return [$"CREATE TRIGGER {trigger} NO CASCADE BEFORE {operation}{updateOf} ON {table} REFERENCING {references}FOR EACH ROW MODE DB2SQL WHEN ({condition}) SIGNAL SQLSTATE '75000' SET MESSAGE_TEXT = {message}"];
        if (d is InformixDialect)
        {
            var function = FunctionName(d, g.Name, table);
            return [$"CREATE PROCEDURE {function}() RAISE EXCEPTION -746, 0, {message}; END PROCEDURE", $"CREATE TRIGGER {trigger} {operation}{updateOf} ON {table} REFERENCING {references}FOR EACH ROW WHEN ({condition}) (EXECUTE PROCEDURE {function}())"];
        }
        if (d is FirebirdDialect)
        {
            if (updateOf.Length > 0) throw new NotSupportedException("Firebird has no assignment-based UPDATE OF trigger; use an OLD/NEW condition.");
            var exception = d.QuoteIdentifier(g.Name + "_error");
            return [$"CREATE EXCEPTION {exception} {message}", $"CREATE TRIGGER {trigger} FOR {table} ACTIVE BEFORE {operation} POSITION 0 AS BEGIN IF ({condition}) THEN EXCEPTION {exception}; END"];
        }
        if (d is HanaDialect)
        {
            if (updateOf.Length > 0) throw new NotSupportedException("HANA has no assignment-based UPDATE OF trigger; use an OLD/NEW condition.");
            var hanaReferences = (g.Event == TriggerEvent.Insert ? "" : "OLD ROW oldrow") + (g.Event == TriggerEvent.Update ? ", " : "") + (g.Event == TriggerEvent.Delete ? "" : "NEW ROW newrow");
            return [$"CREATE TRIGGER {trigger} BEFORE {operation} ON {table} REFERENCING {hanaReferences} FOR EACH ROW BEGIN {string.Join(" ", c.HanaDeclarations)} IF {condition} THEN SIGNAL SQL_ERROR_CODE 10000 SET MESSAGE_TEXT = {message}; END IF; END"];
        }
        if (d is SQLiteDialect)
            return [$"CREATE TRIGGER {trigger} BEFORE {operation}{updateOf} ON {table} FOR EACH ROW WHEN {condition} BEGIN SELECT RAISE(ABORT, {message}); END"];
        if (d is PostgreSQLDialect)
        {
            var body = $"BEGIN IF {condition} THEN RAISE EXCEPTION USING MESSAGE = {message}; END IF; RETURN {(g.Event == TriggerEvent.Delete ? "OLD" : "NEW")}; END";
            var function = FunctionName(d, g.Name, table);
            return [$"CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS {c.Literal(body)}; CREATE TRIGGER {d.QuoteIdentifier(g.Name)} BEFORE {operation}{updateOf} ON {table} FOR EACH ROW EXECUTE PROCEDURE {function}()"];
        }
        if (d is MysqlDialect)
        {
            if (updateOf.Length > 0) throw new NotSupportedException("MySQL cannot detect assignment to an unchanged column; use an OLD/NEW condition instead of UpdateColumns.");
            return [$"CREATE TRIGGER {trigger} BEFORE {operation} ON {table} FOR EACH ROW BEGIN DECLARE guard_message TEXT; SET guard_message = {message}; IF {condition} THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = guard_message; END IF; END"];
        }
        var source = g.Event switch { TriggerEvent.Insert => "inserted i", TriggerEvent.Delete => "deleted d", _ => SqlServerRows(d, g.KeyColumns) };
        var selected = updateOf.Length == 0 ? "" : "IF " + string.Join(" OR ", g.UpdateColumns.Select(x => $"UPDATE({d.QuoteColumnNameIfRequired(x)})")) + " ";
        // UPDATE pairing needs stable keys. Reject assignments to them rather than silently missing rows.
        var failure = d is SybaseDialect ? $"ROLLBACK TRIGGER WITH RAISERROR 20000 {message}; RETURN;" : $"; THROW 50000, {message}, 1;";
        var keyFailure = d is SybaseDialect ? "ROLLBACK TRIGGER WITH RAISERROR 20000 'Trigger pairing keys cannot be assigned'; RETURN;" : "; THROW 50000, 'Trigger pairing keys cannot be assigned', 1;";
        var stable = g.Event == TriggerEvent.Update ? "IF " + string.Join(" OR ", g.KeyColumns.Select(x => $"UPDATE({d.QuoteColumnNameIfRequired(x)})")) + $" BEGIN {keyFailure} END; " : "";
        if (d is SybaseDialect)
        {
            var sybaseStable = g.Event == TriggerEvent.Update ? "IF " + string.Join(" OR ", g.KeyColumns.Select(x => $"UPDATE({d.QuoteColumnNameIfRequired(x)})")) + "\nBEGIN\nROLLBACK TRIGGER WITH RAISERROR 20000 'Trigger pairing keys cannot be assigned'\nRETURN\nEND\n" : "";
            return [$"CREATE TRIGGER {trigger} ON {table} FOR {operation} AS\nBEGIN\nSET NOCOUNT ON\n{sybaseStable}{selected}IF EXISTS (SELECT 1 FROM {source} WHERE {condition})\nBEGIN\nROLLBACK TRIGGER WITH RAISERROR 20000 {message}\nRETURN\nEND\nEND"];
        }
        return [$"CREATE TRIGGER {trigger} ON {table} AFTER {operation} AS BEGIN SET NOCOUNT ON; {stable}{selected}IF EXISTS (SELECT 1 FROM {source} WHERE {condition}) BEGIN {failure} END; END"];
    }
    internal static string SqlServerRows(IDialect d, string[] keys)
    {
        if (keys == null || keys.Length == 0) throw new ArgumentException("SQL Server UPDATE triggers require immutable, non-null unique KeyColumns.");
        return "deleted d INNER JOIN inserted i ON " + string.Join(" AND ", keys.Select(k => "d." + d.QuoteColumnNameIfRequired(k) + " = i." + d.QuoteColumnNameIfRequired(k)));
    }
}
