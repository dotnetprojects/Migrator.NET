using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Sybase;

public class SybaseTransformationProvider : TransformationProvider
{
    public SybaseTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        var factory = DbProviderFactoriesHelper.GetFactory(string.IsNullOrEmpty(providerName) ? "Sybase.Data.AseClient" : providerName, null, null);
        _connection = factory.CreateConnection();
        _connection.ConnectionString = connectionString;
        _connection.Open();
    }
    public SybaseTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
        : base(dialect, connection, null, scope) { }

    private static string Literal(string name) => name.Replace("'", "''");
    public override void AddColumn(string table, Column column) =>
        AddColumn(table, _dialect.GetAndMapColumnProperties(column).ColumnSql);

    public override void AddTable(string name, string engine, params IDbField[] fields)
    {
        base.AddTable(name, engine, fields);
        foreach (var column in fields.OfType<Column>().Where(c => c.ColumnProperty.HasFlag(ColumnProperty.Indexed)))
            AddIndex(name, new Index { KeyColumns = [column.Name] });
    }

    public override bool TableExists(string table) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM sysobjects WHERE id=object_id('{Literal(table)}') AND type='U'")) > 0;
    public override bool ViewExists(string view) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM sysobjects WHERE id=object_id('{Literal(view)}') AND type='V'")) > 0;
    public override string[] GetTables() => ExecuteStringQuery("SELECT name FROM sysobjects WHERE type='U' AND uid=user_id()").ToArray();
    public override List<string> GetDatabases() => ExecuteStringQuery("SELECT name FROM master..sysdatabases");
    public override string[] GetConstraints(string table) => ExecuteStringQuery(
        $"SELECT o.name FROM sysconstraints c JOIN sysobjects o ON o.id=c.constrid WHERE c.tableid=object_id('{Literal(table)}') UNION SELECT name FROM sysindexes WHERE id=object_id('{Literal(table)}') AND (status2 & 2)=2").ToArray();
    public override bool ConstraintExists(string table, string name) => GetConstraints(table).Contains(name);
    protected override string GetPrimaryKeyConstraintName(string table) => ExecuteStringQuery(
        $"SELECT name FROM sysindexes WHERE id=object_id('{Literal(table)}') AND (status & 2048)=2048 AND (status & 2)=2").FirstOrDefault();

    public override Column[] GetColumns(string table)
    {
        var primaryColumns = GetIndexes(table).Where(i => i.PrimaryKey).SelectMany(i => i.KeyColumns).ToHashSet(StringComparer.Ordinal);
        var defaults = GetColumnDefaults(table);
        var columns = new List<Column>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT c.name,t.name,c.status,c.length,c.prec,c.scale FROM syscolumns c JOIN systypes t ON t.usertype=c.usertype
            WHERE c.id=object_id('{Literal(table)}') ORDER BY c.colid
            """);
        while (reader.Read())
        {
            var type = reader.GetString(1).Trim() switch
            {
                "tinyint" => DbType.Byte, "smallint" => DbType.Int16, "int" => DbType.Int32, "bigint" => DbType.Int64,
                "numeric" or "decimal" or "money" => DbType.Decimal, "float" => DbType.Double,
                "real" => DbType.Single, "date" => DbType.Date, "time" => DbType.Time,
                "datetime" or "bigdatetime" => DbType.DateTime, "bit" => DbType.Boolean,
                "image" or "binary" or "varbinary" => DbType.Binary, _ => DbType.String
            };
            var status = Convert.ToInt32(reader.GetValue(2));
            var column = new Column(reader.GetString(0), type)
            {
                ColumnProperty = (status & 8) != 0 ? ColumnProperty.Null : ColumnProperty.NotNull
            };
            if (type == DbType.Decimal)
            {
                if (!reader.IsDBNull(4)) column.Precision = Convert.ToInt32(reader.GetValue(4));
                if (!reader.IsDBNull(5)) column.Scale = Convert.ToInt32(reader.GetValue(5));
            }
            if (defaults.TryGetValue(column.Name, out var defaultSql)) column.DefaultValue = CatalogDefaultValue.Parse(defaultSql, type);
            if ((status & 128) != 0) column.ColumnProperty |= ColumnProperty.Identity;
            if (type == DbType.String) column.Size = Convert.ToInt32(reader.GetValue(3));
            if (primaryColumns.Contains(column.Name)) column.ColumnProperty |= ColumnProperty.PrimaryKey;
            columns.Add(column);
        }
        return columns.ToArray();
    }

    private Dictionary<string, string> GetColumnDefaults(string table)
    {
        var defaults = new Dictionary<string, string>();
        using var command = CreateCommand();
        using var reader = ExecuteQuery(command, $"SELECT c.name,d.text FROM syscolumns c JOIN syscomments d ON d.id=c.cdefault WHERE c.id=object_id('{Literal(table)}') ORDER BY c.colid,d.colid2,d.colid");
        while (reader.Read())
        {
            var name = reader.GetString(0);
            defaults.TryGetValue(name, out var text);
            defaults[name] = text + reader.GetString(1);
        }
        foreach (var name in defaults.Keys.ToArray())
        {
            var sql = defaults[name].Trim();
            if (sql.StartsWith("CREATE DEFAULT", StringComparison.OrdinalIgnoreCase))
                sql = System.Text.RegularExpressions.Regex.Replace(sql, @"^CREATE\s+DEFAULT\s+.+?\s+AS\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (sql.StartsWith("DEFAULT", StringComparison.OrdinalIgnoreCase)) sql = sql[7..].Trim();
            defaults[name] = sql;
        }
        return defaults;
    }

    public override Index[] GetIndexes(string table)
    {
        var indexes = new List<Index>();
        using var cmd = CreateCommand();
        using (var reader = ExecuteQuery(cmd, $"SELECT name,indid,status,status2 FROM sysindexes WHERE id=object_id('{Literal(table)}') AND indid BETWEEN 1 AND 254"))
        {
            while (reader.Read())
            {
                var status = Convert.ToInt32(reader.GetValue(2));
                indexes.Add(new Index { Name = reader.GetString(0), Unique = (status & 2) != 0,
                    PrimaryKey = (status & 2048) != 0, UniqueConstraint = (status & 2048) == 0 && (Convert.ToInt32(reader.GetValue(3)) & 2) != 0,
                    Clustered = Convert.ToInt32(reader.GetValue(1)) == 1 || (Convert.ToInt32(reader.GetValue(3)) & 512) != 0 });
            }
        }
        foreach (var index in indexes)
        {
            var id = Convert.ToInt32(ExecuteScalar($"SELECT indid FROM sysindexes WHERE id=object_id('{Literal(table)}') AND name='{Literal(index.Name)}'"));
            var keys = new List<string>();
            for (var position = 1; position <= 31; position++)
            {
                var key = Convert.ToString(ExecuteScalar($"SELECT index_col('{Literal(table)}', {id}, {position})"));
                if (string.IsNullOrEmpty(key)) break;
                keys.Add(key);
            }
            index.KeyColumns = keys.ToArray();
        }
        return indexes.ToArray();
    }

    public override bool IndexExists(string table, string name) => GetIndexes(table).Any(i => i.Name == name);
    public override string AddIndex(string table, Index index)
    {
        if (index.KeyColumns.Length == 0) throw new ArgumentException("An index needs key columns.", nameof(index));
        if (index.IncludeColumns.Length != 0 || index.FilterItems.Count != 0)
            throw new NotSupportedException("ASE does not support this index's INCLUDE or filter options.");
        var name = index.Name ?? $"ix_{table}_{string.Join("_", index.KeyColumns)}";
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}{(index.Clustered ? "CLUSTERED " : "NONCLUSTERED ")}INDEX {name} ON {table} ({string.Join(", ", index.KeyColumns)})");
        return name;
    }

    public override void AddColumn(string table, string sqlColumn) => ExecuteNonQuery($"ALTER TABLE {table} ADD {sqlColumn}");

    public override void RemoveIndex(string table, string name) => ExecuteNonQuery($"DROP INDEX {table}.{name}");
    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName) =>
        ExecuteNonQuery($"EXEC sp_rename '{Literal(tableName)}.{Literal(oldColumnName)}', '{Literal(newColumnName)}'");
    public override void RenameTable(string oldName, string newName) =>
        ExecuteNonQuery($"EXEC sp_rename '{Literal(oldName)}', '{Literal(newName)}'");
    public override void RemoveColumn(string tableName, string column) => ExecuteNonQuery($"ALTER TABLE {tableName} DROP {column}");
    public override void RemoveColumnDefaultValue(string table, string column) => ExecuteNonQuery($"ALTER TABLE {table} REPLACE {column} DEFAULT NULL");
    public override void ChangeColumn(string table, Column column)
    {
        var isUniqueSet = column.ColumnProperty.HasFlag(ColumnProperty.Unique);
        column.ColumnProperty &= ~ColumnProperty.Unique;
        var type = _dialect.GetColumnMapper(column).Type;
        var nullable = column.ColumnProperty.HasFlag(ColumnProperty.NotNull) ? "NOT NULL" : "NULL";
        ExecuteNonQuery($"ALTER TABLE {table} MODIFY {column.Name} {type} {nullable}");
        ExecuteNonQuery($"ALTER TABLE {table} REPLACE {column.Name} {(column.DefaultValue == null ? "DEFAULT NULL" : _dialect.Default(column.DefaultValue))}");
        if (isUniqueSet)
            AddUniqueConstraint($"UX_{table}_{column.Name}", table, [column.Name]);
    }

    public override void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns, ForeignKeyConstraintType constraint)
    {
        if (constraint is not (ForeignKeyConstraintType.NoAction or ForeignKeyConstraintType.Restrict))
            throw new NotSupportedException("ASE declarative foreign keys do not support cascading referential actions.");
        ExecuteNonQuery($"ALTER TABLE {childTable} ADD CONSTRAINT {name} FOREIGN KEY ({string.Join(", ", childColumns)}) REFERENCES {parentTable} ({string.Join(", ", parentColumns)})");
    }
}
