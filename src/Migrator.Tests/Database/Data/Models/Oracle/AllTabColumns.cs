namespace DotNetProjects.Migrator.Framework.Data.Models.Oracle;

/// <summary>
/// Represents the Oracle system table ALL_TAB_COLUMNS
/// </summary>
public class AllTabColumns
{
    /// <summary>
    /// Gets or sets the column name
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the DATA_DEFAULT. This returns sth. like "SCHEMA"."ISEQ$$_1234".nextval
    /// </summary>
    public string DataDefault { get; set; }

    /// <summary>
    /// Gets or sets the length of the column (in bytes)
    /// </summary>
    public string DataLength { get; set; }

    /// <summary>
    /// Gets or sets the data type of the column
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// Indicates whether this is an identity column (YES) or not (NO)
    /// </summary>
    public string IdentityColumn { get; set; }

    /// <summary>
    /// Indicates whether a column allows NULLs. The value is N if there is a NOT NULL constraint on the column or if the column is part of a 
    /// PRIMARY KEY. The constraint should be in an ENABLE VALIDATE state.
    /// </summary>
    public string Nullable { get; set; }

    /// <summary>
    /// Gets or sets the name of the table, view, or cluster
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets the owner of the table, view, or cluster
    /// </summary>
    public string Owner { get; set; }
}