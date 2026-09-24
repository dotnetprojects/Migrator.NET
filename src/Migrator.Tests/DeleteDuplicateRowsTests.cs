using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
[TestFixture(false)]
[TestFixture(true)]
public class DeleteDuplicateRowsTests(bool systemData)
{
    private IDbConnection connection;
    private ITransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = systemData ? new System.Data.SQLite.SQLiteConnection("Data Source=:memory:") : new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("""
            CREATE TABLE "Duplicate Rows" ("Key One" TEXT, "select" INTEGER, Payload TEXT);
            CREATE INDEX IX_Duplicates ON "Duplicate Rows"("Key One");
            INSERT INTO "Duplicate Rows" VALUES ('a',1,'one'),('a',1,'two'),('a',1,'three'),('a',2,'different'),
                (NULL,1,'n1'),(NULL,1,'n2'),(NULL,NULL,'n3'),(NULL,NULL,'n4'),('z',3,'unique');
            """);
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [TestCase(DuplicateNullHandling.Equal, 4)]
    [TestCase(DuplicateNullHandling.ExcludeNullKeys, 2)]
    public void DeletesOnlyDuplicatesAndReportsAffectedRows(DuplicateNullHandling nulls, int expected)
    {
        int removed = provider.DeleteDuplicateRows("Duplicate Rows", ["Key One", "select"], DuplicateRowRetention.Any, nulls);
        Assert.That(removed, Is.EqualTo(expected));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Duplicate Rows\""), Is.EqualTo(9 - expected));
        Assert.That(provider.ExecuteScalar("SELECT Payload FROM \"Duplicate Rows\" WHERE \"select\"=3"), Is.EqualTo("unique"));
        Assert.That(provider.ExecuteScalar("SELECT Payload FROM \"Duplicate Rows\" WHERE \"select\"=2"), Is.EqualTo("different"));
        Assert.That(provider.GetIndexes("Duplicate Rows").Any(index => index.Name == "IX_Duplicates"), Is.True);
        Assert.That(provider.DeleteDuplicateRows("Duplicate Rows", ["Key One", "select"], DuplicateRowRetention.Any, nulls), Is.Zero);
    }

    [Test]
    public void InvalidKeysLeaveRowsUntouched()
    {
        foreach (var keys in new[] { Array.Empty<string>(), new[] { "missing" }, new[] { "select", "SELECT" } })
            Assert.Catch(() => provider.DeleteDuplicateRows("Duplicate Rows", keys, DuplicateRowRetention.Any));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Duplicate Rows\""), Is.EqualTo(9));
    }

    [Test]
    public void RowIdShadowingUsesAnUnshadowedPhysicalIdentifier()
    {
        provider.ExecuteNonQuery("CREATE TABLE Shadow (rowid TEXT, K INTEGER); INSERT INTO Shadow VALUES ('same',1),('same',1)");
        Assert.That(provider.DeleteDuplicateRows("Shadow", ["K"], DuplicateRowRetention.Any), Is.EqualTo(1));
    }

    [TestCase("CREATE TABLE Unsupported (K INTEGER, Id INTEGER PRIMARY KEY) WITHOUT ROWID")]
    [TestCase("CREATE TABLE Unsupported (K INTEGER, Id INTEGER, rowid TEXT, _rowid_ TEXT, oid TEXT)")]
    public void UnsupportedRowIdentityIsRejectedWithoutDeleting(string create)
    {
        provider.ExecuteNonQuery(create);
        provider.ExecuteNonQuery("INSERT INTO Unsupported (K, Id) VALUES (1,1),(1,2)");
        Assert.Throws<NotSupportedException>(() => provider.DeleteDuplicateRows("Unsupported", ["K"], DuplicateRowRetention.Any));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Unsupported"), Is.EqualTo(2));
    }

    [TestCase(DuplicateNullHandling.Equal, 5)]
    [TestCase(DuplicateNullHandling.ExcludeNullKeys, 7)]
    public void FluentOperationSnapshotsKeysAndMatchesImperativeBehavior(DuplicateNullHandling nulls, int remaining)
    {
        var builder = new MigrationBuilder();
        string[] keys = ["Key One", "select"];
        builder.Delete.DuplicateRows().FromTable("Duplicate Rows").ByColumns(keys).KeepAny(nulls);
        keys[0] = "missing";
        builder.Apply(provider);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Duplicate Rows\""), Is.EqualTo(remaining));
        Assert.Throws<IrreversibleMigrationException>(() => builder.Build().Single().Reverse());
        Assert.Throws<NotSupportedException>(() => builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
    }

    [Test]
    public void IncompleteFluentOperationIsRejectedBeforeExecution()
    {
        var builder = new MigrationBuilder();
        builder.Delete.DuplicateRows().FromTable("Duplicate Rows").ByColumns("Key One");
        Assert.Catch(() => builder.Apply(provider));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Duplicate Rows\""), Is.EqualTo(9));
    }

    [Test]
    public void TransactionRollbackRestoresDeletedRows()
    {
        provider.BeginTransaction();
        Assert.That(provider.DeleteDuplicateRows("Duplicate Rows", ["Key One", "select"], DuplicateRowRetention.Any), Is.EqualTo(4));
        provider.Rollback();
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Duplicate Rows\""), Is.EqualTo(9));
    }
}
