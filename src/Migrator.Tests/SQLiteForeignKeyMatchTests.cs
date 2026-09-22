using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteForeignKeyMatchTests
{
    private static ForeignKeyConstraint ForeignKey(string match) =>
        new("FK_Child", "Parent", ["A", "B"], "Child", ["A", "B"])
        { Match = match, OnDelete = "SET NULL", OnUpdate = "CASCADE" };

    [TestCase("FULL")]
    [TestCase("PARTIAL")]
    [TestCase("unknown")]
    public void UnsupportedMatchIsRejectedByCreationPreviewAndAddingConstraint(string match)
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Parent (A INTEGER, B INTEGER, PRIMARY KEY(A, B))");
        IDbField[] fields = [new Column("A", DbType.Int32), new Column("B", DbType.Int32), ForeignKey(match)];
        var operation = new CreateTableOperation("Child", null, fields);
        Assert.Throws<NotSupportedException>(() => operation.ToSql(new SqlGenerationContext(ProviderTypes.SQLite)));
        Assert.Throws<NotSupportedException>(() => operation.Apply(provider));
        Assert.That(provider.TableExists("Child"), Is.False);
        provider.ExecuteNonQuery("CREATE TABLE Child (A INTEGER, B INTEGER); INSERT INTO Child VALUES (1, 2)");
        Assert.Throws<NotSupportedException>(() => ((TransformationProvider)provider).AddForeignKey("Child", ForeignKey(match)));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child")), Is.EqualTo(1));
        Assert.That(provider.GetForeignKeyConstraints("Child"), Is.Empty);
    }

    [TestCase(null)]
    [TestCase("NONE")]
    [TestCase("simple")]
    public void SupportedMatchRetainsCompositeNullSemanticsAndIndependentActionsAfterRebuild(string match)
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Parent (A INTEGER, B INTEGER, PRIMARY KEY(A, B)); INSERT INTO Parent VALUES (1, 2)");
        provider.AddTable("Child", new Column("A", DbType.Int32), new Column("B", DbType.Int32), ForeignKey(match));
        provider.ChangeColumn("Child", new Column("A", DbType.Int64));
        provider.ExecuteNonQuery("INSERT INTO Child VALUES (NULL, 99), (1, 2); UPDATE Parent SET A=3 WHERE A=1");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT A FROM Child WHERE B=2")), Is.EqualTo(3));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO Child VALUES (3, 99)"));
        provider.ExecuteNonQuery("DELETE FROM Parent");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child WHERE A IS NULL")), Is.EqualTo(2));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child WHERE B IS NULL")), Is.EqualTo(1));
        if (match == "simple")
            Assert.That(provider.GetTableConstraints("Child").OfType<ForeignKeyConstraint>().Single().Match, Is.EqualTo("SIMPLE"));
    }

    [Test]
    public void LegacyUnsupportedMatchIsReportedAndRebuildLeavesOriginalSchemaAndRowsIntact()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Parent (A INTEGER, B INTEGER, PRIMARY KEY(A, B)); CREATE TABLE Child (A INTEGER, B INTEGER, FOREIGN KEY(A, B) REFERENCES Parent(A, B) MATCH FULL); INSERT INTO Child VALUES (NULL, 99)");
        Assert.That(provider.GetForeignKeyConstraints("Child").Single().Match, Is.EqualTo("FULL"));
        var original = provider.ExecuteScalar("SELECT sql FROM sqlite_master WHERE name='Child'");
        Assert.Throws<NotSupportedException>(() => provider.ChangeColumn("Child", new Column("A", DbType.Int64)));
        Assert.That(provider.ExecuteScalar("SELECT sql FROM sqlite_master WHERE name='Child'"), Is.EqualTo(original));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT B FROM Child")), Is.EqualTo(99));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("PRAGMA foreign_keys")), Is.EqualTo(1));
        Assert.That(provider.TableExists("ChildTemp"), Is.False);
    }
}
