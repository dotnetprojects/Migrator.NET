using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite.Models;
using Migrator.Framework;
using Migrator.Providers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using Index = Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite
{
    /// <summary>
    /// Summary description for SQLiteTransformationProvider.
    /// </summary>
    public class SQLiteTransformationProvider : TransformationProvider
    {
        private const string IntermediateTableSuffix = "Temp";

        public SQLiteTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
            : base(dialect, connectionString, null, scope)
        {
            CreateConnection(providerName);
        }

        public SQLiteTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
           : base(dialect, connection, null, scope)
        {
        }

        protected virtual void CreateConnection(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                providerName = "System.Data.SQLite";
            }

            var fac = DbProviderFactoriesHelper.GetFactory(providerName, "System.Data.SQLite", "System.Data.SQLite.SQLiteFactory");
            _connection = fac.CreateConnection(); // new SQLiteConnection(_connectionString);
            _connection.ConnectionString = _connectionString;
            _connection.Open();
        }

        public override void AddForeignKey(
            string name,
            string parentTable,
            string[] parentColumns,
            string childTable,
            string[] childColumns,
            ForeignKeyConstraintType constraint)
        {
            var sqliteTableInfo = GetSQLiteTableInfo(childTable);

            var foreignKey = new ForeignKeyConstraint
            {
                // SQLite does not support FK names
                Name = null,
                ParentTable = parentTable,
                ChildTable = childTable,
                ParentColumns = parentColumns,
                ChildColumns = childColumns
            };

            sqliteTableInfo.ForeignKeys.Add(foreignKey);

            RecreateTable(sqliteTableInfo);
        }

        public string[] GetColumnDefs(string table, out string compositeDefSql)
        {
            return ParseSqlColumnDefs(GetSqlCreateTableScript(table), out compositeDefSql);
        }

        public string GetSqlCreateTableScript(string table)
        {
            string sqlCreateTableScript = null;

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, string.Format("SELECT sql FROM sqlite_master WHERE type='table' AND lower(name)=lower('{0}')", table)))
            {
                if (reader.Read())
                {
                    sqlCreateTableScript = (string)reader[0];
                }
            }

            return sqlCreateTableScript;
        }

        public override ForeignKeyConstraint[] GetForeignKeyConstraints(string tableName)
        {
            List<ForeignKeyConstraint> foreignKeyConstraints = [];

            var pragmaForeignKeyListItems = GetForeignKeyListItems(tableName);
            var groups = pragmaForeignKeyListItems.GroupBy(x => x.Id);

            foreach (var group in groups)
            {
                var foreignKeyConstraint = new ForeignKeyConstraint
                {
                    Id = group.First().Id,
                    // SQLite does not support FK names.
                    Name = null,
                    ParentTable = group.First().Table,
                    ParentColumns = group.OrderBy(x => x.Seq).Select(x => x.To).ToArray(),
                    ChildColumns = group.OrderBy(x => x.Seq).Select(x => x.From).ToArray(),
                    ChildTable = tableName,
                    OnDelete = group.First().OnDelete,
                    OnUpdate = group.First().OnUpdate,
                    Match = group.First().Match
                };

                foreignKeyConstraints.Add(foreignKeyConstraint);
            }

            return foreignKeyConstraints.ToArray();
        }

        private List<PragmaForeignKeyListItem> GetForeignKeyListItems(string tableNameNotQuoted)
        {
            List<PragmaForeignKeyListItem> pragmaForeignKeyListItems = [];

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, $"PRAGMA foreign_key_list('{QuoteTableNameIfRequired(tableNameNotQuoted)}')"))
            {
                while (reader.Read())
                {
                    var pragmaForeignKeyListItem = new PragmaForeignKeyListItem
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Seq = reader.GetInt32(reader.GetOrdinal("seq")),
                        Table = reader.GetString(reader.GetOrdinal("table")),
                        From = reader.GetString(reader.GetOrdinal("from")),
                        To = reader.GetString(reader.GetOrdinal("to")),
                        OnUpdate = reader.GetString(reader.GetOrdinal("on_update")),
                        OnDelete = reader.GetString(reader.GetOrdinal("on_delete")),
                        Match = reader.GetString(reader.GetOrdinal("match")),
                    };

                    pragmaForeignKeyListItems.Add(pragmaForeignKeyListItem);
                }
            }

            return pragmaForeignKeyListItems;
        }

        public string[] ParseSqlColumnDefs(string sqldef, out string compositeDefSql)
        {
            if (string.IsNullOrEmpty(sqldef))
            {
                compositeDefSql = null;

                return null;
            }

            sqldef = sqldef.Replace(Environment.NewLine, " ");
            var start = sqldef.IndexOf("(");

            // Code to handle composite primary keys /mol
            var compositeDefIndex = sqldef.IndexOf("PRIMARY KEY ("); // Not ideal to search for a string like this but I'm lazy

            if (compositeDefIndex > -1)
            {
                compositeDefSql = sqldef.Substring(compositeDefIndex, sqldef.LastIndexOf(")") - compositeDefIndex);
                sqldef = sqldef.Substring(0, compositeDefIndex).TrimEnd(',', ' ') + ")";
            }
            else
            {
                compositeDefSql = null;
            }

            var end = sqldef.LastIndexOf(")"); // Changed from 'IndexOf' to 'LastIndexOf' to handle foreign key definitions /mol

            sqldef = sqldef.Substring(0, end);
            sqldef = sqldef.Substring(start + 1);

            var cols = sqldef.Split([',']);

            for (var i = 0; i < cols.Length; i++)
            {
                cols[i] = cols[i].Trim();
            }

            return cols;
        }

        /// <summary>
        /// Turn something like 'columnName INTEGER NOT NULL' into just 'columnName'
        /// </summary>
        public string[] ParseSqlForColumnNames(string sqldef, out string compositeDefSql)
        {
            var parts = ParseSqlColumnDefs(sqldef, out compositeDefSql);

            return ParseSqlForColumnNames(parts);
        }

        public string[] ParseSqlForColumnNames(string[] parts)
        {
            if (null == parts)
            {
                return null;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = ExtractNameFromColumnDef(parts[i]);
            }

            return parts;
        }

        /// <summary>
        /// Name is the first value before the space.
        /// </summary>
        /// <param name="columnDef"></param>
        /// <returns></returns>
        public static string ExtractNameFromColumnDef(string columnDef)
        {
            var idx = columnDef.IndexOf(" ");

            if (idx > 0)
            {
                return columnDef.Substring(0, idx);
            }
            return null;
        }

        public DbType ExtractTypeFromColumnDef(string columnDef)
        {
            var idx = columnDef.IndexOf(" ") + 1;

            if (idx > 0)
            {
                var idy = columnDef.IndexOf(" ", idx) - idx;

                if (idy > 0)
                {
                    return _dialect.GetDbType(columnDef.Substring(idx, idy));
                }
                else
                {
                    return _dialect.GetDbType(columnDef.Substring(idx));
                }
            }
            else
            {
                throw new Exception("Error extracting type from column definition: '" + columnDef + "'");
            }
        }

        public override void RemoveForeignKey(string table, string name)
        {
            //Check the impl...
            return;
        }

        public string[] GetCreateIndexSqlStrings(string table)
        {
            var sqlStrings = new List<string>();

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, string.Format("SELECT sql FROM sqlite_master WHERE type='index' AND sql NOT NULL AND lower(tbl_name)=lower('{0}')", table)))
            {
                while (reader.Read())
                {
                    sqlStrings.Add((string)reader[0]);
                }
            }

            return [.. sqlStrings];
        }

        public void MoveIndexesFromOriginalTable(string origTable, string newTable)
        {
            var indexSqls = GetCreateIndexSqlStrings(origTable);

            foreach (var indexSql in indexSqls)
            {
                var origTableStart = indexSql.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase) + 4;
                var origTableEnd = indexSql.IndexOf("(", origTableStart);

                // First remove original index, because names have to be unique
                var createIndexDef = " INDEX ";
                var indexNameStart = indexSql.IndexOf(createIndexDef, StringComparison.OrdinalIgnoreCase) + createIndexDef.Length;
                ExecuteNonQuery("DROP INDEX " + indexSql.Substring(indexNameStart, origTableStart - 4 - indexNameStart));

                // Create index on new table
                ExecuteNonQuery(indexSql.Substring(0, origTableStart) + newTable + " " + indexSql.Substring(origTableEnd));
            }
        }

        public override void RemoveColumn(string table, string column)
        {
            if (!(TableExists(table) && ColumnExists(table, column)))
            {
                return;
            }

            var newColumns = GetColumns(table).Where(x => x.Name != column).ToArray();

            AddTable(table + "_temp", null, newColumns);
            var colNamesSql = string.Join(", ", newColumns.Select(x => QuoteColumnNameIfRequired(x.Name)));
            ExecuteNonQuery(string.Format("INSERT INTO {0}_temp SELECT {1} FROM {0}", table, colNamesSql));
            RemoveTable(table);
            ExecuteNonQuery(string.Format("ALTER TABLE {0}_temp RENAME TO {0}", table));
        }

        public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
        {
            // Due to old .Net versions we cannot use ThrowIfNullOrWhitespace
            if (string.IsNullOrWhiteSpace(newColumnName))
            {
                throw new Exception("New column name is null or empty");
            }

            if (ColumnExists(tableName, newColumnName))
            {
                throw new MigrationException(string.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));
            }

            if (ColumnExists(tableName, oldColumnName))
            {
                var sqliteTableInfo = GetSQLiteTableInfo(tableName);
                var columnMapping = sqliteTableInfo.ColumnMappings.First(x => x.OldName == oldColumnName);
                columnMapping.NewName = newColumnName;

                var column = sqliteTableInfo.Columns.First(x => x.Name == oldColumnName);
                column.Name = newColumnName;

                var affectedForeignKeys = sqliteTableInfo.ForeignKeys.Where(x => x.ChildColumns.Contains(oldColumnName)).ToList();

                foreach (var foreignKey in affectedForeignKeys)
                {
                    foreignKey.ChildColumns = foreignKey.ChildColumns.Select(x => x == oldColumnName ? newColumnName : x).ToArray();
                }

                RecreateTable(sqliteTableInfo);

                var allTables = GetTables();

                foreach (var allTablesItem in allTables)
                {
                    if (allTablesItem == tableName)
                    {
                        continue;
                    }

                    var sqliteTableInfoOther = GetSQLiteTableInfo(allTablesItem);

                    foreach (var foreignKey in sqliteTableInfoOther.ForeignKeys)
                    {
                        if (foreignKey.ParentTable != tableName)
                        {
                            continue;
                        }

                        foreignKey.ParentColumns = foreignKey.ParentColumns.Select(x => x == oldColumnName ? newColumnName : x).ToArray();
                    }

                    RecreateTable(sqliteTableInfoOther);
                }
            }
            else
            {
                throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", tableName, oldColumnName));
            }
        }

        public override void RemoveColumnDefaultValue(string tableName, string columnName)
        {
            if (!TableExists(tableName))
            {
                throw new Exception("Table does not exist");
            }

            if (!ColumnExists(table: tableName, column: columnName))
            {
                throw new Exception("Column does not exist");
            }

            var sqliteTableInfo = GetSQLiteTableInfo(tableName);

            var column = sqliteTableInfo.Columns.First(x => x.Name == columnName);
            column.DefaultValue = null;

            RecreateTable(sqliteTableInfo);
        }

        public override void AddPrimaryKey(string name, string tableName, params string[] columnNames)
        {
            if (!TableExists(tableName))
            {
                throw new Exception("Table does not exist");
            }

            var sqliteTableInfo = GetSQLiteTableInfo(tableName);

            foreach (var column in sqliteTableInfo.Columns)
            {
                if (columnNames.Contains(column.Name))
                {
                    column.ColumnProperty |= ColumnProperty.PrimaryKey;
                }
            }

            RecreateTable(sqliteTableInfo);
        }

        public override void AddUniqueConstraint(string name, string table, params string[] columns)
        {
            var uniqueConstraint = new Unique() { KeyColumns = columns, Name = name };

            ChangeColumnInternal(table: table, old: [], columns: [uniqueConstraint]);
        }

        public SQLiteTableInfo GetSQLiteTableInfo(string tableName)
        {
            var sqliteTable = new SQLiteTableInfo
            {
                TableNameMapping = new MappingInfo { OldName = tableName, NewName = tableName },
                Columns = GetColumns(tableName).ToList(),
                ForeignKeys = GetForeignKeyConstraints(tableName).ToList(),
                Indexes = GetIndexes(tableName).ToList(),
                Uniques = GetUniques(tableName).ToList()
            };

            sqliteTable.ColumnMappings = sqliteTable.Columns
                .Select(x =>
                    new MappingInfo
                    {
                        OldName = x.Name,
                        NewName = x.Name
                    })
                .ToList();

            return sqliteTable;
        }

        public bool CheckForeignKeyIntegrity()
        {
            ExecuteNonQuery("PRAGMA foreign_keys = ON");

            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, "PRAGMA foreign_key_check");

            if (reader.Read())
            {
                return false;
            }

            return true;
        }

        private void RecreateTable(SQLiteTableInfo sqliteTableInfo)
        {
            var sourceTableQuoted = QuoteTableNameIfRequired(sqliteTableInfo.TableNameMapping.OldName);
            var targetIntermediateTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}{IntermediateTableSuffix}");
            var targetTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}");

            var columnDbFields = sqliteTableInfo.Columns.Cast<IDbField>();
            var foreignKeyDbFields = sqliteTableInfo.ForeignKeys.Cast<IDbField>();
            var indexDbFields = sqliteTableInfo.Indexes.Cast<IDbField>();

            var dbFields = columnDbFields.Concat(foreignKeyDbFields)
                .Concat(indexDbFields)
                .ToArray();

            AddTable(targetIntermediateTableQuoted, null, dbFields);

            var columnMappings = sqliteTableInfo.ColumnMappings
                .OrderBy(x => x.OldName)
                .ToList();

            var sourceColumnsQuotedString = string.Join(", ", columnMappings.Select(x => QuoteColumnNameIfRequired(x.OldName)));
            var targetColumnsQuotedString = string.Join(", ", columnMappings.Select(x => QuoteColumnNameIfRequired(x.NewName)));

            using (var cmd = CreateCommand())
            {
                var sql = $"INSERT INTO {targetIntermediateTableQuoted} ({targetColumnsQuotedString}) SELECT {sourceColumnsQuotedString} FROM {sourceTableQuoted}";
                ExecuteQuery(cmd, sql);
            }

            RemoveTable(sourceTableQuoted);

            using (var cmd = CreateCommand())
            {
                var sql = $"ALTER TABLE {targetIntermediateTableQuoted} RENAME TO {targetTableQuoted}";
                ExecuteQuery(cmd, sql);
            }
        }

        private void ChangeColumnInternal(string table, string[] old, IDbField[] columns)
        {
            var newColumns = GetColumns(table).Where(x => !old.Any(y => x.Name.Equals(y, StringComparison.InvariantCultureIgnoreCase))).ToList();
            var oldColumnNames = newColumns.Select(x => x.Name).ToList();
            newColumns.AddRange(columns.Where(x => x is Column).Cast<Column>());
            oldColumnNames.AddRange(old);

            var newFieldsPlusUnique = newColumns.Cast<IDbField>().ToList();
            newFieldsPlusUnique.AddRange(columns.Where(x => x is Unique));

            AddTable(table + "_temp", null, [.. newFieldsPlusUnique]);
            var colNamesNewSql = string.Join(", ", newColumns.Select(x => x.Name).Select(QuoteColumnNameIfRequired));
            var colNamesSql = string.Join(", ", oldColumnNames.Select(QuoteColumnNameIfRequired));

            using (var cmd = CreateCommand())
            {
                ExecuteQuery(cmd, string.Format("INSERT INTO {1}_temp ({0}) SELECT {2} FROM {1}", colNamesNewSql, table, colNamesSql));
            }

            RemoveTable(table);

            using (var cmd = CreateCommand())
            {
                ExecuteQuery(cmd, string.Format("ALTER TABLE {0}_temp RENAME TO {0}", table));
            }
        }

        public override void AddColumn(string table, Column column)
        {
            var backUp = column.ColumnProperty;
            column.ColumnProperty &= ~ColumnProperty.PrimaryKey;
            column.ColumnProperty &= ~ColumnProperty.Identity;
            base.AddColumn(table, column);
            column.ColumnProperty = backUp;

            if (backUp.HasFlag(ColumnProperty.PrimaryKey) || backUp.HasFlag(ColumnProperty.Identity))
            {
                ChangeColumn(table, column);
            }
        }

        public override void ChangeColumn(string table, Column column)
        {
            if (
                    (column.ColumnProperty & ColumnProperty.PrimaryKey) != ColumnProperty.PrimaryKey &&
                    (column.ColumnProperty & ColumnProperty.Unique) != ColumnProperty.Unique &&
                    ((column.ColumnProperty & ColumnProperty.NotNull) != ColumnProperty.NotNull || column.DefaultValue != null) &&
                    (column.DefaultValue == null || column.DefaultValue.ToString() != "'CURRENT_TIME'" && column.DefaultValue.ToString() != "'CURRENT_DATE'" && column.DefaultValue.ToString() != "'CURRENT_TIMESTAMP'")
                )
            {
                var tempColumn = "temp_" + column.Name;
                RenameColumn(table, column.Name, tempColumn);
                AddColumn(table, column);

                using (var cmd = CreateCommand())
                {
                    ExecuteQuery(cmd, string.Format("UPDATE {0} SET {1}={2}", table, column.Name, tempColumn));
                }

                RemoveColumn(table, tempColumn);
            }
            else
            {
                var newColumns = GetColumns(table).ToArray();

                for (var i = 0; i < newColumns.Count(); i++)
                {
                    if (newColumns[i].Name == column.Name)
                    {
                        newColumns[i] = column;
                        break;
                    }
                }

                AddTable(table + "_temp", null, newColumns);

                var colNamesSql = string.Join(", ", newColumns.Select(x => x.Name).Select(x => QuoteColumnNameIfRequired(x)));

                using (var cmd = CreateCommand())
                {
                    ExecuteQuery(cmd, string.Format("INSERT INTO {0}_temp SELECT {1} FROM {0}", table, colNamesSql));
                }

                RemoveTable(table);

                using (var cmd = CreateCommand())
                {
                    ExecuteQuery(cmd, string.Format("ALTER TABLE {0}_temp RENAME TO {0}", table));
                }
            }
        }

        public override int TruncateTable(string table)
        {
            return ExecuteNonQuery(string.Format("DELETE FROM {0} ", table));
        }

        public override bool TableExists(string table)
        {
            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, string.Format("SELECT name FROM sqlite_master WHERE type='table' and lower(name)=lower('{0}')", table));

            return reader.Read();
        }

        public override bool ViewExists(string view)
        {
            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, string.Format("SELECT name FROM sqlite_master WHERE type='view' and lower(name)=lower('{0}')", view));

            return reader.Read();
        }

        public override List<string> GetDatabases()
        {
            throw new NotImplementedException();
        }

        public override bool ConstraintExists(string table, string name)
        {
            return false;
        }

        public override string[] GetConstraints(string table)
        {
            return [];
        }

        public override string[] GetTables()
        {
            var tables = new List<string>();

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name"))
            {
                while (reader.Read())
                {
                    tables.Add((string)reader[0]);
                }
            }

            return [.. tables];
        }

        public override Column[] GetColumns(string tableName)
        {
            var pragmaTableInfoItems = GetPragmaTableInfoItems(tableName);


            var columns = new List<Column>();

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, string.Format("PRAGMA table_info('{0}')", tableName)))
            {
                while (reader.Read())
                {
                    var column = new Column((string)reader[1])
                    {
                        Type = _dialect.GetDbTypeFromString((string)reader[2])
                    };

                    if (Convert.ToBoolean(reader[3]))
                    {
                        column.ColumnProperty |= ColumnProperty.NotNull;
                    }
                    else
                    {
                        column.ColumnProperty |= ColumnProperty.Null;
                    }

                    var defValue = reader[4] == DBNull.Value ? null : reader[4];

                    if (defValue is string v && v.StartsWith("'") && v.EndsWith("'"))
                    {
                        column.DefaultValue = v.Substring(1, v.Length - 2);
                    }
                    else
                    {
                        column.DefaultValue = defValue;
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
                            column.DefaultValue = column.DefaultValue.ToString().Trim() == "1" || column.DefaultValue.ToString().Trim().ToUpper() == "TRUE";
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

                    if (Convert.ToBoolean(reader[5]))
                    {
                        column.ColumnProperty |= ColumnProperty.PrimaryKey;
                    }

                    columns.Add(column);

                }
            }

            return [.. columns];
        }

        public bool IsNullable(string columnDef)
        {
            return !columnDef.Contains("NOT NULL");
        }

        public bool ColumnMatch(string column, string columnDef)
        {
            return columnDef.StartsWith(column + " ") || columnDef.StartsWith(_dialect.Quote(column));
        }

        public override bool IndexExists(string table, string name)
        {
            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, string.Format("SELECT name FROM sqlite_master WHERE type='index' and lower(name)=lower('{0}')", name));

            return reader.Read();
        }

        public override Index[] GetIndexes(string table)
        {
            var indexes = new List<Index>();

            var sql = @"SELECT type, name, tbl_name, sql FROM sqlite_master WHERE type = 'index' AND lower(tbl_name) = lower('{0}');";

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
            {
                while (reader.Read())
                {
                    string idxSql = null;

                    if (!reader.IsDBNull(3))
                    {
                        idxSql = reader.GetString(3);
                    }

                    var idx = new Index
                    {
                        Name = reader.GetString(1)
                    };

                    idx.PrimaryKey = idx.Name.StartsWith("sqlite_autoindex_");
                    idx.Unique = idx.Name.StartsWith("sqlite_autoindex_") || idxSql != null && idxSql.Contains("UNIQUE");

                    indexes.Add(idx);
                }
            }

            foreach (var idx in indexes)
            {
                sql = "PRAGMA index_info(\"" + idx.Name + "\")";
                using var cmd = CreateCommand();
                using var reader = ExecuteQuery(cmd, sql);

                var columns = new List<string>();

                while (reader.Read())
                {
                    columns.Add(reader.GetString(2));
                }

                idx.KeyColumns = columns.ToArray();
            }

            return [.. indexes];
        }

        public override void AddTable(string name, string engine, params IDbField[] fields)
        {
            var columns = fields.Where(x => x is Column).Cast<Column>().ToArray();

            var pks = GetPrimaryKeys(columns);
            var compoundPrimaryKey = pks.Count > 1;

            var columnProviders = new List<ColumnPropertiesMapper>(columns.Length);

            foreach (var column in columns)
            {
                // Remove the primary key notation if compound primary key because we'll add it back later
                if (compoundPrimaryKey && column.IsPrimaryKey)
                {
                    column.ColumnProperty ^= ColumnProperty.PrimaryKey;
                    column.ColumnProperty |= ColumnProperty.NotNull; // PK is always not-null
                }

                var mapper = _dialect.GetAndMapColumnProperties(column);
                columnProviders.Add(mapper);
            }

            var columnsAndIndexes = JoinColumnsAndIndexes(columnProviders);

            var table = _dialect.TableNameNeedsQuote ? _dialect.Quote(name) : QuoteTableNameIfRequired(name);
            StringBuilder stringBuilder = new();

            stringBuilder.Append(string.Format("CREATE TABLE {0} ({1}", table, columnsAndIndexes));

            if (compoundPrimaryKey)
            {
                stringBuilder.Append(string.Format(", PRIMARY KEY ({0}) ", string.Join(",", pks.ToArray())));
            }

            var uniques = fields.Where(x => x is Unique).Cast<Unique>().ToArray();

            foreach (var u in uniques)
            {
                var nm = "";

                if (!string.IsNullOrEmpty(u.Name))
                {
                    nm = string.Format(" CONSTRAINT {0}", u.Name);
                }

                stringBuilder.Append(string.Format(",{0} UNIQUE ({1})", nm, string.Join(",", u.KeyColumns)));
            }

            var foreignKeys = fields.Where(x => x is ForeignKeyConstraint).Cast<ForeignKeyConstraint>().ToArray();

            foreach (var fk in foreignKeys)
            {
                // Since in SQLite the foreign key name can be given as 
                // CONSTRAINT <FK name>
                // but not being stored in any way hence not being retrievable using foreign_key_list
                // we leave it out in the following string.

                var sourceColumnNamesQuotedString = string.Join(", ", fk.ChildColumns.Select(QuoteColumnNameIfRequired));
                var parentColumnNamesQuotedString = string.Join(", ", fk.ParentColumns.Select(QuoteColumnNameIfRequired));
                var parentTableNameQuoted = QuoteTableNameIfRequired(fk.ParentTable);

                var foreignKeyString = $", FOREIGN KEY ({sourceColumnNamesQuotedString}) REFERENCES {parentTableNameQuoted}({parentColumnNamesQuotedString})";

                stringBuilder.Append(foreignKeyString);
            }

            stringBuilder.Append(")");

            ExecuteNonQuery(stringBuilder.ToString());

            var indexes = fields.Where(x => x is Index)
                .Cast<Index>()
                .ToArray();

            foreach (var index in indexes)
            {
                AddIndex(name, index);
            }
        }

        protected override string GetPrimaryKeyConstraintName(string table)
        {
            throw new NotImplementedException();
        }

        public override void RemovePrimaryKey(string table)
        {
            if (!TableExists(table))
            {
                return;
            }

            var columnDefs = GetColumns(table);

            foreach (var columnDef in columnDefs.Where(columnDef => columnDef.IsPrimaryKey))
            {
                columnDef.ColumnProperty = columnDef.ColumnProperty.Clear(ColumnProperty.PrimaryKey);
                columnDef.ColumnProperty = columnDef.ColumnProperty.Clear(ColumnProperty.PrimaryKeyWithIdentity);
            }

            ChangeColumnInternal(table, [.. columnDefs.Select(x => x.Name)], columnDefs);
        }

        public override void RemoveAllIndexes(string table)
        {
            if (!TableExists(table))
            {
                return;
            }

            var columnDefs = GetColumns(table);

            foreach (var columnDef in columnDefs.Where(columnDef => columnDef.IsPrimaryKey))
            {
                columnDef.ColumnProperty = columnDef.ColumnProperty.Clear(ColumnProperty.PrimaryKey);
                columnDef.ColumnProperty = columnDef.ColumnProperty.Clear(ColumnProperty.PrimaryKeyWithIdentity);
                columnDef.ColumnProperty = columnDef.ColumnProperty.Clear(ColumnProperty.Unique);
                columnDef.ColumnProperty = columnDef.ColumnProperty.Clear(ColumnProperty.Indexed);
            }

            ChangeColumnInternal(table, [.. columnDefs.Select(x => x.Name)], columnDefs);
        }

        public List<Unique> GetUniques(string tableName)
        {
            List<Unique> uniques = [];

            var pragmaIndexListItems = GetPragmaIndexListItems(tableName);

            var uniqueConstraints = pragmaIndexListItems.Where(x => x.Unique && x.Origin == "u")
                .ToList();

            foreach (var uniqueConstraint in uniqueConstraints)
            {
                var indexInfos = GetPragmaIndexInfo(uniqueConstraint.Name);

                var columns = indexInfos.OrderBy(x => x.SeqNo)
                    .Select(x => x.Name)
                    .ToArray();

                var unique = new Unique
                {
                    Name = uniqueConstraint.Name,
                    KeyColumns = columns
                };

                uniques.Add(unique);
            }

            return uniques;
        }

        public List<PragmaIndexInfoItem> GetPragmaIndexInfo(string indexNameNotQuoted)
        {
            List<PragmaIndexInfoItem> pragmaIndexInfoItems = [];

            var quotedIndexName = QuoteTableNameIfRequired(indexNameNotQuoted);

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, $"PRAGMA index_info({quotedIndexName})"))
            {
                while (reader.Read())
                {
                    var pragmaIndexInfoItem = new PragmaIndexInfoItem
                    {
                        SeqNo = reader.GetInt32(reader.GetOrdinal("seqno")),
                        Cid = reader.GetInt32(reader.GetOrdinal("cid")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                    };

                    pragmaIndexInfoItems.Add(pragmaIndexInfoItem);
                }
            }

            return pragmaIndexInfoItems;
        }

        public List<PragmaIndexListItem> GetPragmaIndexListItems(string tableNameNotQuoted)
        {
            List<PragmaIndexListItem> pragmaIndexListItems = [];

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, $"PRAGMA index_list({QuoteTableNameIfRequired(tableNameNotQuoted)})"))
            {
                while (reader.Read())
                {
                    var pragmaIndexListItem = new PragmaIndexListItem
                    {
                        Seq = reader.GetInt32(reader.GetOrdinal("seq")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        Unique = reader.GetInt32(reader.GetOrdinal("unique")) == 1,
                        Origin = reader.GetString(reader.GetOrdinal("origin")),
                        Partial = reader.GetInt32(reader.GetOrdinal("partial")) == 1
                    };

                    pragmaIndexListItems.Add(pragmaIndexListItem);
                }
            }

            return pragmaIndexListItems;
        }

        public List<PragmaTableInfoItem> GetPragmaTableInfoItems(string tableNameNotQuoted)
        {
            List<PragmaTableInfoItem> pragmaTableInfoItems = [];

            using (var cmd = CreateCommand())
            using (var reader = ExecuteQuery(cmd, $"PRAGMA table_info({QuoteTableNameIfRequired(tableNameNotQuoted)})"))
            {
                while (reader.Read())
                {
                    var pragmaTableInfoItem = new PragmaTableInfoItem
                    {
                        Cid = reader.GetInt32(reader.GetOrdinal("cid")),
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        Type = reader.GetString(reader.GetOrdinal("type")),
                        NotNull = reader.GetInt32(reader.GetOrdinal("notnull")) == 1,
                        DfltValue = reader.GetString(reader.GetOrdinal("dflt_value")),
                        Pk = reader.GetInt32(reader.GetOrdinal("pk")),
                    };

                    pragmaTableInfoItems.Add(pragmaTableInfoItem);
                }
            }

            return pragmaTableInfoItems;
        }

        protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
        {
            if (value is ushort)
            {
                parameter.DbType = DbType.Int32;
                parameter.Value = Convert.ToInt32(value);
            }
            else if (value is uint)
            {
                parameter.DbType = DbType.Int64;
                parameter.Value = Convert.ToInt64(value);
            }
            else if (value is Guid || value is Guid?)
            {
                parameter.DbType = DbType.Binary;
                parameter.Value = ((Guid)value).ToByteArray();
            }
            else
            {
                base.ConfigureParameterWithValue(parameter, index, value);
            }
        }
    }
}
