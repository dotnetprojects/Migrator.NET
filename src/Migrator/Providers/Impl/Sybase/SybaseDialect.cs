using System.Data;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Sybase;

public class SybaseDialect : Dialect
{
    public SybaseDialect()
    {
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
    {
        return new SybaseTransformationProvider(dialect, connectionString, scope, providerName);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
   string defaultSchema,
   string scope, string providerName)
    {
        return new SybaseTransformationProvider(dialect, connection, scope, providerName);
    }
}
