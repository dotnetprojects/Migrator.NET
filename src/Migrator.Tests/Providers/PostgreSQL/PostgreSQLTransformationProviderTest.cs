using System;
using System.Configuration;
using Migrator.Providers;
using Migrator.Providers.PostgreSQL;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProviderTest : TransformationProviderConstraintBase
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