namespace Migrator.Tests.Settings.Models;

public class DatabaseConnectionConfig
{
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the connection identifier.
    /// </summary>
    public string Id { get; set; }
}