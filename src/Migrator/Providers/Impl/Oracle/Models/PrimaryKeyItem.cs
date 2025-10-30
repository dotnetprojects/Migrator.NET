namespace DotNetProjects.Migrator.Providers.Impl.Oracle.Models;

public class PrimaryKeyItem
{
    /// <summary>
    /// Gets or sets the table name USER_CONS_COLUMNS.TABLE_NAME
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets the column name USER_CONS_COLUMNS.COLUMN_NAME
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets USER_CONS_COLUMNS.POSITION
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets USER_CONSTRAINTS.STATUS Enforcement status of the constraint: ENABLED, DISABLED
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the USER_CONSTRAINTS.CONSTRAINT_NAME
    /// </summary>
    public string ConstraintName { get; set; }
}