using System;
using System.Data.Common;
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
