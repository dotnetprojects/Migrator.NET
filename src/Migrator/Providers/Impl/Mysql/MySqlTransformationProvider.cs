using DotNetProjects.Migrator.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Mysql;

/// <summary>
/// MySql transformation provider
/// </summary>    
public class MySqlTransformationProvider : TransformationProvider
{
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
            ExecuteNonQuery(string.Format("ALTER TABLE {0} DROP FOREIGN KEY {1}", table, _dialect.Quote(name)));
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
        if (ConstraintExists(table, name))
        {
            ExecuteNonQuery(string.Format("ALTER TABLE {0} DROP KEY {1}", table, _dialect.Quote(name)));
        }
    }

    public override bool ConstraintExists(string table, string name)
    {
        if (!TableExists(table))
        {
            return false;
        }

        var sqlConstraint = string.Format("SHOW KEYS FROM {0}", table);

        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, sqlConstraint);

        while (reader.Read())
        {
            if (reader["Key_name"].ToString().ToLower() == name.ToLower())
            {
                return true;
            }
        }

        return false;
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
        var retVal = new List<Index>();

        var sql = @"SHOW INDEX FROM {0}";

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    var idx = new Index
                    {
                        Name = reader.GetString(2),
                        PrimaryKey = reader.GetString(2) == "PRIMARY",
                        Unique = !reader.GetBoolean(1),
                    };
                    //var cols = reader.GetString(7);
                    //cols = cols.Substring(1, cols.Length - 2);
                    //idx.KeyColumns = cols.Split(',');                        
                    retVal.Add(idx);
                }
            }
        }

        return retVal.ToArray();
    }

    public override bool PrimaryKeyExists(string table, string name)
    {
        return ConstraintExists(table, "PRIMARY");
    }

    public override Column[] GetColumns(string table)
    {
        var columns = new List<Column>();
        using (var cmd = CreateCommand())
        using (
            var reader =
                ExecuteQuery(cmd,
                    string.Format("SHOW COLUMNS FROM {0}", table)))
        {
            while (reader.Read())
            {
                var column = new Column(reader.GetString(0), DbType.String);
                var nullableStr = reader.GetString(2);
                var isNullable = nullableStr == "YES";
                var defaultValue = reader.GetValue(4);
                column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

                if (defaultValue != null && defaultValue != DBNull.Value)
                {
                    column.DefaultValue = defaultValue;
                }

                if (column.DefaultValue != null)
                {
                    if (column.Type == DbType.Int16 || column.Type == DbType.Int32 || column.Type == DbType.Int64)
                    {
                        column.DefaultValue = long.Parse(column.DefaultValue.ToString());
                    }
                    else if (column.Type == DbType.UInt16 || column.Type == DbType.UInt32 || column.Type == DbType.UInt64)
                    {
                        column.DefaultValue = ulong.Parse(column.DefaultValue.ToString());
                    }
                    else if (column.Type == DbType.Double || column.Type == DbType.Single)
                    {
                        column.DefaultValue = double.Parse(column.DefaultValue.ToString());
                    }
                    else if (column.Type == DbType.Boolean)
                    {
                        column.DefaultValue = column.DefaultValue.ToString().Trim() == "1" || column.DefaultValue.ToString().Trim().ToUpper() == "TRUE" || column.DefaultValue.ToString().Trim() == "YES";
                    }
                    else if (column.Type == DbType.DateTime || column.Type == DbType.DateTime2)
                    {
                        if (column.DefaultValue is string defVal)
                        {
                            var dt = defVal;
                            if (defVal.StartsWith("'"))
                            {
                                dt = defVal.Substring(1, defVal.Length - 2);
                            }

                            var d = DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                            column.DefaultValue = d;
                        }
                    }
                    else if (column.Type == DbType.Guid)
                    {
                        if (column.DefaultValue is string defVal)
                        {
                            var dt = defVal;
                            if (defVal.StartsWith("'"))
                            {
                                dt = defVal.Substring(1, defVal.Length - 2);
                            }

                            var d = Guid.Parse(dt);
                            column.DefaultValue = d;
                        }
                    }
                }

                columns.Add(column);
            }
        }

        return columns.ToArray();
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
        if (ColumnExists(tableName, newColumnName))
        {
            throw new MigrationException(string.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));
        }

        if (!ColumnExists(tableName, oldColumnName))
        {
            throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", tableName, oldColumnName));
        }

        string definition = null;

        var dropPrimary = false;
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format("SHOW COLUMNS FROM {0} WHERE Field='{1}'", tableName, oldColumnName)))
        {
            if (reader.Read())
            {
                // TODO: Could use something similar to construct the columns in GetColumns
                definition = reader["Type"].ToString();
                if ("NO" == reader["Null"].ToString())
                {
                    definition += " " + "NOT NULL";
                }

                if (!reader.IsDBNull(reader.GetOrdinal("Key")))
                {
                    var key = reader["Key"].ToString();
                    if ("PRI" == key)
                    {
                        //definition += " " + "PRIMARY KEY";
                        dropPrimary = true;
                    }
                    else if ("UNI" == key)
                    {
                        definition += " " + "UNIQUE";
                    }
                }

                if (!reader.IsDBNull(reader.GetOrdinal("Extra")))
                {
                    definition += " " + reader["Extra"];
                }
            }
        }

        if (!string.IsNullOrEmpty(definition))
        {
            if (dropPrimary)
            {
                ExecuteNonQuery(string.Format("ALTER TABLE {0} DROP PRIMARY KEY", tableName));
            }

            ExecuteNonQuery(string.Format("ALTER TABLE {0} CHANGE {1} {2} {3}", tableName, QuoteColumnNameIfRequired(oldColumnName), QuoteColumnNameIfRequired(newColumnName), definition));
            if (dropPrimary)
            {
                ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD PRIMARY KEY({1});", tableName, QuoteColumnNameIfRequired(newColumnName)));
            }
        }
    }

    public string GetDatabase()
    {
        return ExecuteScalar("SELECT DATABASE()") as string;
    }

    public override void RemoveIndex(string table, string name)
    {
        if (IndexExists(table, name))
        {
            ExecuteNonQuery(string.Format("DROP INDEX {1} ON {0}", table, _dialect.Quote(name)));
        }
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SHOW DATABASES");
    }

    public override bool IndexExists(string table, string name)
    {
        return ConstraintExists(table, name);
    }

    public override string Concatenate(params string[] strings)
    {
        return "CONCAT(" + string.Join(", ", strings) + ")";
    }
}
