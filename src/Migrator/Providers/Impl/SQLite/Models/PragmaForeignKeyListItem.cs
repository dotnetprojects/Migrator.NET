namespace DotNetProjects.Migrator.Providers.Impl.SQLite.Models;

/// <summary>
/// Represents a row of pragma_foreign_key_list() in SQLite.
/// </summary>
public class PragmaForeignKeyListItem
{
    /// <summary>
    /// Gets or sets the foreign key id. Name: id
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the foreign key. Name: seq
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// Gets or sets the name of the referenced table. Name: table
    /// </summary>
    public string Table { get; set; }

    /// <summary>
    /// Gets or sets the column in the current table that acts as the FK. Name: from
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the column in the referenced table. Name: to
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets on update. Name: on_update
    /// </summary>
    public string OnUpdate { get; set; }

    /// <summary>
    /// Gets or sets on delete. Name: on_delete
    /// </summary>
    public string OnDelete { get; set; }

    /// <summary>
    /// Gets or sets match. Name: match
    /// </summary>
    public string Match { get; set; }
}