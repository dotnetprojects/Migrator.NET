using Migrator.Framework;

namespace Migrator.Providers.Oracle
{
    public class OracleManagedDriverDialect : OracleDialect
    {
        public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema)
        {
            return new OracleManagedDriverTransformationProvider(dialect, connectionString, defaultSchema);
        }
    }
}