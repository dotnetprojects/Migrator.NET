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

using System;
using System.Data;
using Migrator.Framework;

namespace Migrator.Providers.SqlServer;

/// <summary>
/// Migration transformations provider for Microsoft SQL Server Compact Edition.
/// </summary>
public class SqlServerCeTransformationProvider : SqlServerTransformationProvider
{
    public SqlServerCeTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope, providerName)
    {
    }

    public SqlServerCeTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, null, scope, providerName)
    {
    }

    protected override void CreateConnection(string providerName)
    {
        if (string.IsNullOrEmpty(providerName)) providerName = "System.Data.SqlServerCe.3.5";
        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection(); //  new SqlConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }

    public override bool ConstraintExists(string table, string name)
    {
        using (var cmd = CreateCommand())
        using (var reader =
            ExecuteQuery(cmd, string.Format("SELECT cont.constraint_name FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS cont WHERE cont.Constraint_Name='{0}'", name)))
        {
            return reader.Read();
        }
    }

    protected string GetSchemaName(string longTableName)
    {
        throw new MigrationException("SQL CE does not support database schemas.");
    }

    public override bool TableExists(string table)
    {
        using (var cmd = CreateCommand())
        using (var reader = base.ExecuteQuery(cmd, string.Format("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{0}'", table)))
        {
            return reader.Read();
        }
    }

    public override bool ColumnExists(string table, string column)
    {
        if (!TableExists(table))
        {
            return false;
        }
        var firstIndex = table.IndexOf(".");
        if (firstIndex >= 0)
        {
            table = table.Substring(firstIndex + 1);
        }

        using (var cmd = CreateCommand())
        using (
            var reader = base.ExecuteQuery(cmd, string.Format("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{0}' AND COLUMN_NAME='{1}'", table, column)))
        {
            return reader.Read();
        }
    }

    public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
    {
        if (ColumnExists(tableName, newColumnName))
            throw new MigrationException(String.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));

        if (!ColumnExists(tableName, oldColumnName))
            throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", tableName, oldColumnName));

        if (ColumnExists(tableName, oldColumnName))
        {
            var column = GetColumnByName(tableName, oldColumnName);

            AddColumn(tableName, new Column(newColumnName, column.Type, column.ColumnProperty, column.DefaultValue));
            ExecuteNonQuery(string.Format("UPDATE {0} SET {1}={2}", tableName, newColumnName, oldColumnName));
            RemoveColumn(tableName, oldColumnName);
        }
    }

    // Not supported by SQLCe when we have a better schemadumper which gives the exact sql construction including constraints we may use it to insert into a new table and then drop the old table...but this solution is dangerous for big tables.
    public override void RenameTable(string oldName, string newName)
    {
        throw new NotSupportedException("Table Rename is not supported in SQL CE");
    }

    protected override string FindConstraints(string table, string column)
    {
        return
            string.Format("SELECT cont.constraint_name FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE cont "
                          + "WHERE cont.Table_Name='{0}' AND cont.column_name = '{1}'",
                          table, column);
    }
}
