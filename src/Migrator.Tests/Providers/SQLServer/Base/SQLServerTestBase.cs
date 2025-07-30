using Migrator.Providers.SqlServer;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer.Base;

[TestFixture]
[Category("SQLServer")]
public abstract class SQLiteTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLiteConnectionConfigId)
            .ConnectionString;

        Provider = new SqlServerTransformationProvider(new SqlServerDialect(), connectionString, null, "default", null);
        Provider.BeginTransaction();

        AddDefaultTable();
    }
}
