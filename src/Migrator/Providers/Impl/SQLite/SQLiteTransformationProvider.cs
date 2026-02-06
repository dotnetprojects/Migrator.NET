using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite.Models;
using System;
using System.Collections.Generic;
using System.Data;
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
        };

        sqliteTableInfo.ForeignKeys
            .Add(foreignKey);

        RecreateTable(sqliteTableInfo);
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

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format("SELECT sql FROM sqlite_master WHERE type='table' AND lower(name)=lower('{0}')", table)))
        {
            if (reader.Read())
            {
                sqlCreateTableScript = reader.IsDBNull(0) ? null : (string)reader[0];
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

        if (foreignKeyConstraints.Count == 0)
        {
            return [];
        }

        var createTableScript = GetSqlCreateTableScript(tableName);
        // GeneratedRegex
        var regEx = new Regex(@"CONSTRAINT\s+\w+\s+FOREIGN\s+KEY\s*\([^)]+\)\s+REFERENCES\s+[\w""]+\s*\([^)]+\)");
        var matchesCollection = regEx.Matches(createTableScript);
        var fkParts = matchesCollection.Cast<Match>().ToList().Where(x => x.Success).Select(x => x.Value).ToList();

        if (fkParts.Count != foreignKeyConstraints.Count)
        {
            throw new Exception($"Cannot extract all foreign keys out of the create table script in SQLite. Did you use a name as foreign key constraint for all constraints in table '{tableName}' in this or older migrations?");
        }

        List<ForeignKeyExtract> foreignKeyExtracts = [];

        foreach (var fkPart in fkParts)
        {
            var regexParenthesis = new Regex(@"\(([^)]+)\)");
            var parenthesisContents = regexParenthesis.Matches(fkPart).Cast<Match>().Select(x => x.Groups[1].Value).ToList();

            if (parenthesisContents.Count != 2)
            {
                throw new Exception("Cannot extract parenthesis of foreign key constraint");
            }

            var foreignKeyExtract = new ForeignKeyExtract()
            {
                ChildColumnNames = parenthesisContents[0].Split(',').Select(x => x.Trim()).ToList(),
                ForeignKeyString = fkPart,
                ParentColumnNames = parenthesisContents[1].Split(',').Select(x => x.Trim()).ToList(),
            };

            var foreignKeyConstraintNameRegex = new Regex(@"CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY");
            var foreignKeyNameMatch = foreignKeyConstraintNameRegex.Match(fkPart);

            if (!foreignKeyNameMatch.Success)
            {
                throw new Exception("Could not extract the foreign key constraint name");
            }

            foreignKeyExtract.ForeignKeyName = foreignKeyNameMatch.Groups[1].Value;

            foreignKeyExtracts.Add(foreignKeyExtract);
        }

        foreach (var foreignKeyConstraint in foreignKeyConstraints)
        {
            foreach (var foreignKeyExtract in foreignKeyExtracts)
            {
                if (
                    foreignKeyExtract.ChildColumnNames.SequenceEqual(foreignKeyConstraint.ChildColumns) &&
                    foreignKeyExtract.ParentColumnNames.SequenceEqual(foreignKeyConstraint.ParentColumns)
                )
                {
                    foreignKeyConstraint.Name = foreignKeyExtract.ForeignKeyName;
                }
            }
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
        if (!sqliteTableInfo.ForeignKeys.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new MigrationException($"Foreign key '{name}' does not exist.");
        }

        sqliteTableInfo.ForeignKeys.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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
        // In SQLite we need to recreate the table even if we only want to add, alter or drop a foreign key. So we not only recreate the table given 
        // as parameter but also the tables with FKs pointing to the column you want to remove.
        // In order to perform it smoothly, the PRAGMA foreign keys should be set off.
        //
        // Note: SQLite 3.35.0+ added native ALTER TABLE ... DROP COLUMN support, but it has limitations:
        // - Cannot drop PRIMARY KEY columns
        // - Cannot drop UNIQUE columns
        // - Cannot drop columns that are part of a foreign key or index  
        // Therefore, we continue using the RecreateTable approach for maximum compatibility and features.

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

    // SQLite 3.25.0+ supports ALTER TABLE ... RENAME COLUMN natively
    // Use the base implementation which generates the correct SQL
    // public override void RenameColumn removed to use base class implementation

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
            if (columnNames.Any(x => x.Equals(column.Name, StringComparison.OrdinalIgnoreCase)))
            {
                column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.PrimaryKey);
            }
            else
            {
                column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.PrimaryKey);
            }
        }

        var columnNamesList = columnNames.ToList();

        var columnsReordered = sqliteTableInfo.Columns.OrderBy(x =>
        {
            var index = columnNamesList.IndexOf(x.Name);
            return index >= 0 ? index : int.MaxValue;
        }).ToList();

        sqliteTableInfo.Columns = columnsReordered;

        RecreateTable(sqliteTableInfo);
    }

    public override bool PrimaryKeyExists(string table, string name)
    {
        var sqliteTableInfo = GetSQLiteTableInfo(table);

        // SQLite does not offer named primary keys BUT since there can only be one primary key per table we return true if there is any primary key.

        var hasPrimaryKey = sqliteTableInfo.Columns.Any(x => x.ColumnProperty.IsSet(ColumnProperty.PrimaryKey));

        return hasPrimaryKey;
    }

    public override void AddUniqueConstraint(string name, string table, params string[] columns)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MigrationException("Providing a constraint name is obligatory.");
        }

        var sqliteTableInfo = GetSQLiteTableInfo(table);

        if (sqliteTableInfo.Uniques.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new MigrationException("A unique constraint with the same name already exists.");
        }

        var uniqueConstraint = new Unique() { KeyColumns = columns, Name = name };
        sqliteTableInfo.Uniques.Add(uniqueConstraint);

        RecreateTable(sqliteTableInfo);
    }

    public override void RemoveConstraint(string table, string name)
    {
        var sqliteTableInfo = GetSQLiteTableInfo(table);
        sqliteTableInfo.Uniques.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        sqliteTableInfo.CheckConstraints.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

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
            ForeignKeys = GetForeignKeyConstraints(tableName).ToList(),
            Indexes = GetIndexes(tableName).ToList(),
            Uniques = GetUniques(tableName).ToList(),
            CheckConstraints = GetCheckConstraints(tableName)
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

    public void RecreateTable(SQLiteTableInfo sqliteTableInfo)
    {
        var sourceTableQuoted = QuoteTableNameIfRequired(sqliteTableInfo.TableNameMapping.OldName);
        var targetIntermediateTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}{IntermediateTableSuffix}");
        var targetTableQuoted = QuoteTableNameIfRequired($"{sqliteTableInfo.TableNameMapping.NewName}");

        var columnDbFields = sqliteTableInfo.Columns.Cast<IDbField>();
        var foreignKeyDbFields = sqliteTableInfo.ForeignKeys.Cast<IDbField>();
        var indexDbFields = sqliteTableInfo.Indexes.Cast<IDbField>();
        var uniqueDbFields = sqliteTableInfo.Uniques.Cast<IDbField>();
        var checkConstraintDbFields = sqliteTableInfo.CheckConstraints.Cast<IDbField>();

        var dbFields = columnDbFields.Concat(foreignKeyDbFields)
            .Concat(uniqueDbFields)
            .Concat(checkConstraintDbFields)
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
            // Rename to original name
            var sql = $"ALTER TABLE {targetIntermediateTableQuoted} RENAME TO {targetTableQuoted}";
            ExecuteQuery(cmd, sql);
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

    public override void AddColumn(string table, string columnName, DbType type, ColumnProperty property)
    {
        var column = new Column(columnName, type, property);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, MigratorDbType type, ColumnProperty property)
    {
        var column = new Column(columnName, type, property);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, MigratorDbType type, int size, ColumnProperty property,
                                  object defaultValue)
    {
        var column = new Column(columnName, type, property) { Size = size, DefaultValue = defaultValue };

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

    public override void AddColumn(string table, string columnName, DbType type, int size, ColumnProperty property)
    {
        var column = new Column(columnName, type, size, property);

        AddColumn(table, column);
    }

    public override void AddColumn(string table, string columnName, MigratorDbType type, int size, ColumnProperty property)
    {
        var column = new Column(columnName, type, size, property);

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

                        var d = DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
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
                else if (column.Type == DbType.Boolean)
                {
                    throw new NotSupportedException("SQLite does not support default values for BLOB columns.");
                }
            }

            if (pragmaTableInfoItem.Pk > 0)
            {
                if (new[] { DbType.UInt16, DbType.UInt32, DbType.UInt64, DbType.Int16, DbType.Int32, DbType.Int64 }.Contains(column.Type))
                {
                    column.ColumnProperty |= ColumnProperty.PrimaryKey;
                    column.ColumnProperty |= ColumnProperty.NotNull;
                    column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Null);
                }
                else
                {
                    column.ColumnProperty |= ColumnProperty.PrimaryKey;
                }
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

            var tableScript = GetSqlCreateTableScript(tableName);

            var columnTableInfoItem = pragmaTableInfoItems.First(x => x.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase));

            var hasCompoundPrimaryKey = tableInfoPrimaryKeys.Count > 1;

            // Implicit in SQLite
            if (columnTableInfoItem.Type == "INTEGER" && columnTableInfoItem.Pk == 1 && !hasCompoundPrimaryKey)
            {
                column.ColumnProperty |= ColumnProperty.Identity;
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
        var columns = fields.Where(x => x is Column)
            .Cast<Column>()
            .ToArray();

        var pks = GetPrimaryKeys(columns);
        var hasCompoundPrimaryKey = pks.Count > 1;

        var columnProviders = new List<ColumnPropertiesMapper>(columns.Length);

        foreach (var column in columns)
        {
            if (!hasCompoundPrimaryKey && column.IsPrimaryKey)
            {
                // We implicitly set NOT NULL for non-composite primary keys like in other RDBMS.
                column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Null);
                column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.NotNull);
            }

            if (hasCompoundPrimaryKey && column.IsPrimaryKey)
            {
                // We remove PrimaryKey here and readd it as compound later ("...PRIMARY KEY(column1,column2)");
                column.ColumnProperty &= ~ColumnProperty.PrimaryKey;

                // AUTOINCREMENT cannot be used in compound primary keys in SQLite so we remove Identity here
                column.ColumnProperty &= ~ColumnProperty.Identity;
            }

            var mapper = _dialect.GetAndMapColumnProperties(column);
            columnProviders.Add(mapper);
        }

        var columnsAndIndexes = JoinColumnsAndIndexes(columnProviders);

        var table = _dialect.TableNameNeedsQuote ? _dialect.Quote(name) : QuoteTableNameIfRequired(name);
        StringBuilder stringBuilder = new();

        stringBuilder.Append(string.Format("CREATE TABLE {0} ({1}", table, columnsAndIndexes));

        if (hasCompoundPrimaryKey)
        {
            stringBuilder.Append(string.Format(", PRIMARY KEY ({0})", string.Join(", ", pks.ToArray())));
        }


        // Uniques
        var uniques = fields.Where(x => x is Unique).Cast<Unique>().ToArray();

        foreach (var u in uniques)
        {
            if (!string.IsNullOrEmpty(u.Name))
            {
                stringBuilder.Append($", CONSTRAINT {u.Name}");
            }
            else
            {
                stringBuilder.Append(", ");
            }

            var uniqueColumnsCommaSeparated = string.Join(", ", u.KeyColumns);
            stringBuilder.Append($" UNIQUE ({uniqueColumnsCommaSeparated})");
        }

        // Foreign keys
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

        if (foreignKeyStrings.Count > 0)
        {
            stringBuilder.Append(", ");
            stringBuilder.Append(string.Join(", ", foreignKeyStrings));
        }

        // Check Constraints
        var checkConstraints = fields.Where(x => x is CheckConstraint).OfType<CheckConstraint>().ToArray();
        List<string> checkConstraintStrings = [];

        foreach (var checkConstraint in checkConstraints)
        {
            checkConstraintStrings.Add($"CONSTRAINT {checkConstraint.Name} CHECK ({checkConstraint.CheckConstraintString})");
        }

        if (checkConstraintStrings.Count > 0)
        {
            stringBuilder.Append($", {string.Join(", ", checkConstraintStrings)}");
        }

        stringBuilder.Append(')');

        ExecuteNonQuery(stringBuilder.ToString());

        var indexes = fields.Where(x => x is Index)
            .Cast<Index>()
            .ToArray();

        foreach (var index in indexes)
        {
            AddIndex(name, index);
        }
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
                    string stringValue => $"'{stringValue}'",
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
        if (!TableExists(tableName))
        {
            throw new Exception($"Table '{tableName}' does not exist.");
        }

        var regEx = new Regex(@"(?<=,)\s*(CONSTRAINT\s+\w+\s+)?UNIQUE\s*\(\s*[\w\s,]+\s*\)\s*(?=,|\s*\))");
        var regExConstraintName = new Regex(@"(?<=CONSTRAINT\s+)\w+(?=\s+)");
        var regExParenthesis = new Regex(@"(?<=\().+(?=\))");

        List<Unique> uniques = [];

        var pragmaIndexListItems = GetPragmaIndexListItems(tableName);

        // Here we filter for origin u and unique while in "GetIndexes()" we exclude them.
        // If "pk" is set then it was added by using a primary key. If so this is handled by "GetColumns()".
        // If "c" is set it was created by using CREATE INDEX.
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

        var createScript = GetSqlCreateTableScript(tableName);

        var matches = regEx.Matches(createScript);
        if (matches.Count == 0)
        {
            return [];
        }

        var constraintNames = matches
            .OfType<Match>()
            .Where(x => x.Success && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Value.Trim())
            .ToList();

        // We can only use the ones containing a  starting with CONSTRAINT 
        var matchesHavingName = constraintNames.Where(x => x.StartsWith("CONSTRAINT")).ToList();

        foreach (var constraintString in matchesHavingName)
        {
            var constraintNameMatch = regExConstraintName.Match(constraintString);

            if (!constraintNameMatch.Success)
            {
                throw new Exception("Cannot extract constraint name. Please file an issue");
            }

            var constraintName = constraintNameMatch.Value;

            var parenthesisMatch = regExParenthesis.Match(constraintString);

            if (!parenthesisMatch.Success)
            {
                throw new Exception("Cannot extract parenthesis content for UNIQUE constraint. Please file an issue");
            }

            var columns = parenthesisMatch.Value.Split(',').Select(x => x.Trim()).ToList();

            var unique = uniques.Where(x => x.KeyColumns.SequenceEqual(columns)).SingleOrDefault();

            if (unique != null)
            {
                unique.Name = constraintName;
            }
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

    public List<CheckConstraint> GetCheckConstraints(string tableName)
    {
        if (!TableExists(tableName))
        {
            throw new Exception($"Table '{tableName}' does not exist.");
        }

        var checkConstraintRegex = new Regex(@"(?<=,)[^,]+\s+[^,]+check[^,]+(?=[,|\)])", RegexOptions.IgnoreCase);
        var braceContentRegex = new Regex(@"(?<=^\().+(?=\)$)");

        var script = GetSqlCreateTableScript(tableName);

        var matches = checkConstraintRegex.Matches(script);

        if (matches == null)
        {
            return [];
        }

        var checkStrings = matches.OfType<Match>()
            .Where(x => x.Success)
            .Select(x => x.Value)
            .ToList();

        List<CheckConstraint> checkConstraints = [];

        foreach (var checkString in checkStrings)
        {
            var splitted = checkString.Trim().Split(' ')
                .Select(x => x.Trim())
                .ToList();

            if (!splitted[0].Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase) || !splitted[2].Equals("CHECK", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Cannot parse check constraint in table {tableName}");
            }

            var checkConstraintStringWithBraces = string.Join(" ", splitted.Skip(3)).Trim();
            var checkConstraintString = braceContentRegex.Match(checkConstraintStringWithBraces);

            var checkConstraint = new CheckConstraint
            {
                Name = splitted[1],
                CheckConstraintString = checkConstraintString.Value
            };

            checkConstraints.Add(checkConstraint);
        }

        return checkConstraints;
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
