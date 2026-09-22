using System.Data;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Mysql;

public class MariaDBDialect : MysqlDialect
{
    protected override string ResolveCollation(CollationKind kind) => kind switch
    {
        CollationKind.Binary => "utf8mb4_nopad_bin", CollationKind.CaseSensitive => "utf8mb4_uca1400_nopad_as_cs", CollationKind.CaseInsensitive => "utf8mb4_uca1400_nopad_as_ci",
        _ => throw new System.NotSupportedException("MariaDB cannot resolve " + kind + ". Use an installed named collation.")
    };

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
