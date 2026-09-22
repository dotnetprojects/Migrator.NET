using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;
using Migrator.Tests.Settings;
using NUnit.Framework;
namespace Migrator.Tests;

[TestFixture(ProviderTypes.SqlServer, Category = "SQLServer")]
[TestFixture(ProviderTypes.PostgreSQL, Category = "PostgreSQL")]
[TestFixture(ProviderTypes.Mysql, Category = "MySQL")]
[TestFixture(ProviderTypes.MariaDB, Category = "MariaDB")]
public class DatabaseLockTests(ProviderTypes type)
{
    private DbConnection Open()
    {
        DbConnection connection;
        if (type == ProviderTypes.SqlServer)
        {
            var config = new ConfigurationReader().GetDatabaseConnectionConfigById("SQLServer");
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(config.ConnectionString) { InitialCatalog = "master" };
            connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        }
        else if (type == ProviderTypes.PostgreSQL)
            connection = new Npgsql.NpgsqlConnection(new ConfigurationReader().GetDatabaseConnectionConfigById("PostgreSQL").ConnectionString);
        else
            connection = new MySql.Data.MySqlClient.MySqlConnection(Environment.GetEnvironmentVariable(type == ProviderTypes.Mysql ? "MIGRATOR_MYSQL" : "MIGRATOR_MARIADB")
                ?? "Server=127.0.0.1;Database=testdb;User ID=root;Password=rootpass;Pooling=false");
        connection.Open(); return connection;
    }
    private sealed class RunState : IDisposable
    {
        public int Calls;
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();
        public void Dispose() { Entered.Dispose(); Release.Dispose(); }
    }
    [Migration(1)]
    private sealed class CountMigration(RunState state) : Migration
    {
        public override void Up()
        {
            Interlocked.Increment(ref state.Calls);
            state.Entered.Set();
            if (!state.Release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Test migration gate timed out.");
        }
        public override void Down() { }
    }
    private sealed class SignallingLock(ManualResetEventSlim attempted) : IMigrationLock
    {
        public IDisposable Acquire(ITransformationProvider provider, string scope, TimeSpan timeout)
        { attempted.Set(); return new DatabaseMigrationLock().Acquire(provider, scope, timeout); }
    }
    [Test]
    public async Task ConcurrentRunnersReloadStaleHistoryAfterAcquiringNativeLock()
    {
        using var connection1 = Open(); using var connection2 = Open();
        using var p1 = ProviderFactory.Create(type, connection1, null);
        using var p2 = ProviderFactory.Create(type, connection2, null);
        p1.SchemaInfoTable = p2.SchemaInfoTable = "lockhistory_" + Guid.NewGuid().ToString("N")[..12];
        using var state = new RunState(); using var attempted = new ManualResetEventSlim();
        Assert.That(p2.AppliedMigrations, Is.Empty); // Deliberately seed a stale empty cache.
        var first = new DotNetProjects.Migrator.Migrator(p1, false, typeof(CountMigration));
        var second = new DotNetProjects.Migrator.Migrator(p2, false, typeof(CountMigration));
        first.Options.Activator = second.Options.Activator = _ => new CountMigration(state);
        first.Options.Lock = new DatabaseMigrationLock(); second.Options.Lock = new SignallingLock(attempted);
        first.Options.LockTimeout = second.Options.LockTimeout = TimeSpan.FromSeconds(15);
        Task one = null, two = null;
        try
        {
            one = Task.Run(first.MigrateToLastVersion);
            Assert.That(state.Entered.Wait(TimeSpan.FromSeconds(10)), Is.True);
            two = Task.Run(second.MigrateToLastVersion);
            Assert.That(attempted.Wait(TimeSpan.FromSeconds(10)), Is.True);
            state.Release.Set();
            await Task.WhenAll(one, two);
            Assert.That(state.Calls, Is.EqualTo(1));
            Assert.That(((IMigrationHistory)p2).ReadAppliedMigrations(), Is.EqualTo(new long[] { 1 }));
            using var released = new DatabaseMigrationLock().Acquire(p2, ((IMigrationHistory)p2).Scope, TimeSpan.Zero);
        }
        finally
        {
            state.Release.Set();
            try { if (one != null) await one; if (two != null) await two; }
            finally { p1.RemoveTable(p1.SchemaInfoTable); }
        }
    }

    [Test] public void IndependentSessionsContendAndCanAcquireAfterRelease()
    {
        using var connection1 = Open(); using var connection2 = Open();
        using var p1 = ProviderFactory.Create(type, connection1, null);
        using var p2 = ProviderFactory.Create(type, connection2, null);
        var migrationLock = new DatabaseMigrationLock(); var scope = Guid.NewGuid().ToString("N");
        using (migrationLock.Acquire(p1, scope, TimeSpan.FromSeconds(1)))
        {
            Assert.Throws<TimeoutException>(() => migrationLock.Acquire(p2, scope, TimeSpan.FromMilliseconds(100)));
            using var independentScope = migrationLock.Acquire(p2, scope + "other", TimeSpan.Zero);
        }
        using var acquiredAfterRelease = migrationLock.Acquire(p2, scope, TimeSpan.FromSeconds(1));
    }
}
