using System;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.DB2;

namespace DotNetProjects.Migrator.Framework;

public sealed record JsonField(string Key, SqlExpression Value);

/// <summary>Writes one transactional audit event per affected row. INSERT has a SQL NULL previous snapshot.</summary>
public sealed record AuditTrigger(string Name, string Table, TriggerEvent Event, string AuditTable,
    SqlExpression EntityKey, JsonField[] BeforeFields, SqlExpression Condition = null,
    SqlExpression RecordedAt = null, string TimestampColumn = "OccurredAt", string KeyColumn = "EntityId",
    string OperationColumn = "Operation", string SnapshotColumn = "BeforeJson",
    string[] UpdateColumns = null, string[] KeyColumns = null);

public static class AuditTriggers
{
    public static void CreateAuditTrigger(this ITransformationProvider p, AuditTrigger definition)
    {
        if (p is Providers.NoOpTransformationProvider) return;
        var statements = CreateSql(p.Dialect, definition, p.QuoteTableNameIfRequired);
        GuardTriggers.ValidateUpdateColumns(p, definition.Table, definition.UpdateColumns);
        foreach (var sql in statements) p.ExecuteNonQuery(sql);
    }
    public static void DropAuditTrigger(this ITransformationProvider p, string name, string table) => p.DropGuardTrigger(name, table);
    public static SqlExpression UtcNow() => new(c => c.Dialect switch
    {
        SQLiteDialect => "strftime('%Y-%m-%dT%H:%M:%fZ','now')",
        PostgreSQLDialect => "(clock_timestamp() AT TIME ZONE 'UTC')",
        SqlServerDialect => "SYSUTCDATETIME()",
        MysqlDialect => "UTC_TIMESTAMP(6)",
        OracleDialect => "SYS_EXTRACT_UTC(SYSTIMESTAMP)",
        DB2Dialect => "(CURRENT TIMESTAMP - CURRENT TIMEZONE)",
        _ => throw new NotSupportedException("No database UTC expression for " + c.Dialect.GetType().Name)
    });
    public static SqlExpression JsonObject(params JsonField[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var copy = fields.ToArray();
        if (copy.Any(f => f == null || f.Key == null || f.Value == null) || copy.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("JSON fields require distinct non-null keys and values.");
        return new(c =>
        {
            if (c.Dialect is SqlServerDialect)
            {
                if (copy.Any(f => f.Key.Contains('.'))) throw new NotSupportedException("SQL Server JSON PATH requires keys without dots.");
                if (copy.Length == 0) return "'{}'";
                return "(SELECT " + string.Join(", ", copy.Select(f => f.Value.ToSql(c) + " AS " + c.Dialect.QuoteIdentifier(f.Key))) + " FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER)";
            }
            if (c.Dialect is OracleDialect or DB2Dialect)
                return "JSON_OBJECT(" + string.Join(", ", copy.Select(f => c.Literal(f.Key) + " VALUE " + f.Value.ToSql(c))) + " NULL ON NULL RETURNING CLOB)";
            var function = c.Dialect switch { SQLiteDialect => "json_object", PostgreSQLDialect => "json_build_object", MysqlDialect => "JSON_OBJECT", _ => throw new NotSupportedException("No JSON object expression for this provider.") };
            return function + "(" + string.Join(", ", copy.SelectMany(f => new[] { c.Literal(f.Key), f.Value.ToSql(c) })) + ")";
        });
    }
    internal static string[] CreateSql(IDialect d, AuditTrigger a, Func<string, string> tableResolver = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (d is not (SQLiteDialect or PostgreSQLDialect or SqlServerDialect or MysqlDialect or OracleDialect or DB2Dialect))
            throw new NotSupportedException("No transactional JSON audit implementation for " + d.GetType().Name);
        if (!Enum.IsDefined(a.Event)) throw new ArgumentOutOfRangeException(nameof(a.Event));
        ArgumentNullException.ThrowIfNull(a.EntityKey);
        if (a.UpdateColumns?.Length > 0 && a.Event != TriggerEvent.Update) throw new ArgumentException("UpdateColumns requires UPDATE.");
        var table = GuardTriggers.ResolveName(d, tableResolver?.Invoke(a.Table) ?? a.Table);
        var c = new SqlExpressionContext(d, a.Event) { TableResolver = GuardTriggers.RelatedTableResolver(d, table, tableResolver) };
        var condition = a.Condition?.ToSql(c) ?? "1=1";
        var values = new[] { (a.RecordedAt ?? UtcNow()).ToSql(c), a.EntityKey.ToSql(c), c.Literal(a.Event.ToString().ToUpperInvariant()),
            a.Event == TriggerEvent.Insert ? "NULL" : JsonObject(a.BeforeFields).ToSql(c) };
        var columns = new[] { a.TimestampColumn, a.KeyColumn, a.OperationColumn, a.SnapshotColumn };
        if (columns.Distinct().Count() != 4) throw new ArgumentException("Audit target columns must be distinct.");
        var insert = "INSERT INTO " + c.Table(a.AuditTable) + " (" + string.Join(", ", columns.Select(d.QuoteColumnNameIfRequired)) + ") ";
        var trigger = GuardTriggers.TriggerName(d, a.Name, table);
        var operation = a.Event.ToString().ToUpperInvariant();
        var updateOf = a.UpdateColumns?.Length > 0 ? " OF " + string.Join(", ", a.UpdateColumns.Select(d.QuoteColumnNameIfRequired)) : "";
        var statement = insert + "VALUES (" + string.Join(", ", values) + ");";
        if (d is SQLiteDialect)
        {
            // Trigger-body INSERT targets must be unqualified and resolve in the
            // trigger's database. Cross-database audit writes are not supported.
            var sourceParts = Providers.SqlIdentifier.Parse(table);
            var targetParts = Providers.SqlIdentifier.Parse(c.Table(a.AuditTable));
            if (targetParts.Length > 1 && !string.Equals(sourceParts.Length > 1 ? sourceParts[^2].Value : "main", targetParts[^2].Value, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("SQLite audit tables must be in the source trigger's database.");
            insert = "INSERT INTO " + d.QuoteIdentifier(targetParts[^1].Value) + " (" + string.Join(", ", columns.Select(d.QuoteColumnNameIfRequired)) + ") ";
            statement = insert + "VALUES (" + string.Join(", ", values) + "); SELECT CASE WHEN changes()<>1 THEN RAISE(ABORT, 'Audit event was suppressed by a conflict policy') END;";
        }
        if (d is OracleDialect)
            return [$"CREATE TRIGGER {trigger} AFTER {operation}{updateOf} ON {table} FOR EACH ROW DECLARE audit_match INTEGER; BEGIN SELECT CASE WHEN {condition} THEN 1 ELSE 0 END INTO audit_match FROM DUAL; IF audit_match=1 THEN {statement} END IF; END;"];
        if (d is DB2Dialect)
        {
            var references = (a.Event == TriggerEvent.Insert ? "" : "OLD AS oldrow ") + (a.Event == TriggerEvent.Delete ? "" : "NEW AS newrow ");
            return [$"CREATE TRIGGER {trigger} AFTER {operation}{updateOf} ON {table} REFERENCING {references}FOR EACH ROW MODE DB2SQL WHEN ({condition}) BEGIN ATOMIC {statement} END"];
        }
        if (d is SQLiteDialect) return [$"CREATE TRIGGER {trigger} AFTER {operation}{updateOf} ON {table} FOR EACH ROW WHEN {condition} BEGIN {statement} END"];
        if (d is PostgreSQLDialect)
        {
            var function = GuardTriggers.FunctionName(d, a.Name, table);
            var body = $"BEGIN IF {condition} THEN {statement} END IF; RETURN NULL; END";
            return [$"CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS {c.Literal(body)}; CREATE TRIGGER {d.QuoteIdentifier(a.Name)} AFTER {operation}{updateOf} ON {table} FOR EACH ROW EXECUTE PROCEDURE {function}()"];
        }
        if (d is MysqlDialect)
        {
            if (updateOf.Length > 0) throw new NotSupportedException("MySQL has no assignment-based UPDATE OF trigger.");
            return [$"CREATE TRIGGER {trigger} AFTER {operation} ON {table} FOR EACH ROW BEGIN IF {condition} THEN {statement} END IF; END"];
        }
        var source = a.Event switch { TriggerEvent.Insert => "inserted i", TriggerEvent.Delete => "deleted d", _ => GuardTriggers.SqlServerRows(d, a.KeyColumns) };
        var stable = a.Event == TriggerEvent.Update ? "IF " + string.Join(" OR ", a.KeyColumns.Select(k => "UPDATE(" + d.QuoteColumnNameIfRequired(k) + ")")) + " BEGIN; THROW 50000, 'Trigger pairing keys cannot be assigned', 1; END; " : "";
        var selected = updateOf.Length == 0 ? "" : "IF " + string.Join(" OR ", a.UpdateColumns.Select(k => "UPDATE(" + d.QuoteColumnNameIfRequired(k) + ")")) + " ";
        return [$"CREATE TRIGGER {trigger} ON {table} AFTER {operation} AS BEGIN SET NOCOUNT ON; {stable}{selected}{insert}SELECT {string.Join(", ", values)} FROM {source} WHERE {condition}; END"];
    }
}
