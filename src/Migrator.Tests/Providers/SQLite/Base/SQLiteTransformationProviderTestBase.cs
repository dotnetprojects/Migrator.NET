using System.Threading.Tasks;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite.Base;

[TestFixture]
[Category("SQLite")]
public abstract class SQLiteTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLiteId)
            .ConnectionString;

        Provider = new SQLiteTransformationProvider(new SQLiteDialect(), connectionString, "default", null);
        Provider.BeginTransaction();

        AddDefaultTable();

        await Task.CompletedTask;
    }
}
