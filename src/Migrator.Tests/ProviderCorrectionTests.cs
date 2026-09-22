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
        provider.AddColumn("Child", new Column("Name", DbType.String));
        provider.ExecuteNonQuery("INSERT INTO Parent VALUES (1); INSERT INTO Child (Id, ParentId) VALUES (2, 1); UPDATE Parent SET Id=3 WHERE Id=1");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT ParentId FROM Child")), Is.EqualTo(3));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM Audit")), Is.EqualTo(2));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
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
