using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using Mapster;
using Microsoft.Data.SqlClient;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Database.Models;
using Migrator.Tests.Settings.Models;

namespace Migrator.Tests.Database.DerivedDatabaseIntegrationTestServices;

public class SqlServerDatabaseIntegrationTestService(TimeProvider timeProvider, IDatabaseNameService databaseNameService)
    : DatabaseIntegrationTestServiceBase(databaseNameService), IDatabaseIntegrationTestService
{
    private const string SqlServerInitialCatalogString = "Initial Catalog";

    public override async Task<DatabaseInfo> CreateTestDatabaseAsync(DatabaseConnectionConfig databaseConnectionConfig, CancellationToken cancellationToken)
    {
        using var context = new DataConnection(new DataOptions().UseSqlServer(databaseConnectionConfig.ConnectionString));
        await context.ExecuteAsync("use master", cancellationToken);

        var databaseNames = context.Query<string>($"SELECT name FROM sys.databases WHERE name NOT IN ('master', 'model', 'msdb', 'tempdb')").ToList();

        var toBeDeletedDatabaseNames = databaseNames.Where(x =>
            {
                var creationDate = DatabaseNameService.ReadTimeStampFromString(x);
                return creationDate.HasValue && creationDate.Value < timeProvider.GetUtcNow().Subtract(_MinTimeSpanBeforeDatabaseDeletion);
            }).ToList();

        foreach (var databaseName in toBeDeletedDatabaseNames)
        {
            var databaseInfoToBeDeleted = new DatabaseInfo { DatabaseConnectionConfig = databaseConnectionConfig, DatabaseName = databaseName };
            await DropDatabaseAsync(databaseInfoToBeDeleted, cancellationToken);
        }

        var newDatabaseName = DatabaseNameService.CreateDatabaseName();

        await context.ExecuteAsync($"CREATE DATABASE [{newDatabaseName}]", cancellationToken);

        var clonedDatabaseConnectionConfig = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>();

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = clonedDatabaseConnectionConfig.ConnectionString
        };

        if (builder.TryGetValue(SqlServerInitialCatalogString, out var value))
        {
            builder.Remove(SqlServerInitialCatalogString);
            builder.Add(SqlServerInitialCatalogString, newDatabaseName);
        }

        clonedDatabaseConnectionConfig.ConnectionString = builder.ConnectionString;

        var databaseInfo = new DatabaseInfo
        {
            DatabaseConnectionConfig = clonedDatabaseConnectionConfig,
            DatabaseName = newDatabaseName
        };

        return databaseInfo;
    }

    public override async Task DropDatabaseAsync(DatabaseInfo databaseInfo, CancellationToken cancellationToken)
    {
        var creationDate = DatabaseNameService.ReadTimeStampFromString(databaseInfo.DatabaseName);

        if (!creationDate.HasValue)
        {
            throw new Exception("You tried to drop a database that was not created by this service. For safety reasons we deny your request.");
        }

        using var context = new DataConnection(new DataOptions().UseSqlServer(databaseInfo.DatabaseConnectionConfig.ConnectionString));
        await context.ExecuteAsync("use master", cancellationToken);

        try
        {
            await context.ExecuteAsync($"ALTER DATABASE [{databaseInfo.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", cancellationToken);
            await context.ExecuteAsync($"DROP DATABASE [{databaseInfo.DatabaseName}]", cancellationToken);
        }
        catch (SqlException ex)
        {
            // 3701: "Cannot drop the database because it does not exist or you do not have permission"
            if (ex.Errors.Count > 0 && ex.Errors.Cast<SqlError>().Any(x => x.Number == 3701))
            {
                await Task.Delay(5000, cancellationToken);

                var count = await context.ExecuteAsync<int>($"SELECT COUNT(*) FROM sys.databases WHERE name = '{databaseInfo.DatabaseName}'");

                if (count == 1)
                {
                    throw new UnauthorizedAccessException($"The database '{databaseInfo.DatabaseName}' cannot be dropped but it still exists so we assume you do not have sufficient privileges to drop databases or this database.", ex);
                }
                else
                {
                    // The database was removed by another (asynchronously) running test that kicked in earlier.
                    // That's ok for us as we have achieved the goal.
                }
            }
            else
            {
                throw;
            }
        }
    }
}
