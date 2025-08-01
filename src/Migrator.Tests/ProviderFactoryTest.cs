using System;
using System.Configuration;
using System.Linq;
using Migrator.Providers;

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

    [Test]
    [Category("MySql")]
    public void CanLoad_MySqlProvider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.Mysql, ConfigurationManager.AppSettings["MySqlConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    [Category("Oracle")]
    public void CanLoad_OracleProvider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.Oracle, ConfigurationManager.AppSettings["OracleConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    [Category("Postgre")]
    public void CanLoad_PostgreSQLProvider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.PostgreSQL, ConfigurationManager.AppSettings["NpgsqlConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    [Category("SQLite")]
    public void CanLoad_SQLiteProvider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, ConfigurationManager.AppSettings["SQLiteConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    [Category("SqlServer2005")]
    public void CanLoad_SqlServer2005Provider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SqlServer2005, ConfigurationManager.AppSettings["SqlServer2005ConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    [Category("SqlServerCe")]
    public void CanLoad_SqlServerCeProvider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SqlServerCe, ConfigurationManager.AppSettings["SqlServerCeConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }

    [Test]
    [Category("SqlServer")]
    public void CanLoad_SqlServerProvider()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SqlServer, ConfigurationManager.AppSettings["SqlServerConnectionString"], null);

        Assert.That(provider, Is.Not.Null);
    }
}