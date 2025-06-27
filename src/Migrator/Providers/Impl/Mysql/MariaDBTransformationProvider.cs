using System.Data;

namespace Migrator.Providers.Mysql
{
    /// <summary>
    /// MySql transformation provider
    /// </summary>    
    public class MariaDBTransformationProvider : MySqlTransformationProvider
    {
        public MariaDBTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
            : base(dialect, connectionString, scope, providerName)
        {
            if (string.IsNullOrEmpty(providerName)) providerName = "MySql.Data.MySqlClient";
            var fac = DbProviderFactoriesHelper.GetFactory(providerName, "MySql.Data", "MySql.Data.MySqlClient.MySqlClientFactory");
            _connection = fac.CreateConnection();
            _connection.ConnectionString = _connectionString;
            _connection.Open();
        }

        public MariaDBTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
           : base(dialect, connection, scope, providerName)
        {
        }
    }
}
