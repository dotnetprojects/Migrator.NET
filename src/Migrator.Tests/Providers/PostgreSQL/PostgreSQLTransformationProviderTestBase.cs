using Migrator.Providers;
using Migrator.Providers.PostgreSQL;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public abstract class PostgreSQLTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.PostgreSQL)
            .ConnectionString;

        DbProviderFactories.RegisterFactory("Npgsql", () => Npgsql.NpgsqlFactory.Instance);

        Provider = new PostgreSQLTransformationProvider(new PostgreSQLDialect(), connectionString, null, "default", "Npgsql");
        Provider.BeginTransaction();

        AddDefaultTable();
    }
}
