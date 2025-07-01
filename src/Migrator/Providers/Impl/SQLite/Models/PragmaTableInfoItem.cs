namespace DotNetProjects.Migrator.Providers.Impl.SQLite.Models;

public class PragmaTableInfoItem
{
    /// <summary>
    /// Gets or sets the column index (zero-based)
    /// </summary>
    public int Cid { get; set; }

    /// <summary>
    /// Gets or sets the name of the column
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the declared data type (INTEGER, TEXT, REAL etc.)
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets if is not null.
    /// </summary>
    public bool NotNull { get; set; }

    /// <summary>
    /// Gets or sets the default value or NULL
    /// </summary>
    public object DfltValue { get; set; }

    /// <summary>
    /// Gets or set the position in the primary key (1-based) 0 if not part of the primary key.
    /// </summary>
    public int Pk { get; set; }
}