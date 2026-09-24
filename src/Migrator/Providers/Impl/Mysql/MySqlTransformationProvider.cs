using DotNetProjects.Migrator.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Mysql;

/// <summary>
/// MySql transformation provider
/// </summary>    
public class MySqlTransformationProvider : TransformationProvider
{
    public override void AddColumn(string table, Column column, PrimaryKeyConstraint primaryKey)
    {
        var definition = PrepareColumnWithPrimaryKey(table, column, primaryKey);
        // AUTO_INCREMENT must be indexed in the same ALTER statement, including on MariaDB.
        ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} ADD COLUMN {_dialect.GetAndMapColumnProperties(definition).ColumnSql}, ADD {_dialect.GetTableConstraintSql(primaryKey)}");
    }

    public MySqlTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope) // we ignore schemas for MySql (schema == database for MySql)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "MySql.Data.MySqlClient";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, "MySql.Data", "MySql.Data.MySqlClient.MySqlClientFactory");
        _connection = fac.CreateConnection(); //new MySqlConnection(_connectionString) {ConnectionString = _connectionString};
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }

    public MySqlTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, null, scope)
    {
    }

    public override void RemoveForeignKey(string table, string name)
    {
        if (ForeignKeyExists(table, name))
        {
            ExecuteNonQuery(string.Format("ALTER TABLE {0} DROP FOREIGN KEY {1}", table, _dialect.QuoteIdentifier(name)));
        }
    }

    public override void RemoveAllIndexes(string table)
    {
        var qry = string.Format(@"SELECT k.TABLE_NAME, i.CONSTRAINT_NAME, i.CONSTRAINT_TYPE
                                                    FROM information_schema.KEY_COLUMN_USAGE k 
                                                    INNER JOIN information_schema.TABLE_CONSTRAINTS i 
                                                    ON i.CONSTRAINT_NAME = k.CONSTRAINT_NAME AND i.TABLE_NAME = k.TABLE_NAME 
                                                    WHERE k.REFERENCED_TABLE_SCHEMA='{0}' AND
                                                    (k.REFERENCED_TABLE_NAME='{1}') OR (k.TABLE_NAME='{1}')", GetDatabase(), table);

        var l = new List<Tuple<string, string, string>>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, qry))
        {
            while (reader.Read())
            {
                l.Add(new Tuple<string, string, string>(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        foreach (var tuple in l)
        {
            if (tuple.Item3 == "FOREIGN KEY")
            {
                RemoveForeignKey(tuple.Item1, tuple.Item2);
            }
            else if (tuple.Item3 == "PRIMARY KEY")
            {
                try
                {
                    ExecuteNonQuery(string.Format("ALTER TABLE {0} DROP PRIMARY KEY", table));
                }
                catch (Exception)
                { }
            }
            else if (tuple.Item3 == "UNIQUE")
            {
                RemoveIndex(tuple.Item1, tuple.Item2);
            }
        }
    }

    public override void RemoveAllForeignKeys(string tableName, string columnName)
    {
        var qry = string.Format(@"SELECT k.TABLE_NAME, i.CONSTRAINT_NAME
                                                    FROM information_schema.KEY_COLUMN_USAGE k 
                                                    INNER JOIN information_schema.TABLE_CONSTRAINTS i 
                                                    ON i.CONSTRAINT_NAME = k.CONSTRAINT_NAME AND i.TABLE_NAME = k.TABLE_NAME 
                                                    WHERE k.REFERENCED_TABLE_SCHEMA='{0}' AND  i.CONSTRAINT_TYPE = 'FOREIGN KEY' AND
                                                    (k.REFERENCED_TABLE_NAME='{1}' AND REFERENCED_COLUMN_NAME='{2}') OR (k.TABLE_NAME='{1}' AND COLUMN_NAME='{2}')", GetDatabase(), tableName, columnName);

        if (string.IsNullOrEmpty(columnName))
        {
            qry = string.Format(@"SELECT k.TABLE_NAME, i.CONSTRAINT_NAME
                                                    FROM information_schema.KEY_COLUMN_USAGE k 
                                                    INNER JOIN information_schema.TABLE_CONSTRAINTS i 
                                                    ON i.CONSTRAINT_NAME = k.CONSTRAINT_NAME AND i.TABLE_NAME = k.TABLE_NAME 
                                                    WHERE k.REFERENCED_TABLE_SCHEMA='{0}' AND i.CONSTRAINT_TYPE = 'FOREIGN KEY' AND
                                                    (k.REFERENCED_TABLE_NAME='{1}') OR (k.TABLE_NAME='{1}')", GetDatabase(), tableName);
        }
        var l = new List<Tuple<string, string>>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, qry))
        {
            while (reader.Read())
            {
                l.Add(new Tuple<string, string>(reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach (var tuple in l)
        {
            RemoveForeignKey(tuple.Item1, tuple.Item2);
        }
    }

    public override void RemoveConstraint(string table, string name)
    {
        var type = Convert.ToString(ExecuteScalar($"SELECT CONSTRAINT_TYPE FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table.Replace("'", "''")}' AND CONSTRAINT_NAME='{name.Replace("'", "''")}'"));
        var action = type switch
        {
            "PRIMARY KEY" => "DROP PRIMARY KEY",
            "FOREIGN KEY" => "DROP FOREIGN KEY " + _dialect.QuoteIdentifier(name),
            "UNIQUE" => "DROP INDEX " + _dialect.QuoteIdentifier(name),
            "CHECK" => (_dialect is MariaDBDialect ? "DROP CONSTRAINT " : "DROP CHECK ") + _dialect.QuoteIdentifier(name),
            _ => throw new MigrationException($"Constraint '{name}' does not exist")
        };
        ExecuteNonQuery($"ALTER TABLE {_dialect.Quote(table)} {action}");
    }

    public override bool ConstraintExists(string table, string name)
    {
        return Convert.ToInt32(ExecuteScalar($"SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table.Replace("'", "''")}' AND CONSTRAINT_NAME='{name.Replace("'", "''")}'")) > 0;
    }

    public bool ForeignKeyExists(string table, string name)
    {
        if (!TableExists(table))
        {
            return false;
        }

        var sqlConstraint = string.Format(@"SELECT distinct i.CONSTRAINT_NAME
                                                    FROM information_schema.TABLE_CONSTRAINTS i 
                                                    INNER JOIN information_schema.KEY_COLUMN_USAGE k 
                                                    ON i.CONSTRAINT_NAME = k.CONSTRAINT_NAME 
                                                    WHERE i.CONSTRAINT_TYPE = 'FOREIGN KEY' 
                                                    AND i.TABLE_SCHEMA = '{1}'
                                                    AND i.TABLE_NAME = '{0}';", table, GetDatabase());

        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, sqlConstraint);

        while (reader.Read())
        {
            if (reader["CONSTRAINT_NAME"].ToString().ToLower() == name.ToLower())
            {
                return true;
            }
        }

        return false;
    }

    public override Index[] GetIndexes(string table)
    {
        if (!TableExists(table)) return [];
        var constraints = ExecuteStringQuery($"SELECT CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table.Replace("'", "''")}' AND CONSTRAINT_TYPE='UNIQUE'").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var indexes = new Dictionary<string, Index>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"SHOW INDEX FROM {_dialect.Quote(table)}");
        var columns = new Dictionary<string, SortedDictionary<int, string>>();
        while (reader.Read())
        {
            var name = Convert.ToString(reader["Key_name"]);
            if (!indexes.ContainsKey(name))
            {
                indexes[name] = new Index { Name = name, PrimaryKey = name == "PRIMARY", UniqueConstraint = constraints.Contains(name), Unique = Convert.ToInt32(reader["Non_unique"]) == 0 };
                columns[name] = new SortedDictionary<int, string>();
            }
            columns[name][Convert.ToInt32(reader["Seq_in_index"])] = Convert.ToString(reader["Column_name"]);
        }
        foreach (var item in indexes) item.Value.KeyColumns = columns[item.Key].Values.ToArray();
        return indexes.Values.ToArray();
    }

    public override bool PrimaryKeyExists(string table, string name)
    {
        return ConstraintExists(table, "PRIMARY");
    }

    public override Column[] GetColumns(string table)
    {
        var columns = new List<Column>();
        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, EXTRA, CHARACTER_MAXIMUM_LENGTH, COLUMN_KEY, COLUMN_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table.Replace("'", "''")}' ORDER BY ORDINAL_POSITION");
        while (reader.Read())
        {
            var type = reader.GetString(1) switch
            {
                "smallint" => DbType.Int16, "int" or "integer" or "mediumint" => DbType.Int32,
                "bigint" => DbType.Int64, "tinyint" => reader.GetString(7).StartsWith("tinyint(1)", StringComparison.OrdinalIgnoreCase) ? DbType.Boolean : DbType.Byte,
                "decimal" or "numeric" => DbType.Decimal, "double" => DbType.Double, "float" => DbType.Single,
                "date" => DbType.Date, "datetime" or "timestamp" => DbType.DateTime, "time" => DbType.Time,
                "tinyblob" or "mediumblob" or "blob" or "binary" or "varbinary" or "longblob" => DbType.Binary, _ => DbType.String
            };
            var column = new Column(reader.GetString(0), type);
            column.IsNullable = reader.GetString(2) == "YES";
            if (reader.GetString(4).Contains("auto_increment")) column.IsIdentity = true;
            if (!reader.IsDBNull(3)) column.DefaultValue = ReadDefault(reader.GetString(3), type, reader.GetString(4));
            if (type == DbType.Decimal)
            {
                if (!reader.IsDBNull(8)) column.Precision = Convert.ToInt32(reader.GetValue(8));
                if (!reader.IsDBNull(9)) column.Scale = Convert.ToInt32(reader.GetValue(9));
            }
            if (!reader.IsDBNull(5)) column.Size = (int)Math.Min(int.MaxValue, Convert.ToInt64(reader.GetValue(5)));
            columns.Add(column);
        }
        return columns.ToArray();
    }

    // Non-string objects retain SQL expression semantics in Dialect.Default.
    private sealed record DatabaseDefault(string Sql)
    {
        public override string ToString() => Sql;
    }

    private object ReadDefault(string value, DbType type, string extra)
    {
        if (_dialect is MariaDBDialect)
        {
            if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;
            if (value.StartsWith("'") && value.EndsWith("'"))
                value = value[1..^1].Replace("''", "'").Replace("\\'", "'").Replace("\\\\", "\\");
            else if (type == DbType.String) return new DatabaseDefault(value);
        }
        if (extra.Contains("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase) ||
            (type == DbType.DateTime && value.StartsWith("current_timestamp", StringComparison.OrdinalIgnoreCase)))
            return new DatabaseDefault(value);
        return type switch
        {
            DbType.Time => TimeOnly.Parse(value, CultureInfo.InvariantCulture),
            DbType.Boolean => value != "0",
            DbType.Byte => byte.Parse(value, CultureInfo.InvariantCulture),
            DbType.Int16 => short.Parse(value, CultureInfo.InvariantCulture),
            DbType.Int32 => int.Parse(value, CultureInfo.InvariantCulture),
            DbType.Int64 => long.Parse(value, CultureInfo.InvariantCulture),
            DbType.Decimal => decimal.Parse(value, CultureInfo.InvariantCulture),
            DbType.Double => double.Parse(value, CultureInfo.InvariantCulture),
            DbType.Single => float.Parse(value, CultureInfo.InvariantCulture),
            DbType.Date or DbType.DateTime => DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture), DateTimeKind.Utc),
            _ => value
        };
    }

    public override string[] GetTables()
    {
        var tables = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, "SHOW TABLES"))
        {
            while (reader.Read())
            {
                tables.Add((string)reader[0]);
            }
        }

        return tables.ToArray();
    }

    public override void ChangeColumn(string table, string sqlColumn)
    {
        ExecuteNonQuery(string.Format("ALTER TABLE {0} MODIFY {1}", table, sqlColumn));
    }

    public override void AddTable(string name, params IDbField[] columns)
    {
        AddTable(name, "INNODB", columns);
    }

    public override void AddTable(string name, string engine, string columns)
    {
        var sqlCreate = string.Format("CREATE TABLE {0} ({1}) ENGINE = {2}", name, columns, engine);
        ExecuteNonQuery(sqlCreate);
    }

    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        if (!ColumnExists(tableName, oldColumnName) || ColumnExists(tableName, newColumnName))
            throw new MigrationException("Source column must exist and destination column must not exist.");
        ExecuteNonQuery($"ALTER TABLE {_dialect.Quote(tableName)} RENAME COLUMN {_dialect.Quote(oldColumnName)} TO {_dialect.Quote(newColumnName)}");
    }

    public string GetDatabase()
    {
        return ExecuteScalar("SELECT DATABASE()") as string;
    }

    public override void RemoveIndex(string table, string name)
    {
        if (IndexExists(table, name))
        {
            ExecuteNonQuery(string.Format("DROP INDEX {1} ON {0}", table, _dialect.QuoteIdentifier(name)));
        }
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SHOW DATABASES");
    }

    public override bool IndexExists(string table, string name)
    {
        return GetIndexes(table).Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public override string Concatenate(params string[] strings)
    {
        return "CONCAT(" + string.Join(", ", strings) + ")";
    }
    public override bool TableExists(string table) =>
        Convert.ToInt32(ExecuteScalar($"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE' AND TABLE_NAME='{table.Replace("'", "''")}'")) > 0;

    public override bool ViewExists(string view) =>
        Convert.ToInt32(ExecuteScalar($"SELECT COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{view.Replace("'", "''")}'")) > 0;

    public override string AddIndex(string table, Index index)
    {
        if (index.KeyColumns.Length == 0) throw new ArgumentException("An index needs key columns.", nameof(index));
        if (index.IncludeColumns.Length != 0 || index.FilterItems.Count != 0 || index.Clustered)
            throw new NotSupportedException("MySQL and MariaDB do not support included columns, filtered indexes or explicit clustered indexes.");
        var name = index.Name ?? $"IX_{table}_{string.Join("_", index.KeyColumns)}";
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {_dialect.QuoteIdentifier(name)} ON {_dialect.Quote(table)} ({string.Join(", ", index.KeyColumns.Select(_dialect.Quote))})");
        return name;
    }

    protected override string GetPrimaryKeyConstraintName(string table) =>
        ConstraintExists(table, "PRIMARY") ? "PRIMARY" : null;

}
