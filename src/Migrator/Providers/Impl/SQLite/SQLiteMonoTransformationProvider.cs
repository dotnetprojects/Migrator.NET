using System.Data;
using Migrator.Providers;
using Migrator.Providers.SQLite;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

/// <summary>
/// Summary description for SQLiteTransformationProvider.
/// </summary>
public class SQLiteMonoTransformationProvider : SQLiteTransformationProvider
{
    public SQLiteMonoTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, scope, providerName)
    {

    }

    public SQLiteMonoTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, scope, providerName)
    {
    }

    protected override void CreateConnection(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "Mono.Data.Sqlite";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, "Mono.Data.Sqlite", "Mono.Data.Sqlite.SQLiteFactory");
        _connection = fac.CreateConnection(); // new SQLiteConnection(_connectionString);
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }
}
