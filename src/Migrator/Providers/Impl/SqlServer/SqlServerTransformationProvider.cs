#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using DotNetProjects.Migrator.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.SqlServer;

/// <summary>
/// Migration transformations provider for Microsoft SQL Server.
/// </summary>
public class SqlServerTransformationProvider : TransformationProvider
{
    public SqlServerTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        CreateConnection(providerName);
    }

    public SqlServerTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope)
    {
    }


    protected virtual void CreateConnection(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "System.Data.SqlClient";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();

        string collationString = null;
        var collation = ExecuteScalar("SELECT DATABASEPROPERTYEX('" + _connection.Database + "', 'Collation')");

        if (collation != null)
        {
            collationString = collation.ToString();
        }

        if (string.IsNullOrWhiteSpace(collationString))
        {
            collationString = "Latin1_General_CI_AS";
        }

        Dialect.RegisterProperty(ColumnProperty.CaseSensitive, "COLLATE " + collationString.Replace("_CI_", "_CS_"));
    }

    public override bool TableExists(string tableName)
    {
        // This is not clean! Usually you should use schema as well as this query will find tables in other tables as well!

        using var cmd = CreateCommand();
        using var reader = ExecuteQuery(cmd, $"SELECT OBJECT_ID('{tableName}', 'U')");

        if (reader.Read())
        {
            var result = reader.GetValue(0);
            var tableExists = result != DBNull.Value && result != null;

            return tableExists;
        }

        return false;
    }

    public override bool ViewExists(string viewName)
    {
        // This is not clean! Usually you should use schema as well as this query will find views in other tables as well!

        using var cmd = CreateCommand();
        cmd.CommandText = $"SELECT OBJECT_ID(@FullViewName, 'V')";

        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "@FullViewName";
        parameter.Value = viewName;
        cmd.Parameters.Add(parameter);

        var result = cmd.ExecuteScalar();

        var viewExists = result != DBNull.Value && result != null;

        return viewExists;
    }

    public override bool ConstraintExists(string table, string name)
    {
        var retVal = false;
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format("SELECT TOP 1 * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME ='{0}'", name)))
        {
            retVal = reader.Read();
        }

        if (!retVal)
        {
            using var cmd = CreateCommand();
            using var reader = ExecuteQuery(cmd, string.Format("SELECT TOP 1 * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('{0}') AND name = '{1}'", table, name));
            return reader.Read();
        }

        return true;
    }

    public override void AddColumn(string table, string sqlColumn)
    {
        table = _dialect.TableNameNeedsQuote ? _dialect.Quote(table) : table;
        ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
    }

    public override void AddPrimaryKeyNonClustered(string name, string table, params string[] columns)
    {
        var nonclusteredString = "NONCLUSTERED";
        ExecuteNonQuery(
        string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} PRIMARY KEY {2} ({3}) ", table, name, nonclusteredString,
                      string.Join(",", QuoteColumnNamesIfRequired(columns))));
    }

    public override void AddIndex(string table, Index index)
    {
        var name = QuoteConstraintNameIfRequired(index.Name);

        table = QuoteTableNameIfRequired(table);

        var columns = QuoteColumnNamesIfRequired(index.KeyColumns);

        if (index.IncludeColumns != null && index.IncludeColumns.Length > 0)
        {
            var include = QuoteColumnNamesIfRequired(index.IncludeColumns);
            ExecuteNonQuery(string.Format("CREATE {0}{1} INDEX {2} ON {3} ({4}) INCLUDE ({5})", (index.Unique ? "UNIQUE " : ""), (index.Clustered ? "CLUSTERED" : "NONCLUSTERED"), name, table, string.Join(", ", columns), string.Join(", ", include)));
        }
        else
        {
            ExecuteNonQuery(string.Format("CREATE {0}{1} INDEX {2} ON {3} ({4})", (index.Unique ? "UNIQUE " : ""), (index.Clustered ? "CLUSTERED" : "NONCLUSTERED"), name, table, string.Join(", ", columns)));
        }
    }

    public override void ChangeColumn(string table, Column column)
    {
        if (column.DefaultValue == null || column.DefaultValue == DBNull.Value)
        {
            base.ChangeColumn(table, column);
        }
        else
        {
            var def = column.DefaultValue;
            var notNull = column.ColumnProperty.IsSet(ColumnProperty.NotNull);
            column.DefaultValue = null;
            column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.Null);
            column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.NotNull);

            base.ChangeColumn(table, column);

            var mapper = _dialect.GetAndMapColumnPropertiesWithoutDefault(column);
            ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} {2} FOR {3}", this.QuoteTableNameIfRequired(table), "DF_" + table + "_" + column.Name, _dialect.Default(def), this.QuoteColumnNameIfRequired(column.Name)));

            if (notNull)
            {
                column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.NotNull);
                column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Null);
                base.ChangeColumn(table, column);
            }
        }
    }

    public override bool ColumnExists(string table, string column)
    {
        string schema;

        if (!TableExists(table))
        {
            return false;
        }

        var firstIndex = table.IndexOf(".");

        if (firstIndex >= 0)
        {
            schema = table.Substring(0, firstIndex);
            table = table.Substring(firstIndex + 1);
        }
        else
        {
            schema = _defaultSchema;
        }

        using var cmd = CreateCommand();
        using var reader = base.ExecuteQuery(cmd, string.Format("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{0}' AND TABLE_NAME='{1}' AND COLUMN_NAME='{2}'", schema, table, column));
        return reader.Read();
    }

    public override void RemoveColumnDefaultValue(string table, string column)
    {
        var sql = string.Format("SELECT name FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('{0}') AND parent_column_id = (SELECT column_id FROM sys.columns WHERE name = '{1}' AND object_id = OBJECT_ID('{0}'))", table, column);
        var constraintName = ExecuteScalar(sql);
        if (constraintName != null)
        {
            RemoveConstraint(table, constraintName.ToString());
        }
    }

    public override Index[] GetIndexes(string table)
    {
        var retVal = new List<Index>();

        var sql = @"SELECT  Tab.[name] AS TableName,
                        Ind.[name] AS IndexName,
                        Ind.[type_desc] AS IndexType,
                        Ind.[is_primary_key] AS IndexPrimary,
                        Ind.[is_unique] AS IndexUnique,
                        Ind.[is_unique_constraint] AS ConstraintUnique,
                        SUBSTRING(( SELECT  ',' + AC.name
                    FROM    sys.[tables] AS T
                            INNER JOIN sys.[indexes] I ON T.[object_id] = I.[object_id]
                            INNER JOIN sys.[index_columns] IC ON I.[object_id] = IC.[object_id]
                                                                 AND I.[index_id] = IC.[index_id]
                            INNER JOIN sys.[all_columns] AC ON T.[object_id] = AC.[object_id]
                                                               AND IC.[column_id] = AC.[column_id]
                    WHERE   Ind.[object_id] = I.[object_id]
                            AND Ind.index_id = I.index_id
                            AND IC.is_included_column = 0
                    ORDER BY IC.key_ordinal
                  FOR
                    XML PATH('') ), 2, 8000) AS KeyCols,
        SUBSTRING(( SELECT  ',' + AC.name
                    FROM    sys.[tables] AS T
                            INNER JOIN sys.[indexes] I ON T.[object_id] = I.[object_id]
                            INNER JOIN sys.[index_columns] IC ON I.[object_id] = IC.[object_id]
                                                                 AND I.[index_id] = IC.[index_id]
                            INNER JOIN sys.[all_columns] AC ON T.[object_id] = AC.[object_id]
                                                               AND IC.[column_id] = AC.[column_id]
                    WHERE   Ind.[object_id] = I.[object_id]
                            AND Ind.index_id = I.index_id
                            AND IC.is_included_column = 1
                    ORDER BY IC.key_ordinal
                  FOR
                    XML PATH('') ), 2, 8000) AS IncludeCols
FROM    sys.[indexes] Ind
        INNER JOIN sys.[tables] AS Tab ON Tab.[object_id] = Ind.[object_id]
        WHERE LOWER(Tab.[name]) = LOWER('{0}')";

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    var idx = new Index
                    {
                        Name = reader.GetString(1),
                        Clustered = reader.GetString(2) == "CLUSTERED",
                        PrimaryKey = reader.GetBoolean(3),
                        Unique = reader.GetBoolean(4),
                        UniqueConstraint = reader.GetBoolean(5),
                    };

                    if (!reader.IsDBNull(6))
                    {
                        idx.KeyColumns = (reader.GetString(6).Split(','));
                    }
                    if (!reader.IsDBNull(7))
                    {
                        idx.IncludeColumns = (reader.GetString(7).Split(','));
                    }

                    retVal.Add(idx);
                }
            }
        }

        return retVal.ToArray();
    }

    public override int GetColumnContentSize(string table, string columnName)
    {
        var result = this.ExecuteScalar("SELECT MAX(LEN(" + this.QuoteColumnNameIfRequired(columnName) + ")) FROM " + this.QuoteTableNameIfRequired(table));

        if (result == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(result);
    }

    public override Column[] GetColumns(string table)
    {
        string schema;

        var firstIndex = table.IndexOf(".");
        if (firstIndex >= 0)
        {
            schema = table.Substring(0, firstIndex);
            table = table.Substring(firstIndex + 1);
        }
        else
        {
            schema = _defaultSchema;
        }

        var pkColumns = new List<string>();
        try
        {
            pkColumns = this.ExecuteStringQuery("SELECT cu.COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE cu WHERE EXISTS ( SELECT tc.* FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc WHERE tc.TABLE_NAME = '{0}' AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME )", table);
        }
        catch (Exception)
        { }

        var idtColumns = new List<string>();
        try
        {
            idtColumns = this.ExecuteStringQuery(" select COLUMN_NAME from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = '{1}' and TABLE_NAME = '{0}' and COLUMNPROPERTY(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1", table, schema);
        }
        catch (Exception)
        { }

        var columns = new List<Column>();
        using (var cmd = CreateCommand())
        using (
                var reader =
                ExecuteQuery(cmd,
                    string.Format("select COLUMN_NAME, IS_NULLABLE, DATA_TYPE, ISNULL(CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION), COLUMN_DEFAULT, NUMERIC_SCALE from INFORMATION_SCHEMA.COLUMNS where table_name = '{0}'", table)))
        {
            while (reader.Read())
            {
                var column = new Column(reader.GetString(0), DbType.String);

                if (pkColumns.Contains(column.Name))
                {
                    column.ColumnProperty |= ColumnProperty.PrimaryKey;
                }

                if (idtColumns.Contains(column.Name))
                {
                    column.ColumnProperty |= ColumnProperty.Identity;
                }

                var nullableStr = reader.GetString(1);
                var isNullable = nullableStr == "YES";
                if (!reader.IsDBNull(2))
                {
                    var type = reader.GetString(2);
                    column.Type = Dialect.GetDbTypeFromString(type);
                }
                if (!reader.IsDBNull(3))
                {
                    column.Size = reader.GetInt32(3);
                }
                if (!reader.IsDBNull(4))
                {
                    column.DefaultValue = reader.GetValue(4);

                    if (column.DefaultValue.ToString()[1] == '(' || column.DefaultValue.ToString()[1] == '\'')
                    {
                        column.DefaultValue = column.DefaultValue.ToString().Substring(2, column.DefaultValue.ToString().Length - 4); // Example "((10))" or "('false')"
                    }
                    else
                    {
                        column.DefaultValue = column.DefaultValue.ToString().Substring(1, column.DefaultValue.ToString().Length - 2); // Example "(CONVERT([datetime],'20000101',(112)))"
                    }

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
                        if (column.DefaultValue is string defValCv && defValCv.StartsWith("CONVERT("))
                        {
                            var dt = defValCv.Substring((defValCv.IndexOf("'") + 1), defValCv.IndexOf("'", defValCv.IndexOf("'") + 1) - defValCv.IndexOf("'") - 1);
                            var d = DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                            column.DefaultValue = d;
                        }
                        else if (column.DefaultValue is string defVal)
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
                if (!reader.IsDBNull(5))
                {
                    if (column.Type == DbType.Decimal)
                    {
                        column.Size = reader.GetInt32(5);
                    }
                }

                column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

                columns.Add(column);
            }
        }

        return columns.ToArray();
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SELECT name FROM sys.databases");
    }

    public override void KillDatabaseConnections(string databaseName)
    {
        ExecuteNonQuery(string.Format(
            "USE [master]" + System.Environment.NewLine +
            "ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", databaseName));
    }

    public override void DropDatabases(string databaseName)
    {
        ExecuteNonQuery(string.Format("USE [master]" + System.Environment.NewLine + "DROP DATABASE {0}", databaseName));
    }

    public override void RemoveColumn(string table, string column)
    {
        DeleteColumnConstraints(table, column);
        DeleteColumnIndexes(table, column);
        RemoveColumnDefaultValue(table, column);
        base.RemoveColumn(table, column);
    }

    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        if (!TableExists(tableName))
        {
            throw new MigrationException($"The table '{tableName}' does not exist");
        }

        if (ColumnExists(tableName, newColumnName))
        {
            throw new MigrationException(string.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));
        }

        if (!ColumnExists(tableName, oldColumnName))
        {
            throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", tableName, oldColumnName));
        }

        if (ColumnExists(tableName, oldColumnName))
        {
            ExecuteNonQuery(string.Format("EXEC sp_rename '{0}.{1}', '{2}', 'COLUMN'", tableName, oldColumnName, newColumnName));
        }
    }

    public override void RenameTable(string oldName, string newName)
    {
        if (TableExists(newName))
        {
            throw new MigrationException(string.Format("Table with name '{0}' already exists", newName));
        }

        if (!TableExists(oldName))
        {
            throw new MigrationException(string.Format("Table with name '{0}' does not exist to rename", oldName));
        }

        ExecuteNonQuery(string.Format("EXEC sp_rename '{0}', '{1}'", oldName, newName));
    }

    // Deletes all constraints linked to a column. Sql Server
    // doesn't seems to do this.
    private void DeleteColumnConstraints(string table, string column)
    {
        var sqlContrainte = FindConstraints(table, column);
        var constraints = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, sqlContrainte))
        {
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }
        }
        // Can't share the connection so two phase modif
        foreach (var constraint in constraints)
        {
            RemoveForeignKey(table, constraint);
        }
    }

    private void DeleteColumnIndexes(string table, string column)
    {
        var sqlIndex = this.FindIndexes(table, column);
        var indexes = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, sqlIndex))
        {
            while (reader.Read())
            {
                indexes.Add(reader.GetString(0));
            }
        }
        // Can't share the connection so two phase modif
        foreach (var index in indexes)
        {
            this.RemoveIndex(table, index);
        }
    }

    protected virtual string FindIndexes(string table, string column)
    {
        return string.Format(@"
select
    i.name as IndexName
from sys.indexes i
join sys.objects o on i.object_id = o.object_id
join sys.index_columns ic on ic.object_id = i.object_id
    and ic.index_id = i.index_id
join sys.columns co on co.object_id = i.object_id
    and co.column_id = ic.column_id
where (select count(*) from sys.index_columns ic1 where ic1.object_id = i.object_id and ic1.index_id = i.index_id) = 1
and o.[Name] = '{0}'
and co.[Name] = '{1}'",
                table, column);
    }

    // FIXME: We should look into implementing this with INFORMATION_SCHEMA if possible
    // so that it would be usable by all the SQL Server implementations
    protected virtual string FindConstraints(string table, string column)
    {
        return string.Format(@"SELECT DISTINCT CU.CONSTRAINT_NAME FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE CU
INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
ON CU.CONSTRAINT_NAME = TC.CONSTRAINT_NAME
AND CU.TABLE_NAME = '{0}'
AND CU.COLUMN_NAME = '{1}'",
                table, column);
    }

    public override bool IndexExists(string table, string name)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT top 1 * FROM sys.indexes WHERE object_id = OBJECT_ID('{0}') AND name = '{1}'", table, name));
        return reader.Read();
    }

    public override void RemoveIndex(string table, string name)
    {
        if (TableExists(table) && IndexExists(table, name))
        {
            ExecuteNonQuery(string.Format("DROP INDEX {0} ON {1}", QuoteConstraintNameIfRequired(name), QuoteTableNameIfRequired(table)));
        }
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('{0}') AND is_primary_key = 1", table));
        return reader.Read() ? reader.GetString(0) : null;
    }

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        if (value is ushort)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = value;
        }
        else if (value is uint)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = value;
        }
        else if (value is ulong)
        {
            parameter.DbType = DbType.Decimal;
            parameter.Value = value;
        }
        else
        {
            base.ConfigureParameterWithValue(parameter, index, value);
        }
    }

    public override string Concatenate(params string[] strings)
    {
        return string.Join(" + ", strings);
    }
}
