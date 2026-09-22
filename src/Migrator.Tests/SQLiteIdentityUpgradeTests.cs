using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SQLite.Models;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteIdentityUpgradeTests
{
    [Test]
    public void IdentityAndPrimaryKeyCanBeAddedAtomicallyToAPopulatedTable()
    {
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("Settings", new Column("Name", DbType.String));
        provider.Insert("Settings", ["Name"], ["first"]);
        provider.Insert("Settings", ["Name"], ["second"]);

        var definition = provider.GetSQLiteTableInfo("Settings");
        definition.Columns.Add(new Column("Id", DbType.Int32) { IsIdentity = true });
        definition.ColumnMappings.Add(new MappingInfo { OldName = null, NewName = "Id" });
        definition.PrimaryKey = new PrimaryKeyConstraint("PK_Settings", "Id");
        provider.RecreateTable(definition);

        Assert.That(provider.ExecuteScalar("SELECT COUNT(DISTINCT Id) FROM Settings"), Is.EqualTo(2));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Settings WHERE Name IN ('first', 'second')"), Is.EqualTo(2));
        Assert.That(provider.GetColumns("Settings").Single(c => c.Name == "Id").IsIdentity, Is.True);
        Assert.That(provider.GetTableConstraints("Settings").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("PK_Settings"));
        provider.Insert("Settings", ["Name"], ["third"]);
        Assert.That(provider.ExecuteScalar("SELECT Id FROM Settings WHERE Name = 'third'"), Is.EqualTo(3));
    }

    [Test]
    public void AddingIdentityWithoutItsKeyRejectsTheOperationWithoutLosingData()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("Settings", new Column("Name", DbType.String));
        provider.Insert("Settings", ["Name"], ["first"]);
        Assert.Throws<MigrationException>(() => provider.AddColumn("Settings", new Column("Id", DbType.Int32) { IsIdentity = true }));
        Assert.That(provider.GetColumns("Settings").Select(c => c.Name), Is.EqualTo(new[] { "Name" }));
        Assert.That(provider.ExecuteScalar("SELECT Name FROM Settings"), Is.EqualTo("first"));
        Assert.That(provider.GetTables(), Is.EqualTo(new[] { "Settings" }));
    }
}
