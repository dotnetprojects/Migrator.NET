using System;
using System.Collections.Generic;
using System.Data;

namespace DotNetProjects.Migrator.Providers.Impl.Informix;

/// <summary>
/// DB2 transformation provider
/// </summary>
public class InformixTransformationProvider : TransformationProvider
{
    public InformixTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "IBM.Data.Informix.Client";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection();
        _connection.ConnectionString = _connectionString;
        this._connection.Open();
    }

    public InformixTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, null, scope)
    {
    }

    public override List<string> GetDatabases()
    {
        throw new NotImplementedException();
    }

    public override bool ConstraintExists(string table, string name)
    {
        throw new NotImplementedException();
    }

    public override bool IndexExists(string table, string name)
    {
        throw new NotImplementedException();
    }
}
