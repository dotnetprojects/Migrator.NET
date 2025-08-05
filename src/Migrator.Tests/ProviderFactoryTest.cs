using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using Npgsql;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class ProviderFactoryTest
{
    [Test]
    public void CanGetDialectsForProvider()
    {
        foreach (var provider in Enum.GetValues(typeof(ProviderTypes)).Cast<ProviderTypes>().Where(x => x != ProviderTypes.none))
        {
            Assert.That(ProviderFactory.DialectForProvider(provider), Is.Not.Null);
        }

        Assert.That(ProviderFactory.DialectForProvider(ProviderTypes.none), Is.Null);
    }

    [SetUp]
    public void SetUp()
    {
        DbProviderFactories.RegisterFactory("Npgsql", () => NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySql.Data.MySqlClient", () => MySql.Data.MySqlClient.MySqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Oracle.DataAccess.Client", () => Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.SqlClient", () => Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.SQLite", () => System.Data.SQLite.SQLiteFactory.Instance);
    }

    [Test]
    [Category("MySql")]
    public void CanLoad_MySqlProvider()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.MySQLId)?.ConnectionString;
        if (!String.IsNullOrEmpty(connectionString))
        {
            using var provider = ProviderFactory.Create(ProviderTypes.Mysql, connectionString, null);
            Assert.That(provider, Is.Not.Null);
        }
    }

    [Test]
    [Category("Oracle")]
    public void CanLoad_OracleProvider()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.OracleId)?.ConnectionString;
        if (!String.IsNullOrEmpty(connectionString))
        {
            using var provider = ProviderFactory.Create(ProviderTypes.Oracle, connectionString, null);
            Assert.That(provider, Is.Not.Null);
        }
    }

    [Test]
    [Category("Postgre")]
    public void CanLoad_PostgreSQLProvider()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.PostgreSQL)?.ConnectionString;
        if (!String.IsNullOrEmpty(connectionString))
        {
            using var provider = ProviderFactory.Create(ProviderTypes.PostgreSQL, connectionString, null);
            Assert.That(provider, Is.Not.Null);
        }
    }

    [Test]
    [Category("SQLite")]
    public void CanLoad_SQLiteProvider()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLiteId)?.ConnectionString;
        if (!String.IsNullOrEmpty(connectionString))
        {
            using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connectionString, null);
            Assert.That(provider, Is.Not.Null);
        }
    }

    [Test]
    [Category("SqlServer")]
    public void CanLoad_SqlServerProvider()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLServerId)?.ConnectionString;
        if (!String.IsNullOrEmpty(connectionString))
        {
            using var provider = ProviderFactory.Create(ProviderTypes.SqlServer, connectionString, null);
            Assert.That(provider, Is.Not.Null);
        }
    }
}
