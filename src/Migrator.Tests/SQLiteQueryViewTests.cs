using System;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteQueryViewTests
{
    [Test]
    public void PreviewUsesTheSameMainViewAsLiveOperations()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("CREATE TABLE Source(Id INTEGER); CREATE TEMP VIEW Active AS SELECT 42 AS Id");
        var b = new DotNetProjects.Migrator.Framework.Fluent.MigrationBuilder();
        b.Create.View("Active", new SelectQuery("Source", "s", [new(Column("Id", "s"), "Id")]));
        b.Delete.View("Active");
        foreach (var sql in b.Preview(new DotNetProjects.Migrator.Framework.Fluent.SqlGenerationContext(DotNetProjects.Migrator.Providers.ProviderTypes.SQLite))) p.ExecuteNonQuery(sql);
        Assert.That(p.ViewExists("main.Active"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Id FROM temp.Active"), Is.EqualTo(42L));
    }
    [Test]
    public void MainAndTemporaryViewsUseTheSameIdentityForLookupAndDdl()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("CREATE TABLE Source (Id INTEGER); INSERT INTO Source VALUES (1); CREATE VIEW main.Active AS SELECT Id FROM Source; CREATE TEMP VIEW Active AS SELECT 42 AS Id");
        p.DropView("Active", true);
        Assert.That(p.ViewExists("main.Active"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Id FROM temp.Active"), Is.EqualTo(42L));
        Assert.Catch(() => p.ReplaceView("Active", new SelectQuery("Source", "s", [new(Column("Missing", "s"), "Id")])));
        Assert.That(p.ViewExists("main.Active"), Is.False);
        p.ReplaceView("Active", new SelectQuery("Source", "s", [new(Column("Id", "s"), "Id")]));
        Assert.That(p.ExecuteScalar("SELECT Id FROM main.Active"), Is.EqualTo(1L));
        Assert.That(p.ExecuteScalar("SELECT Id FROM temp.Active"), Is.EqualTo(42L));
    }
    [Test]
    public void InactiveProviderSkipsViewLifecycle()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        var query = new SelectQuery("Missing", "s", [new(Column("Id", "s"), "Id")]);
        p["PostgreSQL"].CreateView("Active", query); p["PostgreSQL"].ReplaceView("Active", query); p["PostgreSQL"].DropView("Active");
        var b = new DotNetProjects.Migrator.Framework.Fluent.MigrationBuilder(); b.Create.View("Active", query); b.Delete.View("Active"); b.Apply(p["PostgreSQL"]);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ViewsUseTheSameDefinitionAsLiveProviders(bool leftJoin)
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        if (leftJoin) Providers.Live.QueryViewTests.VerifyLeftJoin(p);
        else Providers.Live.QueryViewTests.VerifyFilteredView(p);
    }
    [Test]
    public void InvalidReplacementRestoresOriginalAndTransactionRemainsUsable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("CREATE TABLE Source (Id INTEGER); INSERT INTO Source VALUES (1)");
        p.BeginTransaction();
        p.ReplaceView("main.Active", new SelectQuery("Source", "s", [new(Column("Id", "s"), "Id")]));
        Assert.Catch(() => p.ReplaceView("main.Active", new SelectQuery("Source", "s", [new(Column("Missing", "s"), "Id")])));
        Assert.That(p.ExecuteScalar("SELECT Id FROM Active"), Is.EqualTo(1L));
        p.ExecuteNonQuery("INSERT INTO Source VALUES (2)");
        p.Rollback();
        Assert.That(p.ViewExists("main.Active"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Source"), Is.EqualTo(1L));
    }
}
