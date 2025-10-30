namespace DotNetProjects.Migrator.Providers.Impl.Oracle.Models;

/// <summary>
/// Represents USER_TAB_IDENTITY_COLS partly
/// </summary>
public class UserTabIdentityCols
{
    /// <summary>
    /// Gets or sets the name of the identity column. Column: COLUMN_NAME
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the generation type of the identity column. Possible values are ALWAYS or BY DEFAULT. Column: GENERATION_TYPE
    /// </summary>
    public string GenerationType { get; set; }

    /// <summary>
    /// Gets or sets the name of the sequence associated with the identity column. Column: SEQUENCE_NAME
    /// </summary>
    public string SequenceName { get; set; }

    /// <summary>
    /// Gets or sets the name of the table. Column: TABLE_NAME
    /// </summary>
    public string TableName { get; set; }
}