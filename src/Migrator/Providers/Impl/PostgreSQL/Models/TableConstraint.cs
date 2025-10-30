namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Models;

public class TableConstraint
{
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
    /// Gets or sets the constraint name.
    /// </summary>
    public string ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the constraint type.
    /// </summary>
    public string ConstraintType { get; set; }
}