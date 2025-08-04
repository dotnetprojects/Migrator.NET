using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DryIoc;
using Migrator.Framework;
using Migrator.Providers;
using Migrator.Providers.Oracle;
using Migrator.Tests.Database;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using Migrator.Tests.Settings.Models;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProviderTest : TransformationProviderConstraintBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var configReader = new ConfigurationReader();

        var databaseConnectionConfig = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.Oracle);

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

        AddDefaultTable();
    }

    [Test]
    public void ChangeColumn_FromNotNullToNotNull()
    {
        Provider.ExecuteNonQuery("DELETE FROM TestTwo");
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
        Provider.Insert("TestTwo", ["Id", "TestId"], [3, "Not an Int val."]);
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.NotNull));
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.NotNull));
    }
}
