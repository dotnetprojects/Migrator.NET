using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteCollationRegressionTests
{
    [TestCase("NOCASE", "HELLO")]
    [TestCase("RTRIM", "hello ")]
    public void AddingAColumnPreservesCollationDataAndUniqueConstraints(string collation, string equivalent)
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("Names", new Column("Id", DbType.Int32),
            new Column("Name", DbType.String, 50) { Collation = Collation.Named(collation) },
            new PrimaryKeyConstraint("PK_Names", "Id"), new UniqueConstraint("UQ_Names", "Name"));
        provider.Insert("Names", ["Id", "Name"], [1, "hello"]);

        provider.AddColumn("Names", new Column("Extra", DbType.Int32) { DefaultValue = 7 });

        Assert.That(provider.GetColumns("Names").Single(c => c.Name == "Name").Collation?.Name, Is.EqualTo(collation));
        Assert.That(provider.ExecuteScalar("SELECT Extra FROM Names WHERE Name = '" + equivalent + "'"), Is.EqualTo(7));
        Assert.Catch(() => provider.Insert("Names", ["Id", "Name"], [2, equivalent]));
        Assert.That(provider.GetTableConstraints("Names").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("PK_Names"));
        Assert.That(provider.GetTableConstraints("Names").OfType<UniqueConstraint>().Single().Name, Is.EqualTo("UQ_Names"));
    }

    [Test]
    public void MetadataReadsOnlyColumnLevelCollationsAndUsesTheLastDeclaration()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.ExecuteNonQuery("""
            CREATE TABLE Names (
                "Odd,Name" TEXT COLLATE/*separator*/[NOCASE] COLLATE "RTRIM",
                Literal TEXT DEFAULT 'COLLATE NOCASE',
                Expression TEXT DEFAULT ('x' COLLATE NOCASE),
                CHECK (Literal COLLATE BINARY <> 'bad'),
                CONSTRAINT "COLLATE" UNIQUE ("Odd,Name"))
            """);
        var columns = provider.GetColumns("Names");
        Assert.That(columns.Single(c => c.Name == "Odd,Name").Collation?.Name, Is.EqualTo("RTRIM"));
        Assert.That(columns.Single(c => c.Name == "Literal").Collation, Is.Null);
        Assert.That(columns.Single(c => c.Name == "Expression").Collation, Is.Null);
        provider.AddColumn("Names", new Column("Extra", DbType.Int32));
        provider.ExecuteNonQuery("INSERT INTO Names (\"Odd,Name\") VALUES ('hello')");
        Assert.That(provider.ExecuteScalar("SELECT Literal FROM Names"), Is.EqualTo("COLLATE NOCASE"));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO Names (\"Odd,Name\") VALUES ('hello ' )"));
    }

    [Test]
    public void CustomQuotedCollationSurvivesInspectionAndRebuild()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        const string collation = "custom \" comparison";
        connection.CreateCollation(collation, (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.AddTable("Names", new Column("Name", DbType.String) { Collation = Collation.Named(collation) });
        provider.Insert("Names", ["Name"], ["hello"]);
        provider.AddColumn("Names", new Column("Extra", DbType.Int32));
        Assert.That(provider.GetColumns("Names").Single(c => c.Name == "Name").Collation?.Name, Is.EqualTo(collation));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Names WHERE Name = 'HELLO'"), Is.EqualTo(1));
    }

    [Test]
    public void ChangingCollationToBinaryIsExplicitAndKeepsTheUniqueConstraint()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("Names", new Column("Name", DbType.String) { Collation = Collation.AsciiIgnoreCase },
            new UniqueConstraint("UQ_Names", "Name"));
        provider.Insert("Names", ["Name"], ["hello"]);
        provider.ChangeColumn("Names", new Column("Name", DbType.String) { Collation = Collation.Binary });
        provider.Insert("Names", ["Name"], ["HELLO"]);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Names"), Is.EqualTo(2));
        Assert.Catch(() => provider.Insert("Names", ["Name"], ["hello"]));
    }

    [Test]
    public void FailingCollationChangeRollsBackSchemaDataAndComparisonBehavior()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("Names", new Column("Name", DbType.String) { Collation = Collation.Binary },
            new UniqueConstraint("UQ_Names", "Name"));
        provider.Insert("Names", ["Name"], ["hello"]);
        provider.Insert("Names", ["Name"], ["HELLO"]);
        Assert.Catch(() => provider.ChangeColumn("Names", new Column("Name", DbType.String) { Collation = Collation.AsciiIgnoreCase }));
        Assert.That(provider.GetColumns("Names").Single().Collation?.Name, Is.EqualTo("BINARY"));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Names"), Is.EqualTo(2));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Names WHERE Name = 'hello'"), Is.EqualTo(1));
    }

    [Test]
    public void UnsupportedIndexCollationFailsBeforeReplacingTheTable()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("Names", new Column("Name", DbType.String));
        provider.ExecuteNonQuery("CREATE UNIQUE INDEX UX_Names ON Names (Name COLLATE NOCASE)");
        provider.Insert("Names", ["Name"], ["hello"]);
        Assert.Throws<NotSupportedException>(() => provider.AddColumn("Names", new Column("Extra", DbType.Int32)));
        Assert.That(provider.ColumnExists("Names", "Extra"), Is.False);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Names"), Is.EqualTo(1));
        Assert.Catch(() => provider.Insert("Names", ["Name"], ["HELLO"]));
    }
}
