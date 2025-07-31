using System.Data;

namespace Migrator.Providers.Oracle;

public class MsOracleTransformationProvider : OracleTransformationProvider
{
    public MsOracleTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope, providerName)
    {

    }

    public MsOracleTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope, providerName)
    {
    }

    protected override void CreateConnection(string providerName)
    {
        if (string.IsNullOrEmpty(providerName)) providerName = "System.Data.OracleClient";
        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection(); // new OracleConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }
}
