using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Informix;

public class InformixTransformationProvider : TransformationProvider
{
    public InformixTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        var factory = DbProviderFactoriesHelper.GetFactory(string.IsNullOrEmpty(providerName) ? "IBM.Data.Informix.Client" : providerName, null, null);
        _connection = factory.CreateConnection();
        _connection.ConnectionString = connectionString;
        _connection.Open();
    }

    public InformixTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
        : base(dialect, connection, null, scope) { }

    private static string Name(string name) => (name.StartsWith('"') ? name[1..^1].Replace("\"\"", "\"") : name.ToLowerInvariant()).Replace("'", "''");
    public override string GenerateParameterName(int index) => "?";
    public override void AddColumn(string table, Column column) =>
        AddColumn(table, _dialect.GetAndMapColumnProperties(column).ColumnSql);

    public override void AddTable(string name, string engine, params IDbField[] fields)
    {
        base.AddTable(name, engine, fields);
        foreach (var column in fields.OfType<Column>().Where(c => c.ColumnProperty.HasFlag(ColumnProperty.Indexed)))
            AddIndex(name, new Index { KeyColumns = [column.Name] });
    }

    public override bool TableExists(string table) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM systables WHERE tabname='{Name(table)}' AND owner=USER AND tabtype='T'")) > 0;
    public override bool ViewExists(string view) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM systables WHERE tabname='{Name(view)}' AND owner=USER AND tabtype='V'")) > 0;
    public override string[] GetTables() => ExecuteStringQuery(
        "SELECT tabname FROM systables WHERE owner=USER AND tabid>=100 AND tabtype='T'").Select(n => n.Trim()).ToArray();
    public override List<string> GetDatabases() => ExecuteStringQuery("SELECT name FROM sysmaster:sysdatabases");
    public override string[] GetConstraints(string table) => ExecuteStringQuery(
        $"SELECT c.constrname FROM sysconstraints c JOIN systables t ON c.tabid=t.tabid WHERE t.owner=USER AND t.tabname='{Name(table)}'").Select(n => n.Trim()).ToArray();
    public override bool ConstraintExists(string table, string name) => GetConstraints(table).Contains(Name(name));
    protected override string GetPrimaryKeyConstraintName(string table) => ExecuteStringQuery(
        $"SELECT c.constrname FROM sysconstraints c JOIN systables t ON c.tabid=t.tabid WHERE t.owner=USER AND t.tabname='{Name(table)}' AND c.constrtype='P'").FirstOrDefault()?.Trim();

    public override Column[] GetColumns(string table)
    {
        var primaryColumns = GetIndexes(table).Where(i => i.PrimaryKey).SelectMany(i => i.KeyColumns).ToHashSet(StringComparer.Ordinal);
        var columns = new List<Column>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT c.colname, c.coltype, c.collength, d.default, x.name, d.type
            FROM syscolumns c JOIN systables t ON c.tabid=t.tabid
            LEFT JOIN sysdefaults d ON d.tabid=c.tabid AND d.colno=c.colno AND d.class='T'
            LEFT JOIN sysxtdtypes x ON x.extended_id=c.extended_id
            WHERE t.owner=USER AND t.tabname='{Name(table)}' ORDER BY c.colno
            """);
        while (reader.Read())
        {
            var code = Convert.ToInt32(reader.GetValue(1));
            var extendedType = reader.IsDBNull(4) ? "" : reader.GetString(4).Trim().ToLowerInvariant();
            var type = (code & 255) switch
            {
                1 => DbType.Int16, 2 or 6 => DbType.Int32, 17 or 18 or 52 or 53 => DbType.Int64,
                3 => DbType.Double, 4 => DbType.Single, 5 or 8 => DbType.Decimal,
                7 => DbType.Date, 10 => DbType.DateTime, 11 => DbType.Binary, 14 => DbType.Time,
                0 or 15 => DbType.StringFixedLength,
                45 => DbType.Boolean, _ => DbType.String
            };
            if (extendedType == "blob") type = DbType.Binary;
            if (extendedType == "boolean") type = DbType.Boolean;
            var column = new Column(reader.GetString(0).Trim(), type)
            {
                ColumnProperty = (code & 256) != 0 ? ColumnProperty.NotNull : ColumnProperty.Null
            };
            if (type is DbType.String or DbType.StringFixedLength)
            {
                var length = Convert.ToInt32(reader.GetValue(2));
                // VARCHAR/NVARCHAR pack reserved space into the high byte; CHAR/LVARCHAR store the full length.
                column.Size = (code & 255) == 12 || extendedType == "clob"
                    ? int.MaxValue
                    : (code & 255) is 13 or 16 ? length & 255 : length;
            }
            if (type == DbType.Decimal)
            {
                var length = Convert.ToInt32(reader.GetValue(2));
                column.Precision = length >> 8;
                column.Scale = (length & 255) == 255 ? null : length & 255;
            }
            if ((code & 255) is 6 or 18 or 53) column.ColumnProperty |= ColumnProperty.Identity;
            if (!reader.IsDBNull(5)) column.DefaultValue = ReadDefault(reader.IsDBNull(3) ? "" : reader.GetString(3), reader.GetString(5).Trim(), type);
            if (primaryColumns.Contains(column.Name)) column.ColumnProperty |= ColumnProperty.PrimaryKey;
            columns.Add(column);
        }
        return columns.ToArray();
    }

    private static object ReadDefault(string catalogValue, string kind, DbType type)
    {
        // SYSDEFAULTS stores literal text without SQL quotes, and prefixes non-character
        // literals with a six-bit encoding separated from the readable value by a space.
        if (kind != "L")
            return CatalogDefaultValue.Parse(kind switch
            {
                "N" => "NULL", "C" => "CURRENT", "T" => "TODAY",
                "U" => "USER", "S" => "DBSERVERNAME",
                _ => throw new NotSupportedException($"Unsupported Informix default kind: {kind}")
            }, type);
        var value = catalogValue.TrimEnd();
        if (type is DbType.String or DbType.AnsiString or DbType.StringFixedLength or DbType.AnsiStringFixedLength)
            return value;
        if (type == DbType.Boolean)
            return CatalogDefaultValue.Parse(value.Trim().Equals("t", StringComparison.OrdinalIgnoreCase) ? "true" :
                value.Trim().Equals("f", StringComparison.OrdinalIgnoreCase) ? "false" : value, type);
        var separator = value.IndexOf(' ');
        if (separator >= 0) value = value[(separator + 1)..].Trim();
        if (type is DbType.Date or DbType.DateTime or DbType.Time) value = "'" + value.Replace("'", "''") + "'";
        return CatalogDefaultValue.Parse(value, type);
    }

    public override Index[] GetIndexes(string table)
    {
        var result = new List<Index>();
        var columns = GetColumnsForIndex(table);
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT i.*, c.constrtype FROM sysindexes i JOIN systables t ON t.tabid=i.tabid
            LEFT JOIN sysconstraints c ON c.tabid=i.tabid AND c.idxname=i.idxname AND c.constrtype IN ('P','U')
            WHERE t.owner=USER AND t.tabname='{Name(table)}'
            """);
        while (reader.Read())
        {
            var index = new Index { Name = Convert.ToString(reader["idxname"]).Trim(), Unique = Convert.ToString(reader["idxtype"]).Trim() == "U",
                PrimaryKey = Convert.ToString(reader["constrtype"]).Trim() == "P",
                UniqueConstraint = Convert.ToString(reader["constrtype"]).Trim() == "U" };
            var keys = new List<string>();
            for (var part = 1; part <= 16; part++)
            {
                var number = Math.Abs(Convert.ToInt32(reader["part" + part]));
                if (number == 0) break;
                keys.Add(columns[number]);
            }
            index.KeyColumns = keys.ToArray();
            result.Add(index);
        }
        return result.ToArray();
    }

    private Dictionary<int, string> GetColumnsForIndex(string table)
    {
        var columns = new Dictionary<int, string>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"SELECT c.colno,c.colname FROM syscolumns c JOIN systables t ON t.tabid=c.tabid WHERE t.owner=USER AND t.tabname='{Name(table)}'");
        while (reader.Read()) columns[Convert.ToInt32(reader.GetValue(0))] = reader.GetString(1).Trim();
        return columns;
    }

    public override void RemoveAllIndexes(string table)
    {
        var constraints = ExecuteStringQuery($"SELECT c.constrname FROM sysconstraints c JOIN systables t ON t.tabid=c.tabid WHERE t.owner=USER AND t.tabname='{Name(table)}' AND c.constrtype IN ('P','U')");
        foreach (var name in constraints) RemoveConstraint(table, name.Trim());
        foreach (var index in GetIndexes(table)) RemoveIndex(table, index.Name);
    }

    public override bool IndexExists(string table, string name) => GetIndexes(table).Any(i => i.Name == Name(name));
    public override string AddIndex(string table, Index index)
    {
        if (index.KeyColumns.Length == 0) throw new ArgumentException("An index needs key columns.", nameof(index));
        if (index.IncludeColumns.Length != 0 || index.FilterItems.Count != 0 || index.Clustered)
            throw new NotSupportedException("This Informix provider supports ordinary and unique indexes without INCLUDE, filters or clustering.");
        var name = index.Name ?? $"ix_{table}_{string.Join("_", index.KeyColumns)}";
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {name} ON {table} ({string.Join(", ", index.KeyColumns)})");
        return name;
    }

    public override void AddColumn(string table, string sqlColumn) => ExecuteNonQuery($"ALTER TABLE {table} ADD ({sqlColumn})");
    public override void ChangeColumn(string table, string sqlColumn) => ExecuteNonQuery($"ALTER TABLE {table} MODIFY ({sqlColumn})");
    public override void RemoveColumn(string tableName, string column) => ExecuteNonQuery($"ALTER TABLE {tableName} DROP ({column})");
    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        if (!ColumnExists(tableName, oldColumnName) || ColumnExists(tableName, newColumnName))
            throw new MigrationException("Source column must exist and destination column must not exist.");
        ExecuteNonQuery($"RENAME COLUMN {tableName}.{oldColumnName} TO {newColumnName}");
    }
    public override void RenameTable(string oldName, string newName) => ExecuteNonQuery($"RENAME TABLE {oldName} TO {newName}");
    public override void RemoveColumnDefaultValue(string table, string column)
    {
        var existing = GetColumns(table).Single(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
        existing.DefaultValue = null;
        ChangeColumn(table, existing);
    }

    public override void AddPrimaryKey(string name, string table, params string[] columns) =>
        ExecuteNonQuery($"ALTER TABLE {table} ADD CONSTRAINT PRIMARY KEY ({string.Join(", ", columns)}) CONSTRAINT {name}");
    public override void AddUniqueConstraint(string name, string table, params string[] columns) =>
        ExecuteNonQuery($"ALTER TABLE {table} ADD CONSTRAINT UNIQUE ({string.Join(", ", columns)}) CONSTRAINT {name}");
    public override void AddCheckConstraint(string name, string table, string checkSql) =>
        ExecuteNonQuery($"ALTER TABLE {table} ADD CONSTRAINT CHECK ({checkSql}) CONSTRAINT {name}");

    public override void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns, ForeignKeyConstraintType constraint)
    {
        var action = constraint switch
        {
            ForeignKeyConstraintType.Cascade => " ON DELETE CASCADE",
            ForeignKeyConstraintType.NoAction or ForeignKeyConstraintType.Restrict => "",
            _ => throw new NotSupportedException("Informix supports cascading deletes or its default restrictive referential action.")
        };
        ExecuteNonQuery($"ALTER TABLE {childTable} ADD CONSTRAINT FOREIGN KEY ({string.Join(", ", childColumns)}) REFERENCES {parentTable} ({string.Join(", ", parentColumns)}){action} CONSTRAINT {name}");
    }
}
