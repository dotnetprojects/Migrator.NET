using System.Data;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using Migrator.Framework;

namespace Migrator.Providers.Mysql;

public class MariaDBDialect : MysqlDialect
{
    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
    {
        return new MariaDBTransformationProvider(dialect, connectionString, scope, providerName);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
       string defaultSchema,
       string scope, string providerName)
    {
        return new MariaDBTransformationProvider(dialect, connection, scope, providerName);
    }
}
