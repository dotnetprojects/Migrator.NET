using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DryIoc;
using Migrator.Tests.Database;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using Migrator.Tests.Settings.Models;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Base;

/// <summary>
/// Base class for provider tests.
/// </summary>
public abstract class TransformationProviderBase
{
    protected ITransformationProvider Provider;

    protected async Task StartOracleTransactionAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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

    protected async Task BeginSQLiteTransactionAsync()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLiteId)
            .ConnectionString;

        Provider = new SQLiteTransformationProvider(new SQLiteDialect(), connectionString, "default", null);
        Provider.BeginTransaction();

        await Task.CompletedTask;
    }
}
