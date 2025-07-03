using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Providers.SQLite;
using Migrator.Tests.Settings;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite.Base;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProviderGenericTests : TransformationProviderBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById("SQLiteConnectionString")
            .ConnectionString;

        Provider = new SQLiteTransformationProvider(new SQLiteDialect(), connectionString, "default", null);
        Provider.BeginTransaction();

        AddDefaultTable();
    }
}
