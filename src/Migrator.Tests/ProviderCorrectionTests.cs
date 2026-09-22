using System;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
namespace Migrator.Tests;
[Category("SQLite")]
public class ProviderCorrectionTests
{
    [Test] public void RebuildPreservesTriggerAndUpdateAction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE Child (Id INTEGER, ParentId INTEGER); CREATE TABLE Audit (Id INTEGER)");
        provider.AddForeignKey("FK_Child", "Child", new[] { "ParentId" }, "Parent", new[] { "Id" }, ForeignKeyConstraintType.Cascade, ForeignKeyConstraintType.Cascade);
        provider.ExecuteNonQuery("CREATE TRIGGER ChildAudit AFTER INSERT ON Child BEGIN INSERT INTO Audit VALUES (NEW.Id); END");
        provider.AddColumn("cHiLd", new Column("Name", DbType.String));
        provider.ExecuteNonQuery("INSERT INTO Parent VALUES (1); INSERT INTO Child (Id, ParentId) VALUES (2, 1); UPDATE Parent SET Id=3 WHERE Id=1");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT ParentId FROM Child")), Is.EqualTo(3));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM Audit")), Is.EqualTo(2));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
    }
    [TestCase("ExistingForeignKey")]
    [TestCase("ExistingUnique")]
    public void IndependentActionsRejectConstraintNameCollisions(string existingName)
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE Child (Id INTEGER, ParentId INTEGER, CONSTRAINT ExistingUnique UNIQUE(Id), CONSTRAINT ExistingForeignKey FOREIGN KEY(ParentId) REFERENCES Parent(Id))");
        Assert.Throws<MigrationException>(() => provider.AddForeignKey(existingName.ToLowerInvariant(), "Child", new[] { "ParentId" }, "Parent", new[] { "Id" }, ForeignKeyConstraintType.Cascade, ForeignKeyConstraintType.Cascade));
        Assert.That(provider.GetForeignKeyConstraints("Child").Length, Is.EqualTo(1));
    }
    [Test] public void NativeDropPreservesTriggerAndForeignKeySetting()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); connection.Open();
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER PRIMARY KEY, Obsolete TEXT); CREATE TABLE Audit (Id INTEGER); CREATE TRIGGER OriginalAudit AFTER INSERT ON Original BEGIN INSERT INTO Audit VALUES (NEW.Id); END");
        provider.RemoveColumn("Original", "Obsolete");
        provider.ExecuteNonQuery("INSERT INTO Original VALUES (7)");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM Audit")), Is.EqualTo(7));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
    }
    [Test] public void NativeDropRejectsDependentTriggerWithoutLosingData()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER, Obsolete TEXT); CREATE TABLE Audit (Value TEXT); CREATE TRIGGER OriginalAudit AFTER INSERT ON Original BEGIN INSERT INTO Audit VALUES (NEW.Obsolete); END; INSERT INTO Original VALUES (1, 'keep')");
        var error = Assert.Throws<MigrationException>(() => provider.RemoveColumn("Original", "Obsolete"));
        Assert.That(error.InnerException, Is.TypeOf<SqliteException>());
        Assert.That(provider.ExecuteScalar("SELECT Obsolete FROM Original"), Is.EqualTo("keep"));
    }
    [Test] public void UnsupportedRebuildLeavesTableIntact()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER PRIMARY KEY) WITHOUT ROWID; INSERT INTO Original VALUES (1)");
        Assert.Throws<NotSupportedException>(() => provider.AddColumn("Original", new Column("Name", DbType.String)));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM Original")), Is.EqualTo(1));
    }
}
