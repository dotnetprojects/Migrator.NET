using System.Data.SqlClient;
using Migrator.Providers;
using Migrator.Providers.SqlServer;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer.Base;

[TestFixture]
[Category("SQLServer")]
public abstract class SQLServerTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLServerConnectionConfigId)
            .ConnectionString;


        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", () => Microsoft.Data.SqlClient.SqlClientFactory.Instance);

        Provider = new SqlServerTransformationProvider(new SqlServerDialect(), connectionString, "dbo", "default", "Microsoft.Data.SqlClient");
        Provider.BeginTransaction();

        AddDefaultTable();
    }
}
