using Migrator.Providers.SqlServer;
using System.Data;

namespace Migrator.Providers.Utility;

public static class SqlServerUtility
{
    public static void RemoveAllTablesFromDefaultDatabase(string connectionString)
    {
        var d = new SqlServerDialect();
        using (var p = d.NewProviderForDialect(connectionString, null, null, null))
        using (var connection = p.Connection)
        {
            connection.Open();
            RemoveAllForeignKeys(connection);
            DropAllTables(connection);
            connection.Close();
        }
    }

    private static void DropAllTables(IDbConnection connection)
    {
        ExecuteForEachTable(connection, "DROP TABLE ?");
    }

    private static void RemoveAllForeignKeys(IDbConnection connection)
    {
        using (
            var dropConstraintsCommand = connection.CreateCommand())
        {
            dropConstraintsCommand.CommandText = @"DECLARE @Sql NVARCHAR(500) DECLARE @Cursor CURSOR

SET @Cursor = CURSOR FAST_FORWARD FOR

SELECT DISTINCT sql = 'ALTER TABLE [' + tc2.TABLE_NAME + '] DROP [' + rc1.CONSTRAINT_NAME + ']'

FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc1

LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc2 ON tc2.CONSTRAINT_NAME =rc1.CONSTRAINT_NAME

OPEN @Cursor FETCH NEXT FROM @Cursor INTO @Sql

WHILE (@@FETCH_STATUS = 0)

BEGIN

Exec sys.sp_executesql @Sql

FETCH NEXT FROM @Cursor INTO @Sql

END

CLOSE @Cursor DEALLOCATE @Cursor";
            dropConstraintsCommand.CommandType = CommandType.Text;
            dropConstraintsCommand.ExecuteNonQuery();
        }
    }

    private static void ExecuteForEachTable(IDbConnection connection, string command)
    {
        using (var forEachCommand = connection.CreateCommand())
        {
            forEachCommand.CommandText = "sp_MSforeachtable";
            forEachCommand.CommandType = CommandType.StoredProcedure;
            var par = forEachCommand.CreateParameter();
            par.ParameterName = "@command1";
            par.Value = command;
            forEachCommand.Parameters.Add(par);
            forEachCommand.ExecuteNonQuery();
        }
    }
}
