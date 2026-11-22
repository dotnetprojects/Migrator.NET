using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

// Keep the provider independent of either optional SQLite ADO.NET package.
internal sealed class SQLiteDoubleQuotedStrings : IDisposable
{
    private readonly Action<bool> configure;
    private SQLiteDoubleQuotedStrings(Action<bool> configure) => this.configure = configure;

    internal static IDisposable Disable(ITransformationProvider provider, IDbConnection connection)
    {
        using (var probe = provider.CreateCommand())
        {
            probe.CommandText = "SELECT \"migrator_dqs_" + Guid.NewGuid().ToString("N") + "\"";
            try { probe.ExecuteScalar(); }
            catch (DbException ex) when (ex.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase))
            { return new SQLiteDoubleQuotedStrings(null); }
        }

        Action<bool> configure = null;
        var type = connection.GetType();
        var method = type.GetMethods().FirstOrDefault(m => m.Name == "SetConfigurationOption" &&
            m.GetParameters() is var args && args.Length == 2 && args[0].ParameterType.IsEnum &&
            Enum.IsDefined(args[0].ParameterType, "SQLITE_DBCONFIG_DQS_DML"));
        if (method != null)
        {
            var option = Enum.Parse(method.GetParameters()[0].ParameterType, "SQLITE_DBCONFIG_DQS_DML");
            configure = enabled => Invoke(method, connection, [option, enabled]);
        }
        else
        {
            var property = type.GetProperty("Handle");
            var raw = property?.PropertyType.Assembly.GetType("SQLitePCL.raw");
            var dbConfig = raw?.GetMethod("sqlite3_db_config", [property.PropertyType, typeof(int), typeof(int), typeof(int).MakeByRefType()]);
            if (dbConfig != null)
            {
                var handle = property.GetValue(connection);
                configure = enabled =>
                {
                    // SQLITE_DBCONFIG_DQS_DML controls SELECT preparation,
                    // including the SELECT bodies of persistent and TEMP views.
                    var args = new object[] { handle, 1013, enabled ? 1 : 0, 0 };
                    if (Convert.ToInt32(Invoke(dbConfig, null, args)) != 0)
                        throw new NotSupportedException("This SQLite library cannot disable double-quoted string fallback for dependency validation.");
                };
            }
        }
        if (configure == null)
            throw new NotSupportedException("Schema-changing SQLite rebuilds with dependent views or triggers require double-quoted string validation. Disable DQS_DML on the connection or use Microsoft.Data.Sqlite/System.Data.SQLite.");
        configure(false);
        return new SQLiteDoubleQuotedStrings(configure);
    }

    private static object Invoke(MethodInfo method, object instance, object[] args)
    {
        try { return method.Invoke(instance, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }

    public void Dispose() => configure?.Invoke(true);
}
