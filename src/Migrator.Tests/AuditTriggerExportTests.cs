using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests;

[Category("Unit")]
public class AuditTriggerExportTests
{
    private static AuditTrigger Definition(string watched) => new("audit", "main.child", TriggerEvent.Update, "Audit", Old("Id"), [], UpdateColumns: [watched]);

    [TestCase("PreviousStatus")]
    [TestCase("σ")]
    public void ExportRejectsMissingWatchedColumns(string watched)
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("Id").AsInt32().WithColumn("CurrentStatus").AsInt32().WithColumn("Σ").AsInt32();
        b.Create.AuditTrigger(Definition(watched));
        Assert.Throws<ArgumentException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
    }

    [Test]
    public void ExportNeedsMetadataForExistingTargets()
    {
        var b = new MigrationBuilder(); b.Create.AuditTrigger(Definition("CurrentStatus"));
        Assert.Throws<MigrationException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SQLite, _ => [new Column("CurrentStatus", DbType.Int32)])), Has.Count.EqualTo(1));
    }

    [TestCase("rowid")]
    [TestCase("OID")]
    [TestCase("_RoWiD_")]
    public void QueuedRowidAliasesAreValidatedButExistingImplicitAliasesNeedMoreMetadata(string watched)
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("Id").AsInt32(); b.Create.AuditTrigger(Definition(watched));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SQLite)), Has.Count.EqualTo(2));
        b = new MigrationBuilder(); b.Create.AuditTrigger(Definition(watched));
        Assert.Throws<NotSupportedException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SQLite, _ => [new Column("Id", DbType.Int32)])));
    }

    [Test, Category("SQLite")]
    public void ExportedAuditLogsOnlyWatchedDatabaseIndependentUpdates()
    {
        var b = new MigrationBuilder(); b.Create.Table("Child").WithColumn("Id").AsInt32().WithColumn("CurrentStatus").AsInt32();
        b.Create.Table("Audit").WithColumn("OccurredAt").AsString(100).WithColumn("EntityId").AsInt32().WithColumn("Operation").AsString(10).WithColumn("BeforeJson").AsString(100);
        b.Create.AuditTrigger(Definition("currentstatus"));
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        foreach (var sql in b.Preview(new SqlGenerationContext(ProviderTypes.SQLite))) p.ExecuteNonQuery(sql);
        p.Insert("Child", ["Id", "CurrentStatus"], [1, 0]);
        p.Update("Child", ["Id"], [2], ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(0L));
        p.Update("Child", ["CurrentStatus"], [1], ["Id"], [2]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(1L));
    }
}
