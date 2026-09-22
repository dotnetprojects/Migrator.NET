using System;
using System.Collections.Generic;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Loggers;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
namespace Migrator.Tests;

public class RunnerSafetyTests
{
    [Migration(1, Ignore = true)] public class One : Migration
    {
        public override void Up() => Database.ExecuteNonQuery("CREATE TABLE Example (Id INTEGER)");
        public override void Down() => Database.ExecuteNonQuery("DROP TABLE Example");
    }
    [Migration(6, Ignore = true)] public class Six : One { }
    [Migration(1, Scope = "other", Ignore = true)] public class Other : One { }
    [Migration(2, Ignore = true)] public class Failure : Migration
    {
        public override void Up() => throw new InvalidOperationException("original");
        public override void Down() => throw new InvalidOperationException("original");
    }
    [Test] public void CustomProvidersRetainExplicitMigrationScope()
    {
        var provider = Substitute.For<ITransformationProvider>();
        provider.AppliedMigrations.Returns(new List<long>());
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(Other));
        Assert.That(runner.AssemblyLastMigrationVersion, Is.EqualTo(1));
        runner.MigrateToLastVersion();
        provider.Received().MigrationApplied(1, "other");
    }
    [Test] public void FailedCommitRetainsTransactionForRollback()
    {
        var connection = Substitute.For<System.Data.IDbConnection>();
        var transaction = Substitute.For<System.Data.IDbTransaction>();
        connection.State.Returns(System.Data.ConnectionState.Open);
        connection.BeginTransaction(System.Data.IsolationLevel.Serializable).Returns(transaction);
        transaction.When(t => t.Commit()).Do(_ => throw new InvalidOperationException("commit"));
        using var provider = new TransactionTestProvider(connection);
        provider.BeginTransaction();
        Assert.Throws<InvalidOperationException>(() => provider.Commit());
        Assert.That(provider.HasActiveTransaction, Is.True);
        provider.Rollback();
        transaction.Received(1).Rollback();
        transaction.Received(1).Dispose();
        Assert.That(provider.HasActiveTransaction, Is.False);
    }
    private sealed class TransactionTestProvider(System.Data.IDbConnection connection)
        : TransformationProvider(new DotNetProjects.Migrator.Providers.Impl.SQLite.SQLiteDialect(), connection, null, "default")
    {
        public override List<string> GetDatabases() => new();
        public override bool ConstraintExists(string table, string name) => false;
        public override bool IndexExists(string table, string name) => false;
    }
    [Test] public void LatestDoesNotDependOnRegistrationOrder()
    {
        var loader = new MigrationLoader(null, false, typeof(Six), typeof(One));
        Assert.That(loader.LastVersion, Is.EqualTo(6));
    }
    [Test] public void PlanOrdersDowngradesThenMissingUpgrades()
    {
        var steps = MigrationPlanner.Create(new long[] { 4, 1, 3, 2 }, new long[] { 4, 1, 3 }, 2);
        Assert.That(steps, Is.EqualTo(new[] { new MigrationStep(4, false), new MigrationStep(3, false), new MigrationStep(2, true) }));
    }
    [Test] public void MissingDowngradeIsRejected()
        => Assert.Throws<MigrationException>(() => MigrationPlanner.Create(new long[] { 1 }, new long[] { 2 }, 0));
    [Test] public void RollbackFailureDoesNotReplaceMigrationFailure()
    {
        var provider = Substitute.For<ITransformationProvider>();
        provider.AppliedMigrations.Returns(new List<long>());
        provider.When(p => p.Rollback()).Do(_ => throw new Exception("rollback"));
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(Failure));
        var error = Assert.Throws<InvalidOperationException>(() => runner.MigrateTo(2));
        Assert.That(error.Message, Is.EqualTo("original"));
        Assert.That(error.Data["RollbackException"], Is.TypeOf<Exception>());
        provider.Received(1).Rollback();
        provider.DidNotReceive().Commit();
    }
    [Test, Category("SQLite")] public void DryRunDoesNotCreateHistoryAndScopeIsolated()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(Other), typeof(One)) { DryRun = true };
        runner.MigrateToLastVersion();
        Assert.That(provider.TableExists("SchemaInfo"), Is.False);
        Assert.That(provider.TableExists("Example"), Is.False);
        Assert.That(((TransformationProvider)provider).HasActiveTransaction, Is.False);
        runner.DryRun = false;
        runner.MigrateToLastVersion();
        Assert.That(provider.AppliedMigrations, Is.EqualTo(new long[] { 1 }));
        runner.MigrateTo(0);
        Assert.That(provider.TableExists("Example"), Is.False);
        Assert.That(provider.AppliedMigrations, Is.Empty);
    }
    [Test, Category("SQLite")] public void ForeignKeysAreRestoredAfterFailure()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(Failure));
        Assert.Throws<InvalidOperationException>(() => runner.MigrateTo(2));
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("PRAGMA foreign_keys")), Is.EqualTo(1));
        Assert.That(((TransformationProvider)provider).HasActiveTransaction, Is.False);
        Assert.That(provider.AppliedMigrations, Is.Empty);
    }
}
