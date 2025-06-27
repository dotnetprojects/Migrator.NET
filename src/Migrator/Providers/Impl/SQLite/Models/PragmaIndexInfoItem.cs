namespace DotNetProjects.Migrator.Providers.Impl.SQLite.Models;

public class PragmaIndexInfoItem
{
    /// <summary>
    /// Gets or sets the sequence number of the column in the index (zero-based)
    /// </summary>
    public int SeqNo { get; set; }

    /// <summary>
    /// Gets or sets the column ID. -1 if expression
    /// </summary>
    public int Cid { get; set; }

    /// <summary>
    /// Gets or sets the name of the column (expression if no not column related)
    /// </summary>
    public string Name { get; set; }
}