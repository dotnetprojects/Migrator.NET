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
            case ProviderTypes.Hana:
                return new DotNetProjects.Migrator.Providers.Impl.Hana.HanaDialect();
            case ProviderTypes.SQLite:
                return new SQLiteDialect();
            case ProviderTypes.MonoSQLite:
                return new SQLiteMonoDialect();
            case ProviderTypes.Mysql:
                return new MysqlDialect();
            case ProviderTypes.MariaDB:
                return new MariaDBDialect();
            case ProviderTypes.Oracle:
                return new OracleDialect();
            case ProviderTypes.PostgreSQL:
                return new PostgreSQLDialect();
            case ProviderTypes.PostgreSQL82:
                return new PostgreSQL82Dialect();
            case ProviderTypes.SqlServer:
                return new SqlServerDialect();
            case ProviderTypes.SqlServer2005:
                return new SqlServer2005Dialect();
            case ProviderTypes.MsOracle:
                return new MsOracleDialect();
            case ProviderTypes.IBM_DB2:
                return new DB2Dialect();
            case ProviderTypes.IBM_Informix:
                return new InformixDialect();
            case ProviderTypes.Firebird:
                return new FirebirdDialect();
            case ProviderTypes.Ingres:
                return new IngresDialect();
            case ProviderTypes.Sybase:
                return new SybaseDialect();
        }

        return null;
    }
}
