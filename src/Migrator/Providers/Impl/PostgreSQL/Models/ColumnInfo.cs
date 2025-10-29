namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Models;

/// <summary>
/// Represents the INFORMATIONSCHEMA.COLUMNS
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// Gets or sets the date time precision.
    /// </summary>
    public int? DateTimePrecision { get; set; }

    /// <summary>
    /// Gets or sets the character maximum length.
    /// If data_type identifies a character or bit string type, the declared maximum length; null for all other data types or if no maximum length was declared.
    /// </summary>
    public int? CharacterMaximumLength { get; set; }

    /// <summary>
    /// Gets or sets the schema name.
    /// </summary>
    public string TableSchema { get; set; }

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the data type. Data type of the column, if it is a built-in type, or ARRAY if it is some array (in that case, see the view element_types), else USER-DEFINED (in that case, the type is identified in udt_name and associated columns). If the column is based on a domain, this column refers to the type underlying the domain (and the domain is identified in domain_name and associated columns).
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// Gets or sets the is nullable string.
    /// </summary>
    public string IsNullable { get; set; }

    /// <summary>
    /// Gets or sets the column default.
    /// </summary>
    public string ColumnDefault { get; set; }

    /// <summary>
    /// Gets or sets the is identity string. YES or NO.
    /// </summary>
    public string IsIdentity { get; set; }

    /// <summary>
    /// Gets or sets the identity generation.
    /// </summary>
    public string IdentityGeneration { get; set; }

    /// <summary>
    /// Gets or sets the ordinal position
    /// </summary>
    public int OrdinalPosition { get; set; }

    /// <summary>
    /// Gets or sets th numeric scale.
    /// </summary>
    public int? NumericScale { get; set; }

    /// <summary>
    /// Gets or sets the numeric precision.
    /// </summary>
    public int? NumericPrecision { get; set; }
}