using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.DB2;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using DotNetProjects.Migrator.Providers.Impl.Informix;
using DotNetProjects.Migrator.Providers.Impl.Ingres;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Sybase;

namespace DotNetProjects.Migrator;

/// <summary>
/// Handles loading Provider implementations
/// </summary>
public class ProviderFactory
{
    static ProviderFactory()
    { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="providerType"></param>
    /// <param name="connectionString"></param>
    /// <param name="defaultSchema"></param>
    /// <param name="scope"></param>
    /// <param name="providerName">for Example: System.Data.SqlClient</param>
    /// <returns></returns>
    public static ITransformationProvider Create(ProviderTypes providerType, string connectionString, string defaultSchema, string scope = "default", string providerName = "")
    {
        var dialectInstance = DialectForProvider(providerType);

        return dialectInstance.NewProviderForDialect(connectionString, defaultSchema, scope, providerName);
    }

    public static ITransformationProvider Create(ProviderTypes providerType, IDbConnection connection, string defaultSchema, string scope = "default", string providerName = "")
    {
        var dialectInstance = DialectForProvider(providerType);

        return dialectInstance.NewProviderForDialect(connection, defaultSchema, scope, providerName);
    }

    public static Dialect DialectForProvider(ProviderTypes providerType)
    {
        switch (providerType)
        {
            case ProviderTypes.SQLite:
                return (Dialect)Activator.CreateInstance(typeof(SQLiteDialect));
            case ProviderTypes.MonoSQLite:
                return (Dialect)Activator.CreateInstance(typeof(SQLiteMonoDialect));
            case ProviderTypes.Mysql:
                return (Dialect)Activator.CreateInstance(typeof(MysqlDialect));
            case ProviderTypes.MariaDB:
                return (Dialect)Activator.CreateInstance(typeof(MariaDBDialect));
            case ProviderTypes.Oracle:
                return (Dialect)Activator.CreateInstance(typeof(OracleDialect));
            case ProviderTypes.PostgreSQL:
                return (Dialect)Activator.CreateInstance(typeof(PostgreSQLDialect));
            case ProviderTypes.PostgreSQL82:
                return (Dialect)Activator.CreateInstance(typeof(PostgreSQL82Dialect));
            case ProviderTypes.SqlServer:
                return (Dialect)Activator.CreateInstance(typeof(SqlServerDialect));
            case ProviderTypes.SqlServer2005:
                return (Dialect)Activator.CreateInstance(typeof(SqlServer2005Dialect));
            case ProviderTypes.MsOracle:
                return (Dialect)Activator.CreateInstance(typeof(MsOracleDialect));
            case ProviderTypes.IBM_DB2:
                return (Dialect)Activator.CreateInstance(typeof(DB2Dialect));
            case ProviderTypes.IBM_Informix:
                return (Dialect)Activator.CreateInstance(typeof(InformixDialect));
            case ProviderTypes.Firebird:
                return (Dialect)Activator.CreateInstance(typeof(FirebirdDialect));
            case ProviderTypes.Ingres:
                return (Dialect)Activator.CreateInstance(typeof(IngresDialect));
            case ProviderTypes.Sybase:
                return (Dialect)Activator.CreateInstance(typeof(SybaseDialect));
        }

        return null;
    }
}
