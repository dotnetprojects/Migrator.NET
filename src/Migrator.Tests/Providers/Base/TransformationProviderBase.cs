using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DryIoc;
using Migrator.Tests.Database;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using Migrator.Tests.Settings.Models;
using Npgsql;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Base;

/// <summary>
/// Base class for provider tests.
/// </summary>
public abstract class TransformationProviderBase
{
    private IDbConnection _dbConnection;
    protected ITransformationProvider Provider;

    [TearDown]
    public virtual void TearDown()
    {
        DropTestTables();

        Provider?.Rollback();

        _dbConnection?.Dispose();
    }

    protected void DropTestTables()
    {
        // Because MySql doesn't support schema transaction
        // we got to remove the tables manually... sad...
        try
        {
            Provider.RemoveTable("TestTwo");
        }
        catch (Exception)
        {
        }
        try
        {
            Provider.RemoveTable("Test");
        }
        catch (Exception)
        {
        }
        try
        {
            Provider.RemoveTable("SchemaInfo");
        }
        catch (Exception)
        {
        }
    }

    protected async Task BeginOracleTransactionAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var configReader = new ConfigurationReader();

        var databaseConnectionConfig = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.OracleId);

        var connectionString = databaseConnectionConfig?.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new IgnoreException($"No Oracle {nameof(DatabaseConnectionConfig.ConnectionString)} is set.");
        }

        DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess.Client", () => Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);

        using var container = new Container();
        container.RegisterDatabaseIntegrationTestService();
        var databaseIntegrationTestServiceFactory = container.Resolve<IDatabaseIntegrationTestServiceFactory>();
        var oracleIntegrationTestService = databaseIntegrationTestServiceFactory.Create(DatabaseProviderType.Oracle);
        var databaseInfo = await oracleIntegrationTestService.CreateTestDatabaseAsync(databaseConnectionConfig, cts.Token);

        Provider = new OracleTransformationProvider(new OracleDialect(), databaseInfo.DatabaseConnectionConfig.ConnectionString, null, "default", "Oracle.ManagedDataAccess.Client");

        Provider.BeginTransaction();
    }

    protected async Task BeginPostgreSQLTransactionAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var configReader = new ConfigurationReader();

        var databaseConnectionConfig = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.PostgreSQL);

        var connectionString = databaseConnectionConfig?.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new IgnoreException("No Postgre SQL connection string is set.");
        }

        DbProviderFactories.RegisterFactory("Npgsql", () => Npgsql.NpgsqlFactory.Instance);

        using var container = new Container();
        container.RegisterDatabaseIntegrationTestService();
        var databaseIntegrationTestServiceFactory = container.Resolve<IDatabaseIntegrationTestServiceFactory>();
        var postgreIntegrationTestService = databaseIntegrationTestServiceFactory.Create(DatabaseProviderType.Postgres);
        var databaseInfo = await postgreIntegrationTestService.CreateTestDatabaseAsync(databaseConnectionConfig, cts.Token);


        _dbConnection = new NpgsqlConnection(databaseInfo.DatabaseConnectionConfig.ConnectionString);

        Provider = new PostgreSQLTransformationProvider(new PostgreSQLDialect(), _dbConnection, null, "default", "Npgsql");
        Provider.BeginTransaction();

        await Task.CompletedTask;
    }

    protected async Task BeginSQLiteTransactionAsync()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLiteId)
            .ConnectionString;

        Provider = new SQLiteTransformationProvider(new SQLiteDialect(), connectionString, "default", null);
        Provider.BeginTransaction();

        await Task.CompletedTask;
    }

    protected async Task BeginSQLServerTransactionAsync()
    {
        var configReader = new ConfigurationReader();

        var databaseConnectionConfig = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLServerId);

        var connectionString = databaseConnectionConfig?.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new IgnoreException($"No SQL Server {nameof(DatabaseConnectionConfig.ConnectionString)} is set.");
        }

        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", () => Microsoft.Data.SqlClient.SqlClientFactory.Instance);

        using var container = new Container();
        container.RegisterDatabaseIntegrationTestService();
        var databaseIntegrationTestServiceFactory = container.Resolve<IDatabaseIntegrationTestServiceFactory>();
        var sqlServerIntegrationTestService = databaseIntegrationTestServiceFactory.Create(DatabaseProviderType.SQLServer);
        var databaseInfo = await sqlServerIntegrationTestService.CreateTestDatabaseAsync(databaseConnectionConfig, CancellationToken.None);

        Provider = new SqlServerTransformationProvider(new SqlServerDialect(), databaseInfo.DatabaseConnectionConfig.ConnectionString, "dbo", "default", "Microsoft.Data.SqlClient");

        Provider.BeginTransaction();
    }
}
