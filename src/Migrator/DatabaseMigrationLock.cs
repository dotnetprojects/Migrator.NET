using System;
using System.Buffers.Binary;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
namespace DotNetProjects.Migrator;

/// <summary>Session-owned database locks for SQL Server, PostgreSQL and MySQL/MariaDB.</summary>
public sealed class DatabaseMigrationLock : IMigrationLock
{
    public IDisposable Acquire(ITransformationProvider provider, string scope, TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var kind = provider.Dialect switch
        {
            SqlServerDialect => 0, PostgreSQLDialect => 1, MysqlDialect => 2,
            _ => throw new UnsupportedMigrationFeatureException("Database migration locking is supported on SQL Server, PostgreSQL and MySQL/MariaDB.")
        };
        var connection = provider.Connection;
        if (connection.State != ConnectionState.Open) throw new MigrationException("Migration locking requires an open connection.");
        var resource = "Migrator.NET:" + connection.Database + ":" + provider.SchemaInfoTable + ":" + scope;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resource));
        object key = kind == 1 ? BinaryPrimitives.ReadInt64BigEndian(hash) : Convert.ToHexString(hash);
        var acquire = kind switch
        {
            0 => "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@key, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @result",
            1 => "SELECT pg_try_advisory_lock(@key)",
            _ => "SELECT GET_LOCK(@key, 0)"
        };
        var release = kind switch
        {
            0 => "DECLARE @result int; EXEC @result=sys.sp_releaseapplock @Resource=@key, @LockOwner='Session'; SELECT @result",
            1 => "SELECT pg_advisory_unlock(@key)",
            _ => "SELECT RELEASE_LOCK(@key)"
        };
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var value = Scalar(connection, acquire, key);
            if (value == null || value == DBNull.Value) throw new MigrationException("Database lock acquisition returned no result.");
            var code = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (kind == 0 ? code >= 0 : code == 1) return new Lease(connection, release, key, kind);
            if (kind == 0 && code != -1) throw new MigrationException("Database lock acquisition failed with code " + code);
            if (watch.Elapsed >= timeout) throw new TimeoutException("Timed out acquiring the migration lock.");
            Thread.Sleep((int)Math.Min(50, Math.Max(1, (timeout - watch.Elapsed).TotalMilliseconds)));
        }
    }
    private static object Scalar(IDbConnection connection, string sql, object key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql; command.CommandTimeout = 30;
        var parameter = command.CreateParameter(); parameter.ParameterName = "@key"; parameter.Value = key;
        parameter.DbType = key is long ? DbType.Int64 : DbType.String;
        command.Parameters.Add(parameter);
        return command.ExecuteScalar();
    }
    private sealed class Lease(IDbConnection connection, string release, object key, int kind) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            var value = Scalar(connection, release, key);
            if (value == null || value == DBNull.Value || (kind == 0 ? Convert.ToInt32(value) < 0 : Convert.ToInt32(value) != 1))
                throw new MigrationException("The database did not confirm migration lock release.");
            disposed = true;
        }
    }
}
