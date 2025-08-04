using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using Mapster;
using System.Linq;
using Microsoft.Data.Sqlite;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Settings.Models;
using Migrator.Tests.Database.Models;

namespace Migrator.Tests.Database.DerivedDatabaseIntegrationTestServices;

public class SQLiteDatabaseIntegrationTestService(TimeProvider timeProvider, IDatabaseNameService databaseNameService)
    : DatabaseIntegrationTestServiceBase(databaseNameService), IDatabaseIntegrationTestService
{
    private const string SqliteDataSourceName = "data source";
    private static readonly string[] _sqliteFileExtensions = ["*.sqlite", "*.db", "*.sqlite3", "*.db3", "*.sqlitedb", "*.*wal", "*.*shm", "*.*journal"];

    public override async Task<DatabaseInfo> CreateTestDatabaseAsync(DatabaseConnectionConfig databaseConnectionConfig, CancellationToken cancellationToken)
    {
        var builder = new SQLiteConnectionStringBuilder { ConnectionString = databaseConnectionConfig.ConnectionString };

        if (!builder.TryGetValue(SqliteDataSourceName, out var dataSource))
        {
            throw new Exception($@"No {SqliteDataSourceName} given in your SQLite connection string. Use a fully qualified path, e.g. Data Source=C:\bla\bla.db");
        }

        var dataSourceString = (string)dataSource;

        if (dataSourceString.Contains("memory", StringComparison.InvariantCultureIgnoreCase))
        {
            throw new Exception("You are using an 'in memory' SQLite database connection string.");
        }

        if (!Path.IsPathFullyQualified(dataSourceString))
        {
            throw new Exception("You need to use a fully qualified path in your SQLite connection string.");
        }

        var directory = Path.GetDirectoryName(dataSourceString);

        var filePaths = _sqliteFileExtensions.Select(x => Directory.EnumerateFiles(directory, x, SearchOption.TopDirectoryOnly))
                        .SelectMany(x => x)
                        .ToList();

        List<DatabaseInfo> toBeDeletedDatabases = [];

        foreach (var filePath in filePaths)
        {
            var fileName = Path.GetFileName(filePath);

            var creationDate = DatabaseNameService.ReadTimeStampFromString(fileName);

            if (creationDate.HasValue && creationDate.Value < timeProvider.GetUtcNow().Subtract(MinTimeSpanBeforeDatabaseDeletion))
            {
                var builderExistingFile = new SqliteConnectionStringBuilder { DataSource = filePath };
                var dataConnectionConfigExistingFile = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>();
                dataConnectionConfigExistingFile.ConnectionString = builderExistingFile.ConnectionString;

                var databaseInfo = new DatabaseInfo
                {
                    DatabaseConnectionConfig = dataConnectionConfigExistingFile,
                    DatabaseName = fileName
                };

                toBeDeletedDatabases.Add(databaseInfo);
            }
        }

        foreach (var toBeDeletedDatabase in toBeDeletedDatabases)
        {
            await DropDatabaseAsync(toBeDeletedDatabase, cancellationToken);
        }

        builder.Remove(SqliteDataSourceName);

        var newSqliteDatabaseName = $"{DatabaseNameService.CreateDatabaseName()}.db";
        var fullSqliteDatabaseName = Path.Combine(directory, newSqliteDatabaseName);

        builder.Add(SqliteDataSourceName, fullSqliteDatabaseName);

        var newDatabaseConnectionConfig = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>();
        newDatabaseConnectionConfig.ConnectionString = builder.ConnectionString;

        // Create the database file physically
        using var context = new DataConnection(new DataOptions().UseSQLite(newDatabaseConnectionConfig.ConnectionString));

        var databaseInfoNew = new DatabaseInfo
        {
            DatabaseConnectionConfig = newDatabaseConnectionConfig,
            DatabaseConnectionConfigMaster = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>(),
            DatabaseName = newSqliteDatabaseName,
        };

        return databaseInfoNew;
    }

    public override async Task DropDatabaseAsync(DatabaseInfo databaseInfo, CancellationToken cancellationToken)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = databaseInfo.DatabaseConnectionConfig.ConnectionString };

        if (!builder.TryGetValue(SqliteDataSourceName, out var dataSource))
        {
            throw new Exception();
        }

        var dataSourceString = (string)dataSource;

        if (!Path.IsPathFullyQualified(dataSourceString))
        {
            throw new Exception("Path is not fully qualified.");
        }

        var fileName = Path.GetFileName(dataSourceString);

        var creationDate = DatabaseNameService.ReadTimeStampFromString(fileName);

        if (!creationDate.HasValue)
        {
            throw new Exception("You tried to drop a database that was not created by this service. For safety reasons we deny your request.");
        }

        File.Delete(dataSourceString);

        await Task.CompletedTask;
    }
}