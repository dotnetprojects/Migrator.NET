using System.Data;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle;

public class MsOracleDialect : OracleDialect
{
    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
    {
        return new MsOracleTransformationProvider(dialect, connectionString, defaultSchema, scope, providerName);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
        string defaultSchema,
        string scope, string providerName)
    {
        return new MsOracleTransformationProvider(dialect, connection, defaultSchema, scope, providerName);
    }
}
