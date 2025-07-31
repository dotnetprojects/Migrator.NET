using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;

namespace Migrator.Providers.SQLite;

public class SQLiteMonoDialect : SQLiteDialect
{
    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
    {
        return new SQLiteMonoTransformationProvider(dialect, connectionString, scope, providerName);
    }
}
