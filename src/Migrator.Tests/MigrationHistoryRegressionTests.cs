using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class MigrationHistoryRegressionTests
{
    [TestCase(MigrationTransactionMode.PerMigration)]
    [TestCase(MigrationTransactionMode.WholeSession)]
    [TestCase(MigrationTransactionMode.None)]
    public void ConsolidatedBaselineSkipsVersionsItMarksApplied(MigrationTransactionMode mode)
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null, "history-test");
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(Baseline), typeof(Obsolete), typeof(Next));
        runner.Options.TransactionMode = mode;

        runner.MigrateToLastVersion();

        Assert.That(provider.AppliedMigrations.OrderBy(v => v), Is.EqualTo(new long[] { 1, 2, 3 }));
        Assert.That(provider.ColumnExists("CurrentSchema", "NewColumn"), Is.True);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM CurrentSchema"), Is.EqualTo(1));
        runner.MigrateToLastVersion();
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM CurrentSchema"), Is.EqualTo(1));
    }

    [Test]
    public void HistoryWrittenForAnotherScopeDoesNotSkipTheCurrentMigration()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null, "history-test");
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(OtherScopeBaseline), typeof(Obsolete));
        Assert.That(Assert.Throws<InvalidOperationException>(runner.MigrateToLastVersion).Message, Is.EqualTo("Obsolete migration executed."));
        Assert.That(provider.AppliedMigrations, Is.EqualTo(new long[] { 1 }));
        Assert.That(provider.IsMigrationApplied(2, "other"), Is.True);
    }

    [TestCase(MigrationTransactionMode.PerMigration)]
    [TestCase(MigrationTransactionMode.WholeSession)]
    public void FailedBaselineRollsBackItsSchemaAndHistory(MigrationTransactionMode mode)
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null, "history-test");
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(FailedBaseline), typeof(Obsolete));
        runner.Options.TransactionMode = mode;
        Assert.That(Assert.Throws<InvalidOperationException>(runner.MigrateToLastVersion).Message, Is.EqualTo("Baseline failed."));
        Assert.That(provider.AppliedMigrations, Is.Empty);
        Assert.That(provider.TableExists("CurrentSchema"), Is.False);
    }

    [TestCase(MigrationTransactionMode.PerMigration)]
    [TestCase(MigrationTransactionMode.WholeSession)]
    public void DowngradeSkipsVersionsAlreadyRemovedByAnEarlierStep(MigrationTransactionMode mode)
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null, "history-test");
        provider.MigrationApplied(1, "history-test");
        provider.MigrationApplied(2, "history-test");
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(ObsoleteDown), typeof(ConsolidatedDown));
        runner.Options.TransactionMode = mode;
        runner.RollbackTo(0);
        Assert.That(provider.AppliedMigrations, Is.Empty);
    }

    [Test]
    public void BaselineMayIncludeItselfInItsHistoryRange()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null, "history-test");
        new DotNetProjects.Migrator.Migrator(provider, false, typeof(SelfRecordingBaseline), typeof(Obsolete)).MigrateToLastVersion();
        Assert.That(provider.AppliedMigrations.OrderBy(v => v), Is.EqualTo(new long[] { 1, 2 }));
    }

    [Migration(1)]
    internal class Baseline : Migration
    {
        public override void Up()
        {
            Database.AddTable("CurrentSchema", new Column("Id", DbType.Int32));
            Database.Insert("CurrentSchema", ["Id"], [1]);
            Database.MigrationApplied(2, "history-test");
        }
        public override void Down() => Database.RemoveTable("CurrentSchema");
    }

    [Migration(1)]
    internal class FailedBaseline : Baseline
    {
        public override void Up() { base.Up(); throw new InvalidOperationException("Baseline failed."); }
    }

    [Migration(1)]
    internal class OtherScopeBaseline : Migration
    {
        public override void Up() => Database.MigrationApplied(2, "other");
        public override void Down() { }
    }

    [Migration(1)]
    internal class SelfRecordingBaseline : Baseline
    {
        public override void Up() { base.Up(); Database.MigrationApplied(1, "history-test"); }
    }

    [Migration(2)]
    internal class Obsolete : Migration
    {
        public override void Up() => throw new InvalidOperationException("Obsolete migration executed.");
        public override void AfterUp() => throw new InvalidOperationException("Obsolete callback executed.");
        public override void Down() { }
    }

    [Migration(3)]
    internal class Next : Migration
    {
        public override void Up() => Database.AddColumn("CurrentSchema", new Column("NewColumn", DbType.Int32));
        public override void Down() => Database.RemoveColumn("CurrentSchema", "NewColumn");
    }

    [Migration(1)]
    internal class ObsoleteDown : Migration
    {
        public override void Up() { }
        public override void Down() => throw new InvalidOperationException("Already reverted migration executed.");
        public override void AfterDown() => throw new InvalidOperationException("Already reverted callback executed.");
    }

    [Migration(2)]
    internal class ConsolidatedDown : Migration
    {
        public override void Up() { }
        public override void Down() => Database.MigrationUnApplied(1, "history-test");
    }
}
