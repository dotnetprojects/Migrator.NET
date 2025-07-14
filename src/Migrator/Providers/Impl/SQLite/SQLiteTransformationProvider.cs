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
using System.Text.RegularExpressions;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using Index = Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite
{
    /// <summary>
    /// Summary description for SQLiteTransformationProvider.
    /// </summary>
    public partial class SQLiteTransformationProvider : TransformationProvider
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
            string childTable,
            string[] childColumns,
            string parentTable,
            string[] parentColumns,
            ForeignKeyConstraintType constraint)
        {
            var sqliteTableInfo = GetSQLiteTableInfo(childTable);

            var foreignKey = new ForeignKeyConstraint
            {
                // SQLite does not support FK names
                ChildColumns = childColumns,
                ChildTable = childTable,
                Name = name,
                ParentColumns = parentColumns,
                ParentTable = parentTable,
            };

            sqliteTableInfo.ForeignKeys
                .Add(foreignKey);

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
                    ChildColumns = group.OrderBy(x => x.Seq).Select(x => x.From).ToArray(),
                    ChildTable = tableName,
                    Match = group.First().Match,
                    Name = null,
                    OnDelete = group.First().OnDelete,
                    OnUpdate = group.First().OnUpdate,
                    ParentColumns = group.OrderBy(x => x.Seq).Select(x => x.To).ToArray(),
                    ParentTable = group.First().Table,
                };

                foreignKeyConstraints.Add(foreignKeyConstraint);
            }

            var createTableScript = GetSqlCreateTableScript(tableName);
            var regEx = ForeignKeyRegex();
            var matchesCollection = regEx.Matches(createTableScript);
            var matches = matchesCollection.Cast<Match>().ToList().Where(x => x.Success).Select(x => x.Value).ToList();

            if (matches.Count != foreignKeyConstraints.Count)
            {
                throw new Exception($"Cannot extract all foreign keys out of the create table script in SQLite. Did you use a name as foreign key constraint for all constraints in table {tableName}?");
            }

            foreach (var foreignKeyConstraint in foreignKeyConstraints)
            {
                var regexParenthesis = ForeignKeyParenthesisRegex();
                regexParenthesis.Matches()
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

        public override void RemoveColumn(string tableName, string column)
        {
            // In SQLite we need to recreate the table even if we only want to add, alter or drop a foreign key. So we not only recreate the table given 
            // as parameter but also the tables with FKs pointing to the column you want to remove.
            // In order to perform it smoothly, the PRAGMA foreign keys should be set off.

            var isPragmaForeignKeysOn = IsPragmaForeignKeysOn();

            if (isPragmaForeignKeysOn)
            {
                throw new Exception($"{nameof(RemoveColumn)} requires foreign keys off.");
            }

            if (!TableExists(tableName))
            {
                throw new Exception("Table does not exist");
            }

            if (!ColumnExists(tableName, column))
            {
                throw new Exception("Column does not exist");
            }

            var sqliteInfoMainTable = GetSQLiteTableInfo(tableName);

            if (!sqliteInfoMainTable.ColumnMappings.Any(x => x.OldName == column))
            {
                throw new Exception("Column not found");
            }

            // We throw if all of the conditions are fulfilled:
            //   - the unique constraint is a composite constraint (more than one column)
            //   - the column to be removed is part of the constraint
            // In case of single constraint we remove it silently as it is not needed any more
            var isColumnInUniqueConstraint = sqliteInfoMainTable.Uniques
                .Where(x => x.KeyColumns.Length > 1)
                .SelectMany(x => x.KeyColumns)
                .Distinct()
                .Any(x => x.Equals(column, StringComparison.OrdinalIgnoreCase));

            if (isColumnInUniqueConstraint)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.Append("Found composite unique constraint where the column that you want to remove is part of. Remove the unique constraints first before you remove the column.");
                stringBuilder.Append("Other unique constraints(if exists) that contains only the column to be removed are dropped silently.");

                throw new Exception(stringBuilder.ToString());
            }

            var isColumnInIndex = sqliteInfoMainTable.Indexes
                .Where(x => x.KeyColumns.Length > 1)
                .SelectMany(x => x.KeyColumns)
                .Distinct()
                .Any(x => x.Equals(column, StringComparison.OrdinalIgnoreCase));

            if (isColumnInIndex)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.Append("Found composite index where the column that you want to remove is part of. Remove the indexes first before you remove the column.");
                stringBuilder.Append("Other indexes(if exists) that contains only the column to be removed are dropped silently.");

                throw new Exception(stringBuilder.ToString());
            }

            var isColumnInForeignKey = sqliteInfoMainTable.ForeignKeys
                .Where(x => x.ChildColumns.Length > 1)
                .SelectMany(x => x.ChildColumns)
                .Distinct()
                .Any(x => x.Equals(column, StringComparison.OrdinalIgnoreCase));

            if (isColumnInForeignKey)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.Append("Found foreign key with more than two columns with one column is the column you want to remove. Remove the foreign key before you ");
                stringBuilder.Append("remove the column. Other foreign keys (if exists) that contain only the column to be removed are dropped silently.");

                throw new Exception(stringBuilder.ToString());
            }

            var allTableNames = GetTables();

            // Remove foreign keys with single parent column pointing to the column to be removed.
            foreach (var allTableName in allTableNames)
            {
                if (allTableName == tableName)
                {
                    continue;
                }

                var sqliteTableInfoOther = GetSQLiteTableInfo(allTableName);
                var recreateOtherTable = false;

                for (var i = sqliteTableInfoOther.ForeignKeys.Count - 1; i >= 0; i--)
                {
                    if (!sqliteTableInfoOther.ForeignKeys[i].ParentTable.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (sqliteTableInfoOther.ForeignKeys[i].ParentColumns.Contains(column) && sqliteTableInfoOther.ForeignKeys[i].ParentColumns.Length > 1)
                    {
                        StringBuilder stringBuilder = new();
                        stringBuilder.Append($"You need to delete/adjust the FK in table {allTableName} pointing to {tableName}.");
                        stringBuilder.Append("Other foreign key if exists with just one parent column we adjust silently.");

                        throw new Exception(stringBuilder.ToString());
                    }

                    if (sqliteTableInfoOther.ForeignKeys[i].ParentColumns.Contains(column) && sqliteTableInfoOther.ForeignKeys[i].ParentColumns.Length == 1)
                    {
                        recreateOtherTable = true;
                        sqliteTableInfoOther.ForeignKeys.RemoveAt(i);
                    }
                }

                if (recreateOtherTable)
                {
                    RecreateTable(sqliteTableInfoOther);
                }
            }

            sqliteInfoMainTable.Uniques.RemoveAll(x => x.KeyColumns.Length == 1 && x.KeyColumns[0].Equals(column, StringComparison.OrdinalIgnoreCase));
            sqliteInfoMainTable.ColumnMappings.RemoveAll(x => x.OldName.Equals(column, StringComparison.OrdinalIgnoreCase));
            sqliteInfoMainTable.Columns.RemoveAll(x => x.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
            sqliteInfoMainTable.Indexes.RemoveAll(x => x.KeyColumns.Length == 1 && x.KeyColumns[0].Equals(column, StringComparison.OrdinalIgnoreCase));
            sqliteInfoMainTable.ForeignKeys.RemoveAll(x => x.ChildColumns.Length == 1 && x.ChildColumns[0].Equals(column, StringComparison.OrdinalIgnoreCase));

            RecreateTable(sqliteInfoMainTable);
        }

        public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
        {
            var isPragmaForeignKeysOn = IsPragmaForeignKeysOn();

            if (isPragmaForeignKeysOn)
            {
                throw new Exception($"{nameof(RenameColumn)} requires foreign keys off.");
            }

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

                var columnMapping = sqliteTableInfo.ColumnMappings.First(x => x.OldName.Equals(oldColumnName, StringComparison.OrdinalIgnoreCase));
                columnMapping.NewName = newColumnName;

                var column = sqliteTableInfo.Columns.First(x => x.Name.Equals(oldColumnName, StringComparison.OrdinalIgnoreCase));
                column.Name = newColumnName;

                foreach (var foreignKey in sqliteTableInfo.ForeignKeys)
                {
                    foreignKey.ChildColumns = [.. foreignKey.ChildColumns.Select(x => x.Equals(oldColumnName, StringComparison.OrdinalIgnoreCase) ? newColumnName : x)];
                }

                foreach (var index in sqliteTableInfo.Indexes)
                {
                    index.KeyColumns = [.. index.KeyColumns.Select(x => x.Equals(oldColumnName, StringComparison.OrdinalIgnoreCase) ? newColumnName : x)];
                }

                foreach (var unique in sqliteTableInfo.Uniques)
                {
                    unique.KeyColumns = [.. unique.KeyColumns.Select(x => x.Equals(oldColumnName, StringComparison.OrdinalIgnoreCase) ? newColumnName : x)];
                }

                RecreateTable(sqliteTableInfo);

                var allTables = GetTables();

                // Rename in foreign keys of depending tables
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

                // Rename column in index
                foreach (var index in sqliteTableInfo.Indexes)
                {
                    index.KeyColumns = index.KeyColumns.Select(x => x == oldColumnName ? newColumnName : x).ToArray();
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
            var sqliteTableInfo = GetSQLiteTableInfo(table);
            var uniqueConstraint = new Unique() { KeyColumns = columns, Name = name };
            sqliteTableInfo.Uniques.Add(uniqueConstraint);

            RecreateTable(sqliteTableInfo);
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

        public bool IsPragmaForeignKeysOn()
        {
            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, "PRAGMA foreign_keys");
            reader.Read();
            var isOn = reader.GetInt32(0) == 1;

            return isOn;
        }

        public void SetPragmaForeignKeys(bool isOn)
        {
            var onOffString = isOn ? "ON" : "OFF";

            using var cmd = CreateCommand();
            ExecuteQuery(cmd, $"PRAGMA foreign_keys = {onOffString}");
        }

        private void RecreateTable(SQLiteTableInfo sqliteTableInfo)
        {
            var sourceTableQuoted = QuoteTableNameIfRequired(sqliteTableInfo.TableNameMapping.OldName);
            var targetIntermediateTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}{IntermediateTableSuffix}");
            var targetTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}");

            var columnDbFields = sqliteTableInfo.Columns.Cast<IDbField>();
            var foreignKeyDbFields = sqliteTableInfo.ForeignKeys.Cast<IDbField>();
            var indexDbFields = sqliteTableInfo.Indexes.Cast<IDbField>();
            var uniqueDbFields = sqliteTableInfo.Uniques.Cast<IDbField>();

            var dbFields = columnDbFields.Concat(foreignKeyDbFields)
                .Concat(uniqueDbFields)
                .ToArray();

            // ToHashSet() not available in older .NET versions so we create it old-fashioned.
            var uniqueColumnNames = new HashSet<string>(sqliteTableInfo.Uniques
                .SelectMany(x => x.KeyColumns)
                .Distinct()
             );

            // ToHashSet() not available in older .NET versions so we create it old-fashioned.
            var columnNames = new HashSet<string>(sqliteTableInfo.Columns
                .Select(x => x.Name)
            );

            // ToHashSet() not available in older .NET versions so we create it old-fashioned.
            var newColumnNamesInMapping = new HashSet<string>(sqliteTableInfo.ColumnMappings
                .Select(x => x.NewName)
            );

            if (!columnNames.SetEquals(newColumnNamesInMapping))
            {
                throw new Exception($"{nameof(columnNames)} and {nameof(newColumnNamesInMapping)} are not equal regarding length and content");
            }

            if (uniqueColumnNames.Except(columnNames).Any())
            {
                var firstMissing = uniqueColumnNames.Except(columnNames).First();
                throw new Exception($"Detected missing column names OR unique key columns that do not exist in the column list/column mapping. E.g. {firstMissing}");
            }

            AddTable(targetIntermediateTableQuoted, null, dbFields);

            var columnMappings = sqliteTableInfo.ColumnMappings
                .Where(x => x.OldName != null)
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

            foreach (var index in sqliteTableInfo.Indexes)
            {
                AddIndex(sqliteTableInfo.TableNameMapping.NewName, index);
            }
        }

        public override void AddColumn(string table, Column column)
        {
            if (!TableExists(table))
            {
                throw new Exception("Table does not exist.");
            }

            var sqliteInfo = GetSQLiteTableInfo(table);
            if (sqliteInfo.ColumnMappings.Select(x => x.OldName).ToList().Contains(column.Name))
            {
                throw new Exception("Column already exists.");
            }

            sqliteInfo.ColumnMappings.Add(new MappingInfo { OldName = null, NewName = column.Name });
            sqliteInfo.Columns.Add(column);

            RecreateTable(sqliteInfo);
        }

        public override void ChangeColumn(string table, Column column)
        {
            if (!TableExists(table))
            {
                throw new Exception("Table does not exist.");
            }

            var sqliteInfo = GetSQLiteTableInfo(table);

            if (!sqliteInfo.ColumnMappings.Select(x => x.OldName).ToList().Contains(column.Name))
            {
                throw new Exception("Column does not exists.");
            }

            sqliteInfo.Columns = sqliteInfo.Columns
                .Where(x => !x.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            sqliteInfo.Columns.Add(column);

            RecreateTable(sqliteInfo);
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
            throw new NotSupportedException("SQLite is a file-based database. You cannot list other databases.");
        }

        public override bool ConstraintExists(string table, string name)
        {
            throw new NotSupportedException("SQLite does not offer constraint names e.g. for unique, check constraints. You need to use alternative ways.");
        }

        public override string[] GetConstraints(string table)
        {
            throw new NotSupportedException("SQLite does not offer constraint names e.g. for unique, check constraints  You need to drop them using alternative ways.");
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

            // Column provides no way to store the primary key sequence number and we do not want to change the class for all database types for now
            // so we sort the columns.
            var tableInfoPrimaryKeys = pragmaTableInfoItems.Where(x => x.Pk > 0)
                .OrderBy(x => x.Pk)
                .ToList();

            var tableInfoNonPrimaryKeys = pragmaTableInfoItems.Where(x => x.Pk < 1)
                .OrderBy(x => x.Cid)
                .ToList();

            var pragmaTableInfoItemsSorted = tableInfoPrimaryKeys.Concat(tableInfoNonPrimaryKeys).ToList();


            var columns = new List<Column>();

            foreach (var pragmaTableInfoItem in pragmaTableInfoItemsSorted)
            {
                var column = new Column(pragmaTableInfoItem.Name)
                {
                    Type = _dialect.GetDbTypeFromString(pragmaTableInfoItem.Type)
                };

                if (pragmaTableInfoItem.NotNull)
                {
                    column.ColumnProperty |= ColumnProperty.NotNull;
                }
                else
                {
                    column.ColumnProperty |= ColumnProperty.Null;
                }

                var defValue = pragmaTableInfoItem.DfltValue == DBNull.Value ? null : pragmaTableInfoItem.DfltValue;

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

                if (pragmaTableInfoItem.Pk > 0)
                {
                    column.ColumnProperty |= ColumnProperty.PrimaryKey;
                }

                var indexListItems = GetPragmaIndexListItems(tableName);
                var uniqueConstraints = indexListItems.Where(x => x.Unique && x.Origin == "u");

                foreach (var uniqueConstraint in uniqueConstraints)
                {
                    var indexInfos = GetPragmaIndexInfo(uniqueConstraint.Name);

                    if (indexInfos.Count == 1 && indexInfos.First().Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        column.ColumnProperty |= ColumnProperty.Unique;

                        break;
                    }
                }

                columns.Add(column);
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
            List<Index> indexes = [];

            var pragmaIndexListItems = GetPragmaIndexListItems(table);

            // Since unique indexes are supported but only by using unique constraints or primary keys we filter them out here. See "GetUniques()" for unique constraints.
            var pragmaIndexListItemsFiltered = pragmaIndexListItems.Where(x => !x.Unique).ToList();

            foreach (var pragmaIndexListItemFiltered in pragmaIndexListItemsFiltered)
            {
                var indexInfos = GetPragmaIndexInfo(pragmaIndexListItemFiltered.Name);

                var columnNames = indexInfos.OrderBy(x => x.SeqNo)
                    .Select(x => x.Name)
                    .ToArray();

                var index = new Index
                {
                    // At this moment in time the migrator does not support clustered indexes for SQLITE
                    // Since SQLite 3.8.2 WITHOUT ROWID is supported but not in this migrator
                    Clustered = false,

                    // SQLite does not support include colums
                    IncludeColumns = [],
                    KeyColumns = columnNames,
                    Name = pragmaIndexListItemFiltered.Name,

                    // See GetUniques()
                    Unique = false,
                };

                indexes.Add(index);
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
                if (!string.IsNullOrEmpty(u.Name))
                {
                    stringBuilder.Append($" CONSTRAINT {u.Name}");
                }

                var uniqueColumnsCommaSeparated = string.Join(", ", u.KeyColumns);
                stringBuilder.Append($", UNIQUE ({uniqueColumnsCommaSeparated})");
            }

            var foreignKeys = fields.Where(x => x is ForeignKeyConstraint).Cast<ForeignKeyConstraint>().ToArray();

            List<string> foreignKeyStrings = [];

            foreach (var fk in foreignKeys)
            {
                var sourceColumnNamesQuotedString = string.Join(", ", fk.ChildColumns.Select(QuoteColumnNameIfRequired));
                var parentColumnNamesQuotedString = string.Join(", ", fk.ParentColumns.Select(QuoteColumnNameIfRequired));
                var parentTableNameQuoted = QuoteTableNameIfRequired(fk.ParentTable);

                if (string.IsNullOrWhiteSpace(fk.Name))
                {
                    throw new Exception("No foreign key constraint name given");
                }

                foreignKeyStrings.Add($"CONSTRAINT {fk.Name} FOREIGN KEY ({sourceColumnNamesQuotedString}) REFERENCES {parentTableNameQuoted}({parentColumnNamesQuotedString})");
            }

            if (foreignKeyStrings.Count != 0)
            {
                stringBuilder.Append(", ");
                stringBuilder.Append(string.Join(", ", foreignKeyStrings));
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

        public override void RemoveAllConstraints(string table)
        {
            RemovePrimaryKey(table);

            var sqliteTableInfo = GetSQLiteTableInfo(table);

            // Remove unique constraints
            sqliteTableInfo.Uniques = [];

            foreach (var column in sqliteTableInfo.Columns)
            {
                column.ColumnProperty &= ~ColumnProperty.PrimaryKey;
                column.ColumnProperty &= ~ColumnProperty.Unique;
            }

            // TODO CHECK is not implemented yet 
            // https://github.com/dotnetprojects/Migrator.NET/issues/64

            RecreateTable(sqliteTableInfo);
        }

        public override void RemovePrimaryKey(string tableName)
        {
            if (!TableExists(tableName))
            {
                return;
            }

            var sqliteInfoTable = GetSQLiteTableInfo(tableName);

            foreach (var column in sqliteInfoTable.Columns)
            {
                if (column.IsPrimaryKey)
                {
                    column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.PrimaryKey);
                    column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.PrimaryKeyWithIdentity);
                }
            }

            RecreateTable(sqliteInfoTable);
        }

        public override void RemoveAllIndexes(string tableName)
        {
            if (!TableExists(tableName))
            {
                return;
            }

            var sqliteInfoTable = GetSQLiteTableInfo(tableName);

            sqliteInfoTable.Uniques = [];
            sqliteInfoTable.Indexes = [];

            RecreateTable(sqliteInfoTable);
        }

        public List<Unique> GetUniques(string tableName)
        {
            List<Unique> uniques = [];

            var pragmaIndexListItems = GetPragmaIndexListItems(tableName);

            // Here we filter for origin u and unique while in "GetIndexes()" we exclude them.
            // If pk is set then it was added by using a primary key. If so this is handled by "GetColumns()".
            // If c is set it was created by using CREATE INDEX. At this moment in time this migrator does not support UNIQUE indexes but only normal indexes
            // so u should never be set 30.06.2025).
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
                        DfltValue = reader[reader.GetOrdinal("dflt_value")],
                        Name = reader.GetString(reader.GetOrdinal("name")),
                        NotNull = reader.GetInt32(reader.GetOrdinal("notnull")) == 1,
                        Pk = reader.GetInt32(reader.GetOrdinal("pk")),
                        Type = reader.GetString(reader.GetOrdinal("type")),
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

        [GeneratedRegex(@"CONSTRAINT\s+\w+\s+FOREIGN\s+KEY\s*\([^)]+\)\s+REFERENCES\s+\w+\s*\([^)]+\)")]
        private static partial Regex ForeignKeyRegex();
        [GeneratedRegex(@"\(([^)]+)\)")]
        private static partial Regex ForeignKeyParenthesisRegex();
    }
}
