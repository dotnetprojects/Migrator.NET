using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using Mapster;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Database.Models;
using Migrator.Tests.Settings.Models;
using Npgsql;

namespace Migrator.Tests.Database.DerivedDatabaseIntegrationTestServices;

public class PostgreSqlDatabaseIntegrationTestService(TimeProvider timeProvider, IDatabaseNameService databaseNameService)
    : DatabaseIntegrationTestServiceBase(databaseNameService), IDatabaseIntegrationTestService
{
    public override async Task<DatabaseInfo> CreateTestDatabaseAsync(DatabaseConnectionConfig databaseConnectionConfig, CancellationToken cancellationToken)
    {
        var clonedDatabaseConnectionConfig = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>();

        var builder = new NpgsqlConnectionStringBuilder
        {
            ConnectionString = clonedDatabaseConnectionConfig.ConnectionString,
            Database = "postgres"
        };

        List<string> databaseNames;

        using (var context = new DataConnection(new DataOptions().UsePostgreSQL(builder.ConnectionString)))
        {
            databaseNames = await context.QueryToListAsync<string>("SELECT datname from pg_database WHERE datistemplate = false", cancellationToken);
        }

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
        using (var context = new DataConnection(new DataOptions().UsePostgreSQL(builder.ConnectionString)))
        {
            await context.ExecuteAsync($"CREATE DATABASE \"{newDatabaseName}\"", cancellationToken);
        }

        var connectionStringBuilder2 = new NpgsqlConnectionStringBuilder(clonedDatabaseConnectionConfig.ConnectionString)
        {
            Database = newDatabaseName
        };

        clonedDatabaseConnectionConfig.ConnectionString = connectionStringBuilder2.ConnectionString;

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

        var builder = new NpgsqlConnectionStringBuilder(databaseInfo.DatabaseConnectionConfig.ConnectionString)
        {
            Database = "postgres"
        };

        var dataOptions = new DataOptions().UsePostgreSQL(builder.ConnectionString);

        using (var context = new DataConnection(dataOptions))
        {

            try
            {
                await context.ExecuteAsync($"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{databaseInfo.DatabaseName}'", cancellationToken);
                await context.ExecuteAsync($"DROP DATABASE \"{databaseInfo.DatabaseName}\"", cancellationToken);
            }
            catch
            {
                await Task.Delay(2000, cancellationToken);

                var count = await context.ExecuteAsync<int>($"SELECT COUNT(*) from pg_database WHERE datistemplate = false AND datname = '{databaseInfo.DatabaseName}'", cancellationToken);

                if (count == 1)
                {
                    throw;
                }
                else
                {
                    // The database was removed by another asynchronously running test that kicked in earlier.
                    // That's ok for us as we have achieved our objective.
                }
            }
        }
    }
}