using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Firebird;

public class FirebirdTransformationProvider : TransformationProvider
{
    public FirebirdTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        var factory = DbProviderFactoriesHelper.GetFactory(string.IsNullOrEmpty(providerName) ? "FirebirdSql.Data.FirebirdClient" : providerName,
            "FirebirdSql.Data.FirebirdClient", "FirebirdSql.Data.FirebirdClient.FirebirdClientFactory");
        _connection = factory.CreateConnection();
        _connection.ConnectionString = connectionString;
        _connection.Open();
    }

    public FirebirdTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
        : base(dialect, connection, null, scope) { }

    private static string CatalogName(string name) =>
        (name.StartsWith('"') ? name.Trim('"').Replace("\"\"", "\"") : name.ToUpperInvariant()).Replace("'", "''");

    public override void AddColumn(string table, Column column) =>
        AddColumn(table, _dialect.GetAndMapColumnProperties(column).ColumnSql);

    public override void AddTable(string name, string engine, params IDbField[] fields)
    {
        base.AddTable(name, engine, fields);
        foreach (var column in fields.OfType<Column>().Where(c => c.ColumnProperty.HasFlag(ColumnProperty.Indexed)))
            AddIndex(name, new Index { KeyColumns = [column.Name] });
    }

    public override bool TableExists(string table) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='{CatalogName(table)}' AND RDB$VIEW_BLR IS NULL")) > 0;

    public override bool ViewExists(string view) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='{CatalogName(view)}' AND RDB$VIEW_BLR IS NOT NULL")) > 0;

    public override string[] GetTables() => ExecuteStringQuery(
        "SELECT TRIM(RDB$RELATION_NAME) FROM RDB$RELATIONS WHERE COALESCE(RDB$SYSTEM_FLAG,0)=0 AND RDB$VIEW_BLR IS NULL").ToArray();

    // Firebird has no server-wide SQL database catalog; only the attached database is visible.
    public override List<string> GetDatabases() => [_connection.Database];

    public override void DropDatabases(string databaseName)
    {
        if (!string.Equals(databaseName, _connection.Database, StringComparison.Ordinal))
            throw new ArgumentException("Firebird can only drop the currently attached database.", nameof(databaseName));
        // DROP DATABASE is an attachment API operation, not a DSQL statement.
        // Resolve the registered driver's API without adding a driver dependency.
        var method = _connection.GetType().GetMethod("DropDatabase", [typeof(string)])
            ?? throw new NotSupportedException("The registered Firebird driver does not expose DropDatabase(string).");
        var drop = method.CreateDelegate<Action<string>>();
        var connectionString = _connection.ConnectionString;
        _connection.Close();
        drop(connectionString);
    }

    public override string[] GetConstraints(string table) => ExecuteStringQuery(
        $"SELECT TRIM(RDB$CONSTRAINT_NAME) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$RELATION_NAME='{CatalogName(table)}'").ToArray();

    public override bool ConstraintExists(string table, string name) =>
        GetConstraints(table).Any(n => n == CatalogName(name).Replace("''", "'"));

    protected override string GetPrimaryKeyConstraintName(string table) =>
        ExecuteStringQuery($"SELECT TRIM(RDB$CONSTRAINT_NAME) FROM RDB$RELATION_CONSTRAINTS WHERE RDB$RELATION_NAME='{CatalogName(table)}' AND RDB$CONSTRAINT_TYPE='PRIMARY KEY'").FirstOrDefault();

    public override bool PrimaryKeyExists(string table, string name) =>
        string.Equals(GetPrimaryKeyConstraintName(table), CatalogName(name), StringComparison.Ordinal);

    public override Column[] GetColumns(string table)
    {
        var primaryColumns = GetIndexes(table).Where(i => i.PrimaryKey).SelectMany(i => i.KeyColumns).ToHashSet(StringComparer.Ordinal);
        var result = new List<Column>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT TRIM(r.RDB$FIELD_NAME), f.RDB$FIELD_TYPE, r.RDB$NULL_FLAG,
                   r.RDB$DEFAULT_SOURCE, f.RDB$CHARACTER_LENGTH, r.RDB$IDENTITY_TYPE,
                   f.RDB$FIELD_SUB_TYPE, f.RDB$FIELD_PRECISION, f.RDB$FIELD_SCALE
            FROM RDB$RELATION_FIELDS r JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME=r.RDB$FIELD_SOURCE
            WHERE r.RDB$RELATION_NAME='{CatalogName(table)}' ORDER BY r.RDB$FIELD_POSITION
            """);
        while (reader.Read())
        {
            var type = Convert.ToInt32(reader.GetValue(1)) switch
            {
                7 => DbType.Int16, 8 => DbType.Int32, 16 => DbType.Int64, 10 => DbType.Single,
                27 => DbType.Double, 12 => DbType.Date, 13 => DbType.Time, 35 => DbType.DateTime,
                23 => DbType.Boolean, 261 => !reader.IsDBNull(6) && Convert.ToInt32(reader.GetValue(6)) == 1 ? DbType.String : DbType.Binary, _ => DbType.String
            };
            if (!reader.IsDBNull(6) && Convert.ToInt32(reader.GetValue(6)) is 1 or 2 && type is DbType.Int16 or DbType.Int32 or DbType.Int64)
                type = DbType.Decimal;
            var column = new Column(reader.GetString(0), type)
            {
                ColumnProperty = !reader.IsDBNull(2) && Convert.ToInt32(reader.GetValue(2)) == 1 ? ColumnProperty.NotNull : ColumnProperty.Null
            };
            if (type == DbType.Decimal)
            {
                if (!reader.IsDBNull(7)) column.Precision = Convert.ToInt32(reader.GetValue(7));
                if (!reader.IsDBNull(8)) column.Scale = -Convert.ToInt32(reader.GetValue(8));
            }
            if (!reader.IsDBNull(3)) column.DefaultValue = ReadDefault(reader.GetString(3), type);
            if (!reader.IsDBNull(4)) column.Size = Convert.ToInt32(reader.GetValue(4));
            if (Convert.ToInt32(reader.GetValue(1)) == 261 && type == DbType.String) column.Size = int.MaxValue;
            if (!reader.IsDBNull(5)) column.ColumnProperty |= ColumnProperty.Identity;
            if (primaryColumns.Contains(column.Name)) column.ColumnProperty |= ColumnProperty.PrimaryKey;
            result.Add(column);
        }
        return result.ToArray();
    }

    private sealed record DatabaseDefault(string Sql)
    {
        public override string ToString() => Sql;
    }

    private static object ReadDefault(string source, DbType type)
    {
        var value = source.Trim();
        if (value.StartsWith("DEFAULT ", StringComparison.OrdinalIgnoreCase)) value = value[8..].Trim();
        if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;
        if (value.StartsWith("'") && value.EndsWith("'"))
        {
            var literal = value[1..^1].Replace("''", "'");
            if (type is DbType.Date or DbType.DateTime && DateTime.TryParse(literal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return literal;
        }
        if (type == DbType.Int16 && short.TryParse(value, CultureInfo.InvariantCulture, out var small)) return small;
        if (type == DbType.Int32 && int.TryParse(value, CultureInfo.InvariantCulture, out var integer)) return integer;
        if (type == DbType.Int64 && long.TryParse(value, CultureInfo.InvariantCulture, out var large)) return large;
        if (type == DbType.Decimal && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return number;
        if (type == DbType.Boolean && bool.TryParse(value, out var boolean)) return boolean;
        return new DatabaseDefault(value);
    }

    public override void AddColumn(string table, string sqlColumn) =>
        ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} ADD {sqlColumn}");

    public override void RemoveColumn(string tableName, string column)
    {
        if (!ColumnExists(tableName, column))
            throw new MigrationException($"Column '{column}' does not exist in '{tableName}'.");
        var existing = GetColumns(tableName).Single(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
        ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(tableName)} DROP {_dialect.Quote(existing.Name)}");
    }

    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        if (!ColumnExists(tableName, oldColumnName) || ColumnExists(tableName, newColumnName))
            throw new MigrationException("Source column must exist and destination column must not exist.");
        ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(tableName)} ALTER {QuoteColumnNameIfRequired(oldColumnName)} TO {QuoteColumnNameIfRequired(newColumnName)}");
    }

    public override void ChangeColumn(string table, Column column)
    {
        var isUniqueSet = column.ColumnProperty.HasFlag(ColumnProperty.Unique);
        column.ColumnProperty &= ~ColumnProperty.Unique;
        var prefix = $"ALTER TABLE {QuoteTableNameIfRequired(table)} ALTER {QuoteColumnNameIfRequired(column.Name)}";
        var type = _dialect.GetColumnMapper(column).Type;
        ExecuteNonQuery($"{prefix} TYPE {type}");
        if (column.DefaultValue != null || GetColumns(table).Single(c => c.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase)).DefaultValue != null)
            ExecuteNonQuery($"{prefix} {(column.DefaultValue == null ? "DROP DEFAULT" : "SET " + _dialect.Default(column.DefaultValue))}");
        ExecuteNonQuery($"{prefix} {(column.ColumnProperty.HasFlag(ColumnProperty.NotNull) ? "SET" : "DROP")} NOT NULL");
        if (isUniqueSet)
            AddUniqueConstraint($"UX_{table}_{column.Name}", table, [column.Name]);
    }

    public override string AddIndex(string table, Index index)
    {
        if (index.KeyColumns.Length == 0) throw new ArgumentException("An index needs key columns.", nameof(index));
        if (index.IncludeColumns.Length != 0 || index.FilterItems.Count != 0 || index.Clustered)
            throw new NotSupportedException("This Firebird provider supports ordinary and unique indexes without INCLUDE or filters.");
        var name = index.Name ?? $"IX_{table}_{string.Join("_", index.KeyColumns)}";
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {QuoteConstraintNameIfRequired(name)} ON {QuoteTableNameIfRequired(table)} ({string.Join(", ", index.KeyColumns.Select(QuoteColumnNameIfRequired))})");
        return name;
    }

    public override Index[] GetIndexes(string table)
    {
        var result = new Dictionary<string, Index>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"""
            SELECT TRIM(i.RDB$INDEX_NAME), COALESCE(i.RDB$UNIQUE_FLAG,0), TRIM(s.RDB$FIELD_NAME),
                   TRIM(c.RDB$CONSTRAINT_TYPE)
            FROM RDB$INDICES i JOIN RDB$INDEX_SEGMENTS s ON s.RDB$INDEX_NAME=i.RDB$INDEX_NAME
            LEFT JOIN RDB$RELATION_CONSTRAINTS c ON c.RDB$INDEX_NAME=i.RDB$INDEX_NAME
            WHERE i.RDB$RELATION_NAME='{CatalogName(table)}'
            ORDER BY i.RDB$INDEX_NAME, s.RDB$FIELD_POSITION
            """);
        while (reader.Read())
        {
            var name = reader.GetString(0);
            if (!result.TryGetValue(name, out var index))
            {
                var constraint = reader.IsDBNull(3) ? "" : reader.GetString(3);
                index = new Index { Name = name, Unique = Convert.ToInt32(reader.GetValue(1)) == 1,
                    PrimaryKey = constraint == "PRIMARY KEY", UniqueConstraint = constraint == "UNIQUE" };
                result.Add(name, index);
            }
            index.KeyColumns = [..index.KeyColumns, reader.GetString(2)];
        }
        return result.Values.ToArray();
    }

    public override bool IndexExists(string table, string name) =>
        GetIndexes(table).Any(i => i.Name == CatalogName(name));
}
