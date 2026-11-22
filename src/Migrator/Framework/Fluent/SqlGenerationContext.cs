using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DotNetProjects.Migrator.Providers;
namespace DotNetProjects.Migrator.Framework.Fluent;

/// <summary>Tracks schema produced earlier in a preview without modifying a database.</summary>
public sealed class SqlGenerationContext
{
    private readonly Dictionary<string, Dictionary<string, Column>> tables = new(StringComparer.Ordinal);
    private readonly HashSet<string> removed = new(StringComparer.Ordinal);
    private readonly HashSet<string> rowidTables = new(StringComparer.Ordinal);
    private readonly Func<string, Column[]> existingTable;
    private bool schemaUnknown;
    public void InvalidateSchema() => schemaUnknown = true;
    private void RequireKnownSchema()
    { if (schemaUnknown) throw new NotSupportedException("Raw SQL may change the schema; dependent structured preview cannot be verified."); }
    public ProviderTypes Provider { get; }
    public Dialect Dialect { get; }
    public SqlGenerationContext(ProviderTypes provider, Func<string, Column[]> existingTable = null)
    { Provider = provider; Dialect = ProviderFactory.DialectForProvider(provider) ?? throw new ArgumentException("Unknown provider."); this.existingTable = existingTable; }
    public string Quote(string name) => Dialect.QuoteColumnNameIfRequired(name);
    public string Table(string name) => Dialect.QuoteTableNameIfRequired(name);
    private static readonly HashSet<Type> NumericTypes = new()
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(decimal), typeof(float), typeof(double)
    };
    public string Column(Column column) => Dialect.GetAndMapColumnProperties(Definitions.CopyColumn(column)).ColumnSql;
    public string Literal(object value) => value switch
    {
        null or DBNull => "NULL",
        string s => "'" + s.Replace("'", "''") + "'",
        bool b => Provider is ProviderTypes.PostgreSQL or ProviderTypes.PostgreSQL82 ? (b ? "TRUE" : "FALSE") : (b ? "1" : "0"),
        DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "'",
        Guid g when Dialect is DotNetProjects.Migrator.Providers.Impl.Oracle.OracleDialect => Dialect.Default(g)[8..],
        Guid g => "'" + g + "'",
        object number when NumericTypes.Contains(number.GetType()) => Convert.ToString(value, CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException("No portable SQL literal for " + value.GetType().Name)
    };
    public void RequireTable(string table)
    {
        RequireKnownSchema();
        if (tables.ContainsKey(table)) return;
        if (removed.Contains(table)) throw new MigrationException("Table was removed earlier in the plan: " + table);
        var columns = existingTable?.Invoke(table) ?? throw new MigrationException("Offline preview needs a definition for existing table: " + table);
        tables.Add(table, columns.ToDictionary(c => c.Name, Definitions.CopyColumn));
    }
    public void AddTable(string table, IEnumerable<Column> columns) { RequireKnownSchema(); if (tables.ContainsKey(table)) throw new MigrationException("Duplicate table: " + table); removed.Remove(table); tables.Add(table, columns.ToDictionary(c => c.Name, Definitions.CopyColumn)); rowidTables.Add(table); }
    public void RemoveTable(string table) { tables.Remove(table); removed.Add(table); rowidTables.Remove(table); }
    public void RenameTable(string table, string name) { var columns = tables[table].Values.ToArray(); var rowid = rowidTables.Contains(table); RemoveTable(table); AddTable(name, columns); if (!rowid) rowidTables.Remove(name); }
    public void AddColumn(string table, Column column) => tables[table].Add(column.Name, Definitions.CopyColumn(column));
    public void RenameColumn(string table, string oldName, string newName) { var column = tables[table][oldName]; tables[table].Remove(oldName); column.Name = newName; tables[table].Add(newName, column); }

    internal void ValidateTriggerUpdateColumns(string table, string[] columns)
    {
        if (Dialect is not Providers.Impl.SQLite.SQLiteDialect || columns == null || columns.Length == 0) return;
        RequireKnownSchema();
        static string Fold(string name) => string.Concat(name.Select(c => c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c));
        static string TableKey(string name)
        {
            var parts = SqlIdentifier.Parse(name);
            return string.Join("\0", (parts.Length == 1 ? new[] { "main", parts[0].Value } : parts.Select(p => p.Value)).Select(Fold));
        }
        var key = tables.Keys.FirstOrDefault(name => TableKey(name) == TableKey(table));
        if (key == null)
        {
            if (removed.Any(name => TableKey(name) == TableKey(table))) throw new MigrationException("Table was removed earlier in the plan: " + table);
            RequireTable(table); key = table;
        }
        var actual = new HashSet<string>(tables[key].Keys.Select(Fold), StringComparer.Ordinal);
        foreach (var column in columns)
        {
            var name = Fold(column);
            if (actual.Contains(name)) continue;
            if (name is "rowid" or "oid" or "_rowid_")
            {
                if (rowidTables.Contains(key)) continue;
                throw new NotSupportedException("SQLite UPDATE OF preview cannot verify implicit rowid aliases on an existing table from column metadata alone: " + table);
            }
            throw new ArgumentException("SQLite UPDATE OF references a missing column: " + column, nameof(columns));
        }
    }
}
