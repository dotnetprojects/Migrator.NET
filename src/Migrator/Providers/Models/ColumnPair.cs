namespace DotNetProjects.Migrator.Framework.Models;

/// <summary>
/// Represents a column pair for usage e.g. in a column comparison.
/// </summary>
public class ColumnPair
{
    /// <summary>
    /// Gets or sets the column name of the source table. Use the not quoted column name.
    /// </summary>
    public string ColumnNameSourceNotQuoted { get; set; }

    /// <summary>
    /// Gets or sets the column name of the target table. Use the not quoted column name.
    /// </summary>
    public string ColumnNameTargetNotQuoted { get; set; }
}