namespace Migrator.Tests.Database;

public enum DatabaseProviderType
{
    // Do not use in any case not even as default
    None = 0,

    Unknown,

    // Postgre SQL
    Postgres,

    // SQL Server
    SQLServer,

    // SQLite
    SQLite,

    // Oracle
    Oracle
}