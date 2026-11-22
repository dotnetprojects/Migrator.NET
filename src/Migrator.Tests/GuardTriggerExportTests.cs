using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("Unit")]
public class GuardTriggerExportTests
{
    [TestCase("CurrentStatus", "PreviousStatus")]
    [TestCase("Σ", "σ")]
    public void ExportRejectsUnknownQueuedColumns(string actual, string watched)
    {
        var b = new MigrationBuilder();
        b.Create.Table("Child").WithColumn(actual).AsInt32();
        b.Create.GuardTrigger(new("guard", "main.child", TriggerEvent.Update, "Denied", UpdateColumns: [watched]));
        Assert.Throws<ArgumentException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
    }

    [TestCase("CurrentStatus")]
    [TestCase("rowid")]
    public void ExportReportsWhenExistingColumnMetadataIsUnavailable(string watched)
    {
        var b = new MigrationBuilder();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Update, "Denied", UpdateColumns: [watched]));
        Assert.That(Assert.Throws<MigrationException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite))).Message, Does.Contain("definition"));
    }

    [Test]
    public void ExistingDefinitionsValidateOrdinaryColumnsButCannotProveImplicitRowids()
    {
        var b = new MigrationBuilder();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Update, "Denied", UpdateColumns: ["currentstatus"]));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SQLite, _ => [new Column("CurrentStatus", DbType.Int32)]))[0], Does.Contain("CREATE TRIGGER"));
        Assert.Throws<ArgumentException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite, _ => [new Column("PreviousStatus", DbType.Int32)])));
        b = new MigrationBuilder();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Update, "Denied", UpdateColumns: ["rowid"]));
        Assert.Throws<NotSupportedException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite, _ => [new Column("Id", DbType.Int32)])));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SQLite, _ => [new Column("rowid", DbType.Int32)]))[0], Does.Contain("CREATE TRIGGER"));
    }

    [TestCase("rowid")]
    [TestCase("OID")]
    [TestCase("_RoWiD_")]
    public void QueuedRowidTablesCanExportImplicitAliases(string watched)
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("Id").AsInt32();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Update, "Denied", UpdateColumns: [watched]));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)), Has.Count.EqualTo(2));
    }

    [Test, Category("SQLite")]
    public void ExportTracksRenamedTablesAndColumns()
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("CurrentStatus").AsInt32();
        b.Rename.Column("CurrentStatus").OnTable("Child").To("FinalStatus");
        b.Rename.Table("Child").To("Renamed");
        b.Create.GuardTrigger(new("guard", "main.renamed", TriggerEvent.Update, "Renamed guard", UpdateColumns: ["finalstatus", "rowid"]));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)), Has.Count.EqualTo(4));
    }

    [Test]
    public void ExportRejectsRemovedTargetsAndSchemaInvalidatedByRawSql()
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("Status").AsInt32(); b.Delete.Table("Child");
        b.Create.GuardTrigger(new("guard", "main.child", TriggerEvent.Update, "Denied", UpdateColumns: ["Status"]));
        Assert.Throws<MigrationException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
        b = new MigrationBuilder(); b.Execute.Sql("CREATE TABLE Child(Status INTEGER)");
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Update, "Denied", UpdateColumns: ["Status"]));
        Assert.Throws<NotSupportedException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
    }

    [Test, Category("SQLite")]
    public void ExportedGuardActuallyProtectsDatabaseIndependentUpdates()
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("Id").AsInt32().WithColumn("CurrentStatus").AsInt32();
        b.Create.GuardTrigger(new("guard", "main.child", TriggerEvent.Update, "Exported guard", UpdateColumns: ["currentstatus"]));
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        foreach (var sql in b.Preview(new SqlGenerationContext(ProviderTypes.SQLite))) p.ExecuteNonQuery(sql);
        p.Insert("Child", ["Id", "CurrentStatus"], [1, 0]);
        Assert.That(p.Update("Child", ["Id"], [2], ["Id"], [1]), Is.EqualTo(1));
        Assert.That(Assert.Catch(() => p.Update("Child", ["CurrentStatus"], [1], ["Id"], [2])).ToString(), Does.Contain("Exported guard"));
    }
}
