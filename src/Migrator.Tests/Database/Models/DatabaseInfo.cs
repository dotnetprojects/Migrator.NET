using Migrator.Tests.Settings.Models;

namespace Migrator.Tests.Database.Models;

public class DatabaseInfo
{
    /// <summary>
    /// Gets or sets the master <see cref="DatabaseConnectionConfig"/>
    /// </summary>
    public DatabaseConnectionConfig DatabaseConnectionConfigMaster { get; set; }

    /// <summary>
    /// Cloned <see cref="DatabaseConnectionConfigMaster"/> with manipulated connection string. The connection string contains the new database name.
    /// </summary>
    public DatabaseConnectionConfig DatabaseConnectionConfig { get; set; }

    /// <summary>
    /// Gets or sets the name of the created test database.
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the schema name. In Oracle the user name is equal to the schema name.
    /// </summary>
    public string SchemaName { get; set; }
}