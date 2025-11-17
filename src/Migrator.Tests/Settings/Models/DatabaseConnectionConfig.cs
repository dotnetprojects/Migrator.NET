namespace Migrator.Tests.Settings.Models;

public class DatabaseConnectionConfig
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the connection identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string Schema { get; set; }
}