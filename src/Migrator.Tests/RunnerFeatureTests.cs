using System;
using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
namespace Migrator.Tests;

[Category("SQLite")]
public class RunnerFeatureTests
{
    private static readonly List<string> Events = new();
    [Migration(1), Tags("blue", "shared")]
    internal class First : Migration
    {
        public override void Up() { Events.Add("first"); Database.AddTable("First", new Column("Id", DbType.Int32)); }
        public override void Down() => Database.RemoveTable("First");
        public override void AfterUp() => Events.Add("committed");
    }
    [Migration(2), Tags("red", "shared")]
    internal class Second : Migration
    {
        public override void Up() { Events.Add("second"); Database.AddTable("Second", new Column("Id", DbType.Int32)); }
        public override void Down() => Database.RemoveTable("Second");
    }
    [Migration(3)] internal class Failure : Migration
    {
        public override void Up() => throw new InvalidOperationException("migration failed");
        public override void Down() => throw new NotSupportedException();
    }
    [Profile("seed")] internal class Seed : Migration
    {
        public override void Up() { Events.Add("profile"); Database.Insert("First", new[] { "Id" }, new object[] { 7 }); }
        public override void Down() => throw new NotSupportedException();
    }
    [Maintenance(MaintenanceStage.BeforeRun)] internal class Before : Migration
    {
        public override void Up() => Events.Add("before");
        public override void Down() => throw new NotSupportedException();
    }
    [Maintenance(MaintenanceStage.AfterRun)] internal class After : Migration
    {
        public override void Up() => Events.Add("after");
        public override void Down() => throw new NotSupportedException();
    }
    [SetUp] public void Reset() => Events.Clear();
    private static ITransformationProvider Provider()
    {
        // Provider owns this connection, so disposal also closes the in-memory database.
        return ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
    }
    [Test] public void ProfilesAndMaintenanceHaveDeterministicOrderAndNoHistory()
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(After), typeof(Seed), typeof(First), typeof(Before));
        runner.Options.Profiles.Add("seed");
        runner.MigrateToLastVersion();
        Assert.That(Events, Is.EqualTo(new[] { "before", "first", "committed", "profile", "after" }));
        Assert.That(p.AppliedMigrations, Is.EqualTo(new long[] { 1 }));
        Assert.That(Convert.ToInt64(p.ExecuteScalar("SELECT Id FROM First")), Is.EqualTo(7));
    }
    [TestCase(TagMatchMode.Any, 2)]
    [TestCase(TagMatchMode.All, 1)]
    public void TagsUseExplicitAnyOrAll(TagMatchMode mode, int expected)
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(Second), typeof(First));
        runner.Options.TagMatch = mode;
        runner.Options.Tags.Add("blue"); runner.Options.Tags.Add("shared");
        runner.MigrateTo(2);
        Assert.That(p.AppliedMigrations.Count, Is.EqualTo(expected));
    }
    [TestCase(MigrationTransactionMode.WholeSession, false)]
    [TestCase(MigrationTransactionMode.PerMigration, true)]
    [TestCase(MigrationTransactionMode.None, true)]
    public void TransactionModeDefinesFailureBoundary(MigrationTransactionMode mode, bool firstRemains)
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(First), typeof(Failure));
        runner.Options.TransactionMode = mode;
        Assert.Throws<InvalidOperationException>(() => runner.MigrateToLastVersion());
        Assert.That(p.TableExists("First"), Is.EqualTo(firstRemains));
        Assert.That(p.AppliedMigrations.Contains(1), Is.EqualTo(firstRemains));
        Assert.That(Events.Contains("committed"), Is.EqualTo(firstRemains));
    }
    [Test] public void SessionCallbacksRunAfterAllMigrationsAndCommit()
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(First), typeof(Second));
        runner.Options.TransactionMode = MigrationTransactionMode.WholeSession;
        runner.MigrateToLastVersion();
        Assert.That(Events, Is.EqualTo(new[] { "first", "second", "committed" }));
    }
    [Test] public void LockPrecedesHistoryAndReleasesOnFailure()
    {
        using var p = Provider(); var migrationLock = new ProbeLock();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(Failure)); runner.Options.Lock = migrationLock;
        Assert.Throws<InvalidOperationException>(() => runner.MigrateToLastVersion());
        Assert.That(migrationLock.Disposed, Is.True);
    }
    [Test] public void LegacyPreviewRequiresOptInAndNeverCreatesHistory()
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(First));
        Assert.Catch<NotSupportedException>(() => runner.PreviewSql(1, ProviderTypes.SQLite));
        Assert.That(Events, Is.Empty);
        var sql = runner.PreviewSql(1, ProviderTypes.SQLite, allowLegacyBodies: true);
        Assert.That(sql, Does.Contain("CREATE TABLE"));
        Assert.That(p.TableExists("First"), Is.False);
        Assert.That(p.TableExists(p.SchemaInfoTable), Is.False);
        Assert.That(Events, Is.EqualTo(new[] { "first" })); // Opt-in still executes arbitrary C#.
    }
    [Migration(1)] internal class DirectConnection : Migration
    {
        public override void Up() => _ = Database.Connection;
        public override void Down() => throw new NotSupportedException();
    }
    [Test] public void LegacyPreviewRejectsDirectConnectionsAndUnsupportedLocks()
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(DirectConnection));
        Assert.Catch<NotSupportedException>(() => runner.PreviewSql(1, ProviderTypes.SQLite, true));
        runner.Options.Lock = new DatabaseMigrationLock();
        Assert.Catch<NotSupportedException>(() => runner.MigrateTo(1));
        Assert.That(p.TableExists(p.SchemaInfoTable), Is.False);
    }
    [Migration(4)] internal class RequiresInitialization : First
    {
        public override void InitializeOnce(string[] args) => throw new Exception("must not execute");
    }
    [Test] public void PreviewRejectsInitializationDependentMigrationsBeforeBody()
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(RequiresInitialization));
        Assert.Throws<UnsupportedMigrationFeatureException>(() => runner.PreviewSql(4, ProviderTypes.SQLite, true));
        Assert.That(Events, Is.Empty);
        Assert.That(p.TableExists(p.SchemaInfoTable), Is.False);
    }
    [Test] public void LifecycleLogArgumentsRemainInitialHistorySnapshots()
    {
        using var p = Provider();
        var logger = NSubstitute.Substitute.For<DotNetProjects.Migrator.Framework.ILogger>();
        List<long> started = null, finished = null;
        logger.Started(NSubstitute.Arg.Do<List<long>>(h => started = h), NSubstitute.Arg.Any<long>());
        logger.Finished(NSubstitute.Arg.Do<List<long>>(h => finished = h), NSubstitute.Arg.Any<long>());
        var runner = new DotNetProjects.Migrator.Migrator(p, false, logger, typeof(First));
        runner.MigrateTo(1);
        Assert.That(started, Is.Empty); Assert.That(finished, Is.Empty);
        Assert.That(p.AppliedMigrations, Is.EqualTo(new long[] { 1 }));
    }
    [Test] public void LockReleaseFailureDoesNotMaskMigrationFailure()
    {
        using var p = Provider();
        var runner = new DotNetProjects.Migrator.Migrator(p, false, typeof(Failure));
        runner.Options.Lock = new FailingReleaseLock();
        var error = Assert.Throws<InvalidOperationException>(() => runner.MigrateToLastVersion());
        Assert.That(error.Message, Is.EqualTo("migration failed"));
        Assert.That(error.Data["LockReleaseException"], Is.TypeOf<ApplicationException>());
    }
    private sealed class FailingReleaseLock : IMigrationLock, IDisposable
    {
        public IDisposable Acquire(ITransformationProvider p, string scope, TimeSpan timeout) => this;
        public void Dispose() => throw new ApplicationException("release failed");
    }
    private sealed class ProbeLock : IMigrationLock, IDisposable
    {
        public bool Disposed { get; private set; }
        public IDisposable Acquire(ITransformationProvider p, string scope, TimeSpan timeout)
        { Assert.That(p.TableExists(p.SchemaInfoTable), Is.False); return this; }
        public void Dispose() => Disposed = true;
    }
}
