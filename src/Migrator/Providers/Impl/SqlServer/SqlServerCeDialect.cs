using System.Data;
using Migrator.Framework;

namespace Migrator.Providers.SqlServer
{
    public class SqlServerCeDialect : SqlServerDialect
    {
        public SqlServerCeDialect()
        {
            RegisterColumnType(DbType.AnsiStringFixedLength, "NCHAR(255)");
            RegisterColumnType(DbType.AnsiStringFixedLength, 4000, "NCHAR($l)");
            RegisterColumnType(DbType.AnsiString, "NVARCHAR(255)");
            RegisterColumnType(DbType.AnsiString, 4000, "NVARCHAR($l)");
            RegisterColumnType(DbType.AnsiString, 1073741823, "TEXT");
            RegisterColumnType(DbType.Double, "FLOAT");
        }

        public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        {
            return new SqlServerCeTransformationProvider(dialect, connectionString, scope, providerName);
        }

        public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
         string defaultSchema,
         string scope, string providerName)
        {
            return new SqlServerCeTransformationProvider(dialect, connection, scope, providerName);
        }
    }
}
