using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite.Models;
using System;
using System.Collections.Generic;
using System.Data;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using Index = DotNetProjects.Migrator.Framework.Index;
using DotNetProjects.Migrator.Framework.Extensions;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using DotNetProjects.Migrator.Framework.Models;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

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
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new Exception("The foreign key name is mandatory");
        }

        var sqliteTableInfo = GetSQLiteTableInfo(childTable);

        // Get all unique constraint names if available
        var uniqueConstraintNames = sqliteTableInfo.Uniques.Select(x => x.Name).ToList();

        // Get all FK constraint names if available
        var foreignKeyNames = sqliteTableInfo.ForeignKeys.Select(x => x.Name).ToList();

        var names = uniqueConstraintNames.Concat(foreignKeyNames)
            .Distinct()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (names.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception($"Constraint name {name} already exists");
        }

        var foreignKey = new ForeignKeyConstraint
        {
            ChildColumns = childColumns,
            ChildTable = childTable,
            Name = name,
            ParentColumns = parentColumns,
            ParentTable = parentTable,
            OnDelete = new ForeignKeyConstraintMapper().SqlForConstraint(constraint),
        };

        sqliteTableInfo.ForeignKeys
            .Add(foreignKey);

        RecreateTable(sqliteTableInfo);
    }

    public override void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns,
        ForeignKeyConstraintType onDelete, ForeignKeyConstraintType onUpdate)
    {
        var info = GetSQLiteTableInfo(childTable) ?? throw new MigrationException("Child table does not exist.");
        if (string.IsNullOrWhiteSpace(name) || info.ForeignKeys.Select(f => f.Name).Concat(info.Uniques.Select(u => u.Name))
            .Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
            throw new MigrationException("A unique foreign key name is required.");
        info.ForeignKeys.Add(new ForeignKeyConstraint(name, parentTable, (string[])parentColumns.Clone(), childTable, (string[])childColumns.Clone())
        {
            OnDelete = new ForeignKeyConstraintMapper().SqlForConstraint(onDelete),
            OnUpdate = new ForeignKeyConstraintMapper().SqlForConstraint(onUpdate)
        });
        RecreateTable(info);
    }

    public string[] GetColumnDefs(string table, out string compositeDefSql)
    {
        return ParseSqlColumnDefs(GetSqlCreateTableScript(table), out compositeDefSql);
    }

    /// <summary>
    /// Gets the SQL CREATE TABLE script. Case-insensitive
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public string GetSqlCreateTableScript(string table)
    {
        string sqlCreateTableScript = null;

        using var cmd = CreateCommand();
        var parameter = cmd.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = table; cmd.Parameters.Add(parameter);
        using var reader = ExecuteQuery(cmd, "SELECT sql FROM sqlite_master WHERE type='table' AND name=@name COLLATE NOCASE");
        if (reader.Read()) sqlCreateTableScript = reader.IsDBNull(0) ? null : reader.GetString(0);

        return sqlCreateTableScript;
    }

    public override TableConstraint[] GetTableConstraints(string table)
    {
        var script = GetSqlCreateTableScript(table);
        if (string.IsNullOrWhiteSpace(script)) throw new MigrationException("Table does not exist: " + table);
        var constraints = SQLiteConstraintParser.Parse(script);
        foreach (var foreignKey in constraints.OfType<ForeignKeyConstraint>()) foreignKey.ChildTable = table;
        return constraints;
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

        if (foreignKeyConstraints.Count == 0)
        {
            return [];
        }

        var declared = GetTableConstraints(tableName).OfType<ForeignKeyConstraint>().ToList();
        foreach (var foreignKey in foreignKeyConstraints)
        {
            var definition = declared.FirstOrDefault(candidate =>
                candidate.ChildColumns.SequenceEqual(foreignKey.ChildColumns, StringComparer.OrdinalIgnoreCase) &&
                candidate.ParentTable.Equals(foreignKey.ParentTable, StringComparison.OrdinalIgnoreCase) &&
                (candidate.ParentColumns.Length == 0 || candidate.ParentColumns.SequenceEqual(foreignKey.ParentColumns, StringComparer.OrdinalIgnoreCase)));
            if (definition == null) throw new MigrationException("Cannot match a SQLite foreign key to its declaration.");
            foreignKey.Name = definition.Name;
            declared.Remove(definition);
        }

        return foreignKeyConstraints.ToArray();
    }

    public override void UpdateTargetFromSource(string tableSourceNotQuoted, string tableTargetNotQuoted, ColumnPair[] fromSourceToTargetColumnPairs, ColumnPair[] conditionColumnPairs)
    {
        if (!TableExists(tableSourceNotQuoted))
        {
            throw new Exception($"Table '{tableSourceNotQuoted}' given in '{nameof(tableSourceNotQuoted)}' does not exist");
        }

        if (!TableExists(tableTargetNotQuoted))
        {
            throw new Exception($"Table '{tableTargetNotQuoted}' given in '{nameof(tableTargetNotQuoted)}' does not exist");
        }

        if (fromSourceToTargetColumnPairs.Length == 0)
        {
            throw new Exception($"{nameof(fromSourceToTargetColumnPairs)} is empty.");
        }

        if (fromSourceToTargetColumnPairs.Any(x => string.IsNullOrWhiteSpace(x.ColumnNameSource) || string.IsNullOrWhiteSpace(x.ColumnNameTarget)))
        {
            throw new Exception($"One of the strings in {nameof(fromSourceToTargetColumnPairs)} is null or empty");
        }

        if (conditionColumnPairs.Length == 0)
        {
            throw new Exception($"{nameof(conditionColumnPairs)} is empty.");
        }

        if (conditionColumnPairs.Any(x => string.IsNullOrWhiteSpace(x.ColumnNameSource) || string.IsNullOrWhiteSpace(x.ColumnNameTarget)))
        {
            throw new Exception($"One of the strings in {nameof(conditionColumnPairs)} is null or empty");
        }

        var tableNameSource = QuoteTableNameIfRequired(tableSourceNotQuoted);
        var tableNameTarget = QuoteTableNameIfRequired(tableTargetNotQuoted);

        var assignStrings = fromSourceToTargetColumnPairs.Select(x => $"{QuoteColumnNameIfRequired(x.ColumnNameTarget)} = {tableNameSource}.{QuoteColumnNameIfRequired(x.ColumnNameSource)}").ToList();

        var conditionStrings = conditionColumnPairs.Select(x => $"{tableNameSource}.{QuoteColumnNameIfRequired(x.ColumnNameSource)} = {tableNameTarget}.{QuoteColumnNameIfRequired(x.ColumnNameTarget)}");

        var assignStringsJoined = string.Join(", ", assignStrings);
        var conditionStringsJoined = string.Join(" AND ", conditionStrings);

        var sql = $"UPDATE {tableNameTarget} SET {assignStringsJoined} FROM {tableNameSource} WHERE {conditionStringsJoined}";
        ExecuteNonQuery(sql);
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
        if (!TableExists(table))
        {
            throw new MigrationException($"Table '{table}' does not exist.");
        }

        var sqliteTableInfo = GetSQLiteTableInfo(table);
        if (!sqliteTableInfo.ForeignKeys.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new MigrationException($"Foreign key '{name}' does not exist.");
        }

        sqliteTableInfo.ForeignKeys.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        RecreateTable(sqliteTableInfo);
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
        if (Version.Parse(Convert.ToString(ExecuteScalar("SELECT sqlite_version()"))) >= new Version(3, 35, 0)
            && TableExists(tableName))
        {
            var info = GetSQLiteTableInfo(tableName);
            var definition = info.Columns.SingleOrDefault(c => c.Name.Equals(column, StringComparison.OrdinalIgnoreCase));
            bool Matches(string name) => string.Equals(name, column, StringComparison.OrdinalIgnoreCase);
            var dependent = definition == null || info.PrimaryKey?.KeyColumns.Contains(column, StringComparer.OrdinalIgnoreCase) == true || info.Uniques.Any(u => u.KeyColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
                || info.CheckConstraints.Count != 0
                || info.Uniques.Any(u => u.KeyColumns.Any(Matches))
                || info.Indexes.Any(i => i.KeyColumns.Any(Matches) || i.FilterItems.Count != 0)
                || info.ForeignKeys.Any(f => f.ChildColumns.Any(Matches))
                || GetTables().Any(t => GetForeignKeyConstraints(t).Any(f => f.ParentTable.Equals(tableName, StringComparison.OrdinalIgnoreCase) && f.ParentColumns.Any(Matches)));
            if (!dependent)
            {
                // SQLite itself validates trigger/view dependencies atomically. A rejection is
                // surfaced rather than retrying with a potentially lossy reconstruction.
                ExecuteNonQuery($"ALTER TABLE {Dialect.Quote(tableName)} DROP COLUMN {Dialect.Quote(definition.Name)}");
                return;
            }
        }
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
            throw new MigrationException($"The table '{tableName}' does not exist");
        }

        if (!ColumnExists(tableName, column))
        {
            throw new MigrationException($"The table '{tableName}' does not have a column named '{column}'");
        }

        var sqliteInfoMainTable = GetSQLiteTableInfo(tableName);

        if (sqliteInfoMainTable.PrimaryKey?.KeyColumns.Any(x => x.Equals(column, StringComparison.OrdinalIgnoreCase)) == true)
            throw new MigrationException("Remove the named primary-key constraint before removing one of its columns.");

        var checkConstraints = sqliteInfoMainTable.CheckConstraints;

        if (checkConstraints.Any(x => x.CheckConstraintString.Contains(column, StringComparison.OrdinalIgnoreCase)))
        {
            throw new MigrationException("A check constraint contains the column you want to remove. Remove the check constraint first");
        }

        if (!sqliteInfoMainTable.ColumnMappings.Any(x => x.OldName == column))
        {
            throw new MigrationException("Column not found");
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
        if (!TableExists(tableName))
        {
            throw new Exception($"Table {tableName} does not exist");
        }

        if (Version.Parse(Convert.ToString(ExecuteScalar("SELECT sqlite_version()"))) >= new Version(3, 26, 0))
        {
            if (string.IsNullOrWhiteSpace(newColumnName)) throw new ArgumentException("A column name is required.");
            ExecuteNonQuery($"ALTER TABLE {Dialect.Quote(tableName)} RENAME COLUMN {Dialect.Quote(oldColumnName)} TO {Dialect.Quote(newColumnName)}");
            return;
        }

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
            if (sqliteTableInfo.PrimaryKey != null)
                sqliteTableInfo.PrimaryKey.KeyColumns = sqliteTableInfo.PrimaryKey.KeyColumns
                    .Select(x => x.Equals(oldColumnName, StringComparison.OrdinalIgnoreCase) ? newColumnName : x).ToArray();

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

                    RecreateTable(sqliteTableInfoOther);
                }
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
        var info = GetSQLiteTableInfo(tableName) ?? throw new MigrationException("Table does not exist.");
        if (info.PrimaryKey != null) throw new MigrationException("The table already has a primary key. Remove it explicitly first.");
        ValidateKeyColumns(name, columnNames, info.Columns.ToArray());
        info.PrimaryKey = new PrimaryKeyConstraint(name, columnNames);
        RecreateTable(info);
    }

    public override bool PrimaryKeyExists(string table, string name)
    {
        var key = GetTableConstraints(table).OfType<PrimaryKeyConstraint>().SingleOrDefault();
        return key != null && string.Equals(key.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    public override void AddUniqueConstraint(string name, string table, params string[] columns)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MigrationException("Providing a constraint name is obligatory.");
        }

        var sqliteTableInfo = GetSQLiteTableInfo(table);

        if (sqliteTableInfo.Uniques.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new MigrationException("A unique constraint with the same name already exists.");
        }

        var uniqueConstraint = new UniqueConstraint() { KeyColumns = columns, Name = name };
        sqliteTableInfo.Uniques.Add(uniqueConstraint);

        RecreateTable(sqliteTableInfo);
    }

    public override void RemoveConstraint(string table, string name)
    {
        var sqliteTableInfo = GetSQLiteTableInfo(table);
        sqliteTableInfo.Uniques.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        sqliteTableInfo.CheckConstraints.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        RecreateTable(sqliteTableInfo);
    }

    public SQLiteTableInfo GetSQLiteTableInfo(string tableName)
    {
        if (!TableExists(tableName))
        {
            return null;
        }

        var sqliteTable = new SQLiteTableInfo
        {
            TableNameMapping = new MappingInfo { OldName = tableName, NewName = tableName },
            Columns = GetColumns(tableName).ToList(),
            PrimaryKey = GetTableConstraints(tableName).OfType<PrimaryKeyConstraint>().SingleOrDefault(),
            ForeignKeys = GetForeignKeyConstraints(tableName).ToList(),
            Indexes = GetIndexes(tableName).ToList(),
            Uniques = GetUniques(tableName).ToList(),
            CheckConstraints = GetCheckConstraints(tableName)
        };

        if (sqliteTable.PrimaryKey != null)
        {
            var columnOrder = GetPragmaTableInfoItems(tableName).ToDictionary(c => c.Name, c => c.Cid, StringComparer.OrdinalIgnoreCase);
            sqliteTable.Columns = sqliteTable.Columns.OrderBy(c => columnOrder[c.Name]).ToList();
        }

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
        ExecuteNonQuery($"PRAGMA foreign_keys = {onOffString}");
    }

    private static string ValidateForeignKeyAction(string action)
    {
        var normalized = action.ToUpperInvariant();
        if (normalized is not ("CASCADE" or "RESTRICT" or "SET NULL" or "SET DEFAULT" or "NO ACTION"))
            throw new MigrationException("Unsupported foreign key action: " + action);
        return normalized;
    }

    public void RecreateTable(SQLiteTableInfo sqliteTableInfo)
    {
        var oldName = sqliteTableInfo.TableNameMapping.OldName;
        var script = GetSqlCreateTableScript(oldName);
        if (Regex.IsMatch(script, @"\b(STRICT|GENERATED|DEFERRABLE|COLLATE)\b|WITHOUT\s+ROWID|CREATE\s+VIRTUAL|ON\s+CONFLICT", RegexOptions.IgnoreCase))
            throw new NotSupportedException("This table contains SQLite features that cannot be reconstructed faithfully. Use native SQL.");
        var triggers = ExecuteStringQuery("SELECT sql FROM sqlite_master WHERE type='trigger' AND lower(tbl_name)=lower('{0}')", oldName.Replace("'", "''"));
        if (triggers.Count > 0 && (oldName != sqliteTableInfo.TableNameMapping.NewName || sqliteTableInfo.ColumnMappings.Any(m => m.OldName != null && m.OldName != m.NewName)))
            throw new NotSupportedException("Use native SQLite rename when triggers reference renamed objects.");
        var originalColumns = GetColumns(oldName);
        if (triggers.Count > 0 && originalColumns.Any(c => !sqliteTableInfo.Columns.Any(n => n.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))))
            throw new NotSupportedException("Removing columns from a table with triggers requires native SQLite alteration or explicit trigger recreation.");
        var sequence = TableExists("sqlite_sequence")
            ? ExecuteScalar("SELECT seq FROM sqlite_sequence WHERE name='" + oldName.Replace("'", "''") + "'") : null;
        var highWater = sequence == null || sequence == DBNull.Value ? (long?)null : Convert.ToInt64(sequence);
        var foreignKeys = IsPragmaForeignKeysOn();
        if (HasActiveTransaction && foreignKeys)
            throw new MigrationException("SQLite rebuild requires foreign keys to be disabled before beginning the transaction. Use the migration runner.");
        var ownsTransaction = !HasActiveTransaction;
        Exception failure = null;
        try
        {
            if (ownsTransaction)
            {
                if (foreignKeys) SetPragmaForeignKeys(false);
                BeginTransaction();
            }
            RecreateTableCore(sqliteTableInfo);
            if (highWater.HasValue && sqliteTableInfo.Columns.Any(c => c.IsIdentity))
            {
                var sequenceName = sqliteTableInfo.TableNameMapping.NewName.Replace("'", "''");
                var sequenceValue = highWater.Value.ToString(CultureInfo.InvariantCulture);
                ExecuteNonQuery($"UPDATE sqlite_sequence SET seq=MAX(seq, {sequenceValue}) WHERE name='{sequenceName}'");
                ExecuteNonQuery($"INSERT INTO sqlite_sequence(name, seq) SELECT '{sequenceName}', {sequenceValue} WHERE NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name='{sequenceName}')");
            }
            foreach (var trigger in triggers) ExecuteNonQuery(trigger);
            if (ownsTransaction && !CheckForeignKeyIntegrity()) throw new MigrationException("SQLite rebuild would leave invalid foreign keys.");
            if (ownsTransaction) Commit();
        }
        catch (Exception ex)
        {
            failure = ex;
            if (ownsTransaction)
            {
                try { Rollback(); } catch (Exception rollback) { ex.Data["RollbackException"] = rollback; }
            }
            throw;
        }
        finally
        {
            try { if (ownsTransaction && foreignKeys) SetPragmaForeignKeys(true); }
            catch (Exception restore) { if (failure == null) throw; failure.Data["ConnectionRestoreException"] = restore; }
        }
    }

    private void RecreateTableCore(SQLiteTableInfo sqliteTableInfo)
    {
        var sourceTableQuoted = QuoteTableNameIfRequired(sqliteTableInfo.TableNameMapping.OldName);
        var targetIntermediateTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}{IntermediateTableSuffix}");
        var targetTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}");

        var columns = sqliteTableInfo.Columns.Select(c => c.CopyDefinition()).ToArray();
        var columnDbFields = columns.Cast<IDbField>();
        var foreignKeyDbFields = sqliteTableInfo.ForeignKeys.Cast<IDbField>();
        var indexDbFields = sqliteTableInfo.Indexes.Cast<IDbField>();
        var uniqueDbFields = sqliteTableInfo.Uniques.Cast<IDbField>();
        var checkConstraintDbFields = sqliteTableInfo.CheckConstraints.Cast<IDbField>();

        var dbFields = columnDbFields.Concat(foreignKeyDbFields)
            .Concat(uniqueDbFields)
            .Concat(checkConstraintDbFields)
            .Concat(sqliteTableInfo.PrimaryKey == null ? Array.Empty<IDbField>() : new IDbField[] { sqliteTableInfo.PrimaryKey })
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
            ExecuteNonQuery(sql);
        }

        RemoveTable(sourceTableQuoted);

        using (var cmd = CreateCommand())
        {
            // Rename to original name
            var sql = $"ALTER TABLE {targetIntermediateTableQuoted} RENAME TO {targetTableQuoted}";
            ExecuteNonQuery(sql);
        }

        foreach (var index in sqliteTableInfo.Indexes)
        {
            AddIndex(sqliteTableInfo.TableNameMapping.NewName, index);
        }
    }

    [Obsolete]
    public override void AddTable(string table, string engine, string columns)
    {
        throw new NotSupportedException();
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

    public override void AddColumn(string table, string columnName, DbType type, int size)
    {
        var column = new Column(columnName, type, size);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, MigratorDbType type, int size)
    {
        var column = new Column(columnName, type, size);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, DbType type)
    {
        var column = new Column(columnName, type);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, MigratorDbType type)
    {
        var column = new Column(columnName, type);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, DbType type, object defaultValue)
    {
        var column = new Column(columnName, type, defaultValue);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string sqlColumn)
    {
        var column = new Column(sqlColumn);
        AddColumn(table, column);
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

        var columnIndex = sqliteInfo.Columns.FindIndex(x => x.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase));
        sqliteInfo.Columns[columnIndex] = column.CopyDefinition();

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
        if (!TableExists(table))
        {
            throw new Exception($"Table '{table}' does not exist.");
        }

        var constraintNames = GetConstraints(table);

        var exists = constraintNames.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        return exists;
    }

    public override string[] GetConstraints(string table)
    {
        if (!TableExists(table))
        {
            throw new Exception($"Table '{table}' does not exist.");
        }

        var sqliteInfo = GetSQLiteTableInfo(table);

        var foreignKeyNames = sqliteInfo.ForeignKeys
            .Select(x => x.Name)
            .ToList();

        var uniqueConstraints = sqliteInfo.Uniques
            .Select(x => x.Name)
            .ToList();

        var checkConstraints = sqliteInfo.CheckConstraints
            .Select(x => x.Name)
            .ToList();

        var names = foreignKeyNames.Concat(uniqueConstraints)
            .Concat(checkConstraints)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var distinctNames = names.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length != distinctNames.Length)
        {
            throw new Exception($"There are duplicate constraint names in table {table}'");
        }

        return distinctNames;
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

        var tableInfoPrimaryKeys = pragmaTableInfoItems.Where(x => x.Pk > 0).ToList();
        var pragmaTableInfoItemsSorted = pragmaTableInfoItems.OrderBy(x => x.Cid).ToList();

        var columns = new List<Column>();

        foreach (var pragmaTableInfoItem in pragmaTableInfoItemsSorted)
        {
            var column = new Column(pragmaTableInfoItem.Name)
            {
                Type = _dialect.GetDbTypeFromString(pragmaTableInfoItem.Type)
            };

            if (pragmaTableInfoItem.NotNull)
            {
                column.IsNullable = false;
            }
            else
            {
                column.IsNullable = true;
            }

            var defValue = pragmaTableInfoItem.DfltValue == DBNull.Value ? null : pragmaTableInfoItem.DfltValue;

            column.DefaultValue = defValue is string sqlDefault
                ? CatalogDefaultValue.Parse(sqlDefault, column.Type) : defValue;

            var tableScript = GetSqlCreateTableScript(tableName);

            var columnTableInfoItem = pragmaTableInfoItems.First(x => x.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase));

            var hasCompoundPrimaryKey = tableInfoPrimaryKeys.Count > 1;

            // Implicit in SQLite
            if (columnTableInfoItem.Type == "INTEGER" && columnTableInfoItem.Pk == 1 && !hasCompoundPrimaryKey && Regex.IsMatch(tableScript, @"\bAUTOINCREMENT\b", RegexOptions.IgnoreCase))
            {
                column.IsIdentity = true;
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
        var afterWhereRegex = new Regex("(?<= WHERE ).+");
        List<Index> indexes = [];

        var indexCreateScripts = GetCreateIndexSqlStrings(table);

        var pragmaIndexListItems = GetPragmaIndexListItems(table).Where(x => x.Origin == "c");

        var columns = GetColumns(table);

        foreach (var pragmaIndexListItem in pragmaIndexListItems)
        {
            var indexInfos = GetPragmaIndexInfo(pragmaIndexListItem.Name);

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
                Name = pragmaIndexListItem.Name,
                Unique = pragmaIndexListItem.Unique
            };

            var script = indexCreateScripts.FirstOrDefault(x => x.Contains(pragmaIndexListItem.Name, StringComparison.OrdinalIgnoreCase));

            if (script != null)
            {
                if (afterWhereRegex.Match(script) is Match match && match.Success)
                {
                    // We cannot use GeneratedRegexAttribute due to old .NET version
                    var andSplitted = Regex.Split(match.Value, " AND ");

                    var filterSingleStrings = andSplitted
                        .Select(x => x.Trim())
                        .ToList();

                    foreach (var filterSingleString in filterSingleStrings)
                    {
                        var splitted = filterSingleString.Split(' ')
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x.Trim())
                            .ToList();

                        var filterItem = new FilterItem { ColumnName = splitted[0], Filter = _dialect.GetFilterTypeByComparisonString(splitted[1]) };

                        var column = columns.Single(x => x.Name.Equals(splitted[0], StringComparison.OrdinalIgnoreCase));

                        var sqliteIntegerDataTypes = new[] {
                            MigratorDbType.Int16,
                            MigratorDbType.Int32,
                            MigratorDbType.Int64,
                            MigratorDbType.UInt16,
                            MigratorDbType.UInt32,
                            MigratorDbType.UInt64
                        };

                        if (sqliteIntegerDataTypes.Contains(column.MigratorDbType))
                        {
                            if (long.TryParse(splitted[2], out var longValue))
                            {
                                filterItem.Value = longValue;
                            }
                            else if (ulong.TryParse(splitted[2], out var uLongValue))
                            {
                                filterItem.Value = uLongValue;
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                        else
                        {
                            filterItem.Value = column.MigratorDbType switch
                            {
                                MigratorDbType.Boolean => splitted[2] == "1" || splitted[2].Equals("true", StringComparison.OrdinalIgnoreCase),
                                MigratorDbType.String => splitted[2].Substring(1, splitted[2].Length - 2),
                                _ => throw new NotImplementedException("Type not yet supported. Please file an issue."),
                            };
                        }

                        index.FilterItems.Add(filterItem);
                    }
                }
            }

            indexes.Add(index);
        }

        return [.. indexes];
    }

    public override void AddTable(string name, string engine, params IDbField[] fields)
    {
        if (engine != null) throw new NotSupportedException("SQLite does not support table engines.");
        var table = _dialect.TableNameNeedsQuote ? _dialect.Quote(name) : QuoteTableNameIfRequired(name);
        ExecuteNonQuery(SQLiteTableSql.Generate(_dialect, table, fields));
        foreach (var index in fields.OfType<Index>()) AddIndex(name, index);
    }

    public override string AddIndex(string table, Index index)
    {
        ValidateIndex(table, index);

        var hasIncludedColumns = index.IncludeColumns != null && index.IncludeColumns.Length > 0;

        if (hasIncludedColumns)
        {
            // This will be actived in the future.
            // throw new MigrationException($"SQLite does not support included columns. Use 'if(Provider is {nameof(SQLiteTransformationProvider)}' if necessary.");
        }

        if (index.Clustered)
        {
            throw new MigrationException($"For SQLite this migrator does not support clustered indexes at this point in time, sorry. File an issue if needed. Use 'if(Provider is {nameof(SQLiteTransformationProvider)}' if necessary.");
        }

        var name = QuoteConstraintNameIfRequired(index.Name);
        table = QuoteTableNameIfRequired(table);
        var columns = QuoteColumnNamesIfRequired(index.KeyColumns);

        var uniqueString = index.Unique ? "UNIQUE" : null;
        var columnsString = $"({string.Join(", ", columns)})";
        var filterString = string.Empty;

        if (index.FilterItems != null && index.FilterItems.Count > 0)
        {
            List<string> singleFilterStrings = [];

            foreach (var filterItem in index.FilterItems)
            {
                var comparisonString = _dialect.GetComparisonStringByFilterType(filterItem.Filter);

                var filterColumnQuoted = QuoteColumnNameIfRequired(filterItem.ColumnName);
                string value = null;

                value = filterItem.Value switch
                {
                    bool booleanValue => booleanValue ? "1" : "0",
                    string stringValue => $"'{stringValue.Replace("'", "''")}'",
                    byte or short or int or long => Convert.ToInt64(filterItem.Value).ToString(),
                    sbyte or ushort or uint or ulong => Convert.ToUInt64(filterItem.Value).ToString(),
                    _ => throw new NotImplementedException("Given type is not implemented. Please file an issue."),
                };

                if ((filterItem.Value is string || filterItem.Value is bool) && filterItem.Filter != FilterType.EqualTo && filterItem.Filter != FilterType.NotEqualTo)
                {
                    throw new MigrationException($"Bool and string in {nameof(FilterItem)} can only be used with '{nameof(FilterType.EqualTo)}' or '{nameof(FilterType.EqualTo)}'.");
                }

                var singleFilterString = $"{filterColumnQuoted} {comparisonString} {value}";

                singleFilterStrings.Add(singleFilterString);
            }

            filterString = $"WHERE {string.Join(" AND ", singleFilterStrings)}";
        }

        List<string> list = ["CREATE", uniqueString, "INDEX", name, "ON", table, columnsString, filterString];

        var sql = string.Join(" ", list.Where(x => !string.IsNullOrWhiteSpace(x)));

        ExecuteNonQuery(sql);

        return sql;
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        return GetTableConstraints(table).OfType<PrimaryKeyConstraint>().SingleOrDefault()?.Name;
    }

    public override void RemoveAllConstraints(string table)
    {
        var info = GetSQLiteTableInfo(table);
        info.PrimaryKey = null;
        info.Uniques.Clear();
        info.ForeignKeys.Clear();
        info.CheckConstraints.Clear();
        foreach (var column in info.Columns) column.IsIdentity = false;
        RecreateTable(info);
    }

    public override void RemovePrimaryKey(string tableName)
    {
        if (!TableExists(tableName)) return;
        var info = GetSQLiteTableInfo(tableName);
        info.PrimaryKey = null;
        foreach (var column in info.Columns) column.IsIdentity = false;
        RecreateTable(info);
    }

    public override void RemoveAllIndexes(string tableName)
    {
        if (!TableExists(tableName))
        {
            return;
        }

        var sqliteInfoTable = GetSQLiteTableInfo(tableName);

        sqliteInfoTable.Indexes = [];

        RecreateTable(sqliteInfoTable);
    }

    public List<UniqueConstraint> GetUniques(string tableName) => GetTableConstraints(tableName)
        .OfType<UniqueConstraint>().ToList();

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

    public override void AddCheckConstraint(string constraintName, string tableName, string checkSql)
    {
        var sqliteTableInfo = GetSQLiteTableInfo(tableName);

        var checkConstraint = new CheckConstraint(constraintName, checkSql);
        sqliteTableInfo.CheckConstraints.Add(checkConstraint);

        RecreateTable(sqliteTableInfo);
    }

    public override void CopyDataFromTableToTable(string sourceTableName, List<string> sourceColumnNames, string targetTableName, List<string> targetColumnNames, List<string> orderBySourceColumns = null)
    {
        orderBySourceColumns ??= [];

        if (!TableExists(sourceTableName))
        {
            throw new Exception($"Source table '{QuoteTableNameIfRequired(sourceTableName)}' does not exist");
        }

        if (!TableExists(targetTableName))
        {
            throw new Exception($"Target table '{QuoteTableNameIfRequired(targetTableName)}' does not exist");
        }

        var sourceColumnsConcatenated = sourceColumnNames.Concat(orderBySourceColumns);

        foreach (var column in sourceColumnsConcatenated)
        {
            if (!ColumnExists(sourceTableName, column))
            {
                throw new Exception($"Column {column} in source table does not exist.");
            }
        }

        foreach (var column in targetColumnNames)
        {
            if (!ColumnExists(targetTableName, column))
            {
                throw new Exception($"Column {column} in target table does not exist.");
            }
        }

        if (!orderBySourceColumns.All(x => sourceColumnNames.Contains(x)))
        {
            throw new Exception($"All columns in {nameof(orderBySourceColumns)} must be in {nameof(sourceColumnNames)}");
        }

        var sourceTableNameQuoted = QuoteTableNameIfRequired(sourceTableName);
        var targetTableNameQuoted = QuoteTableNameIfRequired(targetTableName);

        var sourceColumnNamesQuoted = sourceColumnNames.Select(QuoteColumnNameIfRequired).ToList();
        var targetColumnNamesQuoted = targetColumnNames.Select(QuoteColumnNameIfRequired).ToList();
        var orderBySourceColumnsQuoted = orderBySourceColumns.Select(QuoteColumnNameIfRequired).ToList();

        var sourceColumnsJoined = string.Join(", ", sourceColumnNamesQuoted);
        var targetColumnsJoined = string.Join(", ", targetColumnNamesQuoted);
        var orderBySourceColumnsJoined = string.Join(", ", orderBySourceColumnsQuoted);

        var orderByComponent = !string.IsNullOrWhiteSpace(orderBySourceColumnsJoined) ? $"ORDER BY {orderBySourceColumnsJoined}" : null;

        List<string> sqlComponents =
        [
            $"INSERT INTO {targetTableNameQuoted} ({targetColumnsJoined}) SELECT {sourceColumnsJoined} FROM {sourceTableNameQuoted}",
            orderByComponent
        ];

        var sql = string.Join(" ", sqlComponents.Where(x => x != null));
        ExecuteNonQuery(sql);
    }

    public List<CheckConstraint> GetCheckConstraints(string tableName) => GetTableConstraints(tableName).OfType<CheckConstraint>().ToList();

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

