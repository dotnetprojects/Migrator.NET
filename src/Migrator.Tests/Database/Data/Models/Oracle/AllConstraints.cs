namespace DotNetProjects.Migrator.Framework.Data.Models.Oracle;

/// <summary>
/// Represents the Oracle system table ALL_CONSTRAINTS
/// </summary>
public class AllConstraints
{
    /// <summary>
    /// Gets or sets the name of the constraint definition.
    /// </summary>
    public string ConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the name of the unique constraint definition for the referenced table (R_CONSTRAINT_NAME)
    /// </summary>
    public string RConstraintName { get; set; }

    /// <summary>
    /// Gets or sets the constraint type.
    /// 
    /// Type of the constraint definition:
    /// <list type="bullet"> 
    /// <item><description>C - Check constraint on a table</description></item>
    /// <item><description>P - Primary key</description></item>
    /// <item><description>U - Unique key</description></item>
    /// <item><description>R - Referential integrity</description></item>
    /// <item><description>V - With check option, on a view</description></item>
    /// <item><description>O - With read only, on a view</description></item>
    /// <item><description>H - Hash expression</description></item>
    /// <item><description>F - Constraint that involves a REF column</description></item>
    /// <item><description>S - Supplemental logging</description></item>
    /// </list>
    /// </summary>
    public string ConstraintType { get; set; }

    /// <summary>
    /// Gets or sets the owner of the constraint definition.
    /// </summary>
    public string Owner { get; set; }

    /// <summary>
    /// Gets or sets the owner of the table referred to in a referential constraint (R_OWNER)
    /// </summary>
    public string ROwner { get; set; }

    /// <summary>
    /// Gets or set the status. Enforcement status of the constraint: ENABLED / DISABLED
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the name associated with the table (or view) with the constraint definition.
    /// </summary>
    public string TableName { get; set; }
}
