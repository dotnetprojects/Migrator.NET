using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
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

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        base.ConfigureParameterWithValue(parameter, index, value);
        if (value is string text && text.Length > 32739)
        {
            // The dialect uses native TEXT beyond LVARCHAR capacity. Bind its
            // LONGVARCHAR representation instead of the driver's NText path.
            parameter.DbType = DbType.AnsiString;
        }
    }

    private static string Name(string name) => (name.StartsWith('"') ? name[1..^1].Replace("\"\"", "\"") : name.ToLowerInvariant()).Replace("'", "''");
    public override string GenerateParameterName(int index) => "?";
    public override void AddColumn(string table, Column column) =>
        AddColumn(table, _dialect.GetAndMapColumnProperties(column).ColumnSql);

    public override void AddTable(string name, string engine, params IDbField[] fields)
    {
        base.AddTable(name, engine, fields);
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
    public override bool ConstraintExists(string table, string name) => GetConstraints(table).Any(n => n == name || n == Name(name).Replace("''", "'"));
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
                7 => DbType.Date, 10 => ((Convert.ToInt32(reader.GetValue(2)) >> 4) & 15) == 6 ? DbType.Time : DbType.DateTime, 11 => DbType.Binary, 14 => (DbType)MigratorDbType.Interval,
                0 or 15 => DbType.StringFixedLength,
                45 => DbType.Boolean, _ => DbType.String
            };
            if (extendedType == "blob") type = DbType.Binary;
            if (extendedType == "boolean") type = DbType.Boolean;
            var column = new Column(reader.GetString(0).Trim(), type)
            {
                IsNullable = !((code & 256) != 0)
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
            if ((code & 255) is 6 or 18 or 53) column.IsIdentity = true;
            if (!reader.IsDBNull(5)) column.DefaultValue = ReadDefault(reader.IsDBNull(3) ? "" : reader.GetString(3), reader.GetString(5).Trim(), type);
            columns.Add(column);
        }
        return columns.ToArray();
    }

    public override ForeignKeyConstraint[] GetForeignKeyConstraints(string table)
    {
        var rows = new List<(string Name, string Parent, string ChildIndex, string ParentIndex, string Delete)>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"SELECT c.constrname,t2.tabname,c.idxname,p.idxname,r.delrule FROM sysconstraints c JOIN systables t ON t.tabid=c.tabid JOIN sysreferences r ON r.constrid=c.constrid JOIN sysconstraints p ON p.constrid=r.primary JOIN systables t2 ON t2.tabid=r.ptabid WHERE t.owner=USER AND t.tabname='{Name(table)}' AND t2.owner=USER ORDER BY c.constrname"))
            while (reader.Read())
                rows.Add((reader.GetString(0).Trim(), reader.GetString(1).Trim(), reader.GetString(2).Trim(), reader.GetString(3).Trim(), reader.GetString(4).Trim()));
        var childIndexes = GetIndexes(table).ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        return rows.Select(row => new ForeignKeyConstraint(row.Name, row.Parent,
            GetIndexes(row.Parent).Single(i => i.Name.Equals(row.ParentIndex, StringComparison.OrdinalIgnoreCase)).KeyColumns,
            table, childIndexes[row.ChildIndex].KeyColumns)
            { OnDelete = row.Delete == "C" ? "CASCADE" : "RESTRICT", OnUpdate = "RESTRICT" }).ToArray();
    }

    public override TableConstraint[] GetTableConstraints(string table)
    {
        var indexes = GetIndexes(table).ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var constraints = new List<TableConstraint>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"SELECT c.constrname,c.constrtype,c.idxname FROM sysconstraints c JOIN systables t ON t.tabid=c.tabid WHERE t.owner=USER AND t.tabname='{Name(table)}' AND c.constrtype IN ('P','U') ORDER BY c.constrname"))
        {
            while (reader.Read())
            {
                var name = reader.GetString(0).Trim();
                var index = indexes[reader.GetString(2).Trim()];
                constraints.Add(reader.GetString(1).Trim() == "P"
                    ? new PrimaryKeyConstraint(name, index.KeyColumns)
                    : new DotNetProjects.Migrator.Framework.UniqueConstraint(name, index.KeyColumns));
            }
        }
        var checks = new Dictionary<string, System.Text.StringBuilder>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"SELECT c.constrname,ch.checktext FROM sysconstraints c JOIN systables t ON t.tabid=c.tabid JOIN syschecks ch ON ch.constrid=c.constrid WHERE t.owner=USER AND t.tabname='{Name(table)}' AND c.constrtype='C' AND ch.type='T' ORDER BY c.constrname,ch.seqno"))
            while (reader.Read())
            {
                var name = reader.GetString(0).Trim();
                if (!checks.TryGetValue(name, out var text)) checks[name] = text = new System.Text.StringBuilder();
                text.Append(reader.GetString(1));
            }
        constraints.AddRange(checks.Select(c => new CheckConstraint(c.Key, ConstraintMetadataReader.CheckExpression(c.Value.ToString()))));
        constraints.AddRange(GetForeignKeyConstraints(table));
        return constraints.ToArray();
    }

    private static object ReadDefault(string catalogValue, string kind, DbType type)
    {
        // SYSDEFAULTS stores literal text without SQL quotes, and prefixes non-character
        // literals with a six-bit encoding separated from the readable value by a space.
        if (kind != "L")
            return CatalogDefaultValue.Parse(kind switch
            {
                // Match the DATETIME precision emitted by InformixDialect.
                "N" => "NULL", "C" => "CURRENT YEAR TO FRACTION(5)", "T" => "TODAY",
                "U" => "USER", "S" => "DBSERVERNAME",
                _ => throw new NotSupportedException($"Unsupported Informix default kind: {kind}")
            }, type);
        // Character literals are null-terminated before the CHAR(256) padding.
        // Stop at the terminator so meaningful trailing spaces remain part of the literal.
        var terminator = catalogValue.IndexOf('\0');
        var value = terminator >= 0 ? catalogValue[..terminator] : catalogValue.TrimEnd();
        if (type is DbType.String or DbType.AnsiString or DbType.StringFixedLength or DbType.AnsiStringFixedLength)
            return value;
        if (type == DbType.Boolean)
        {
            var literal = value.Trim();
            var suffix = literal.LastIndexOf(' ');
            if (suffix >= 0) literal = literal[(suffix + 1)..];
            literal = literal.Trim('\'');
            return CatalogDefaultValue.Parse(literal.Equals("t", StringComparison.OrdinalIgnoreCase) ? "true" :
                literal.Equals("f", StringComparison.OrdinalIgnoreCase) ? "false" : literal, type);
        }
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
        ExecuteNonQuery($"ALTER TABLE {table} ADD CONSTRAINT PRIMARY KEY ({string.Join(", ", QuoteColumnNamesIfRequired(columns))}) CONSTRAINT {QuoteConstraintNameIfRequired(name)}");
    public override void AddUniqueConstraint(string name, string table, params string[] columns) =>
        ExecuteNonQuery($"ALTER TABLE {table} ADD CONSTRAINT UNIQUE ({string.Join(", ", QuoteColumnNamesIfRequired(columns))}) CONSTRAINT {QuoteConstraintNameIfRequired(name)}");
    public override void AddCheckConstraint(string name, string table, string checkSql) =>
        ExecuteNonQuery($"ALTER TABLE {table} ADD CONSTRAINT CHECK ({checkSql}) CONSTRAINT {QuoteConstraintNameIfRequired(name)}");

    public override void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns,
        ForeignKeyConstraintType onDelete, ForeignKeyConstraintType onUpdate)
    {
        if (onUpdate is not (ForeignKeyConstraintType.NoAction or ForeignKeyConstraintType.Restrict))
            throw new NotSupportedException("Informix does not support the requested ON UPDATE action.");
        AddForeignKey(name, childTable, childColumns, parentTable, parentColumns, onDelete);
    }

    public override void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns, ForeignKeyConstraintType constraint)
    {
        var action = constraint switch
        {
            ForeignKeyConstraintType.Cascade => " ON DELETE CASCADE",
            ForeignKeyConstraintType.NoAction or ForeignKeyConstraintType.Restrict => "",
            _ => throw new NotSupportedException("Informix supports cascading deletes or its default restrictive referential action.")
        };
        ExecuteNonQuery($"ALTER TABLE {childTable} ADD CONSTRAINT FOREIGN KEY ({string.Join(", ", QuoteColumnNamesIfRequired(childColumns))}) REFERENCES {parentTable} ({string.Join(", ", QuoteColumnNamesIfRequired(parentColumns))}){action} CONSTRAINT {QuoteConstraintNameIfRequired(name)}");
    }
}
