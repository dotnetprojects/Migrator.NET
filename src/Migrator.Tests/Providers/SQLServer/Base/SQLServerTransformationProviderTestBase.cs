using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DryIoc;
using Migrator.Tests.Database;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using Migrator.Tests.Settings.Models;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer.Base;

[TestFixture]
[Category("SQLServer")]
public abstract class SQLServerTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
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

        AddDefaultTable();
    }
}
