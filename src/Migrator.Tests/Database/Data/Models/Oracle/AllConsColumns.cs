namespace DotNetProjects.Migrator.Framework.Data.Models.Oracle;

/// <summary>
/// Represents the Oracle system table ALL_CONS_COLUMNS
/// </summary>
public class AllConsColumns
{
    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the name of the constraint definition.
    /// </summary>
    public string ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets 
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the name of the table with the constraint definition.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets the owner of the constraint definition.
    /// </summary>
    public string Owner { get; set; }
}