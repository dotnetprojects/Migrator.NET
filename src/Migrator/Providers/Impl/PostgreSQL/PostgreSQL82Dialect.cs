using System.Data;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL;

public class PostgreSQL82Dialect : PostgreSQLDialect
{
    public PostgreSQL82Dialect()
    {
        RegisterColumnType(DbType.Guid, "uuid"); // Requires postgresql 8.2 and up
    }
}