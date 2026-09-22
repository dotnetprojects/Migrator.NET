using System.Data;

namespace DotNetProjects.Migrator.Providers.Impl.Mysql;

/// <summary>
/// MySql transformation provider
/// </summary>    
public class MariaDBTransformationProvider : MySqlTransformationProvider
{
    public MariaDBTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, scope, providerName)
    {
    }

    public MariaDBTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, scope, providerName)
    {
    }
}
