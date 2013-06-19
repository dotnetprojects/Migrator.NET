using Oracle.ManagedDataAccess.Client;

namespace Migrator.Providers.Oracle
{
    public class OracleManagedDriverTransformationProvider : OracleTransformationProviderBase
    {
        public OracleManagedDriverTransformationProvider(Dialect dialect, string connectionString, string defaultSchema)
            : base(dialect, connectionString, defaultSchema)
        {
            _connection = new OracleConnection();
            _connection.ConnectionString = _connectionString;
            _connection.Open();
        }
    }
}