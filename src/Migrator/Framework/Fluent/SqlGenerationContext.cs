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
    private readonly Func<string, Column[]> existingTable;
    public ProviderTypes Provider { get; }
    public Dialect Dialect { get; }
    public SqlGenerationContext(ProviderTypes provider, Func<string, Column[]> existingTable = null)
    { Provider = provider; Dialect = ProviderFactory.DialectForProvider(provider) ?? throw new ArgumentException("Unknown provider."); this.existingTable = existingTable; }
    public string Quote(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Identifier cannot be empty.");
        var template = Dialect.QuoteTemplate;
        var closing = template[^1].ToString();
        return string.Format(CultureInfo.InvariantCulture, template, name.Replace(closing, closing + closing));
    }
    public string Table(string name) => string.Join(".", name.Split('.').Select(Quote));
    public string Column(Column column) => Dialect.GetAndMapColumnProperties(Definitions.CopyColumn(column)).ColumnSql;
    public string Literal(object value) => value switch
    {
        null or DBNull => "NULL",
        string s => "'" + s.Replace("'", "''") + "'",
        bool b => Provider == ProviderTypes.PostgreSQL ? (b ? "TRUE" : "FALSE") : (b ? "1" : "0"),
        DateTime d => "'" + d.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "'",
        Guid g => "'" + g + "'",
        byte or sbyte or short or ushort or int or uint or long or ulong or decimal or float or double => Convert.ToString(value, CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException("No portable SQL literal for " + value.GetType().Name)
    };
    public void RequireTable(string table)
    {
        if (tables.ContainsKey(table)) return;
        if (removed.Contains(table)) throw new MigrationException("Table was removed earlier in the plan: " + table);
        var columns = existingTable?.Invoke(table) ?? throw new MigrationException("Offline preview needs a definition for existing table: " + table);
        tables.Add(table, columns.ToDictionary(c => c.Name, Definitions.CopyColumn));
    }
    public void AddTable(string table, IEnumerable<Column> columns) { if (tables.ContainsKey(table)) throw new MigrationException("Duplicate table: " + table); removed.Remove(table); tables.Add(table, columns.ToDictionary(c => c.Name, Definitions.CopyColumn)); }
    public void RemoveTable(string table) { tables.Remove(table); removed.Add(table); }
    public void RenameTable(string table, string name) { var columns = tables[table].Values.ToArray(); RemoveTable(table); AddTable(name, columns); }
    public void AddColumn(string table, Column column) => tables[table].Add(column.Name, Definitions.CopyColumn(column));
    public void RenameColumn(string table, string oldName, string newName) { var column = tables[table][oldName]; tables[table].Remove(oldName); column.Name = newName; tables[table].Add(newName, column); }
}
