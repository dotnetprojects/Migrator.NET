using System;
using System.Data;
using System.Data.Common;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using ProviderFactories = DotNetProjects.Migrator.Providers.DbProviderFactories;

namespace Migrator.Tests;

public class CoreCleanupRegressionTests
{
    [TestCase("CreateATable", "Create a table")]
    [TestCase("createTable", "Create table")]
    [TestCase("x", "X")]
    [TestCase("_123", "")]
    [TestCase("", "")]
    [TestCase("001_CreateATable_123", "Create a table")]
    public void HumanNamesHandleShortAndLowercaseNames(string name, string expected)
    {
        Assert.That(StringUtils.ToHumanName(name), Is.EqualTo(expected));
    }

    [Test]
    public void RegisteredFactoryErrorsArePreserved()
    {
        var name = "Cleanup.FailingFactory";
        var failure = new InvalidOperationException("Factory configuration failed.");
        ProviderFactories.RegisterFactory(name, () => throw failure);

        var actual = Assert.Throws<InvalidOperationException>(() =>
            DbProviderFactoriesHelper.GetFactory(name, "Missing.Assembly", "Missing.Factory"));

        Assert.That(actual, Is.SameAs(failure));
    }

    [Test]
    public void FactoryResolutionUsesCustomThenSystemThenReflection()
    {
        var name = "Cleanup.FactoryPrecedence." + Guid.NewGuid();
        var assembly = typeof(SqliteFactory).Assembly.GetName().Name;
        var type = typeof(SqliteFactory).FullName;
        Assert.That(DbProviderFactoriesHelper.GetFactory(name, assembly, type), Is.SameAs(SqliteFactory.Instance));

        System.Data.Common.DbProviderFactories.RegisterFactory(name, SqliteFactory.Instance);
        try
        {
            Assert.That(DbProviderFactoriesHelper.GetFactory(name, "Missing.Assembly", "Missing.Factory"), Is.SameAs(SqliteFactory.Instance));
            var custom = new TestFactory();
            ProviderFactories.RegisterFactory(name, () => custom);
            Assert.That(DbProviderFactoriesHelper.GetFactory(name, assembly, type), Is.SameAs(custom));
        }
        finally
        {
            System.Data.Common.DbProviderFactories.UnregisterFactory(name);
        }
    }

    [Test]
    [SetCulture("tr-TR")]
    public void SqlTypeLookupDoesNotDependOnCurrentCulture()
    {
        var names = new TypeNames();
        names.Put(DbType.Int32, "INTEGER");
        names.Put(DbType.Int64, 20, "BIGINT");
        names.PutAlias(DbType.Int16, "SMALLINT");
        Assert.That(names.GetDbType("integer"), Is.EqualTo(DbType.Int32));
        Assert.That(names.GetDbType("bigint"), Is.EqualTo(DbType.Int64));
        Assert.That(names.GetDbType("smallint"), Is.EqualTo(DbType.Int16));
    }

    [Test]
    public void DefaultTypeTemplatesExpandSizePrecisionAndScale()
    {
        var names = new TypeNames();
        names.Put(DbType.String, "VARCHAR($l)");
        names.Put(DbType.Decimal, "DECIMAL($p,$s)");
        names.Put(DbType.Decimal, 5, "SMALLDECIMAL($p,$s)");

        Assert.That(names.Get(DbType.String, 80, 0, 0), Is.EqualTo("VARCHAR(80)"));
        Assert.That(names.Get(DbType.Decimal, 10, 18, 4), Is.EqualTo("DECIMAL(18,4)"));
        Assert.That(names.Get(DbType.Decimal, 5, 8, 2), Is.EqualTo("SMALLDECIMAL(8,2)"));
    }

    private sealed class TestFactory : DbProviderFactory { }
}
