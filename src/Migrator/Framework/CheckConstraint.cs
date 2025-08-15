namespace DotNetProjects.Migrator.Framework;

/// <summary>
/// Currently only used for SQLite
/// </summary>
public class CheckConstraint : IDbField
{
    public CheckConstraint()
    { }

    public CheckConstraint(string name, string checkConstraintText)
    {
        CheckConstraintString = checkConstraintText;
        Name = name;
    }

    /// <summary>
    /// Gets or sets the CheckConstraintString. Add it without the braces they will be added by the migrator.
    /// </summary>
    public string CheckConstraintString { get; set; }

    /// <summary>
    /// Gets or sets the name of the CHECK constraint.
    /// </summary>
    public string Name { get; set; }
}
