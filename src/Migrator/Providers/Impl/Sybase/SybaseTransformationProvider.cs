using System;
using System.Collections.Generic;
using System.Data;

namespace Migrator.Providers.Impl.Sybase
{
    public class SybaseTransformationProvider : TransformationProvider
    {
        public SybaseTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
            : base(dialect, connectionString, null, scope)
        {
            if (string.IsNullOrEmpty(providerName)) providerName = "Sybase.Data.AseClient";
            var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
            _connection = fac.CreateConnection();
            _connection.ConnectionString = _connectionString;
            this._connection.Open();
        }

        public SybaseTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
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
}
