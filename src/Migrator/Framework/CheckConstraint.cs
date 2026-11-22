namespace DotNetProjects.Migrator.Framework;

/// <summary>
/// A CHECK constraint expressed as native SQL or a portable expression.
/// </summary>
public class CheckConstraint : TableConstraint
{
    /// <summary>Portable expression rendered by the target dialect. Takes precedence over raw SQL.</summary>
    public SqlExpression Expression { get; set; }
    /// <summary>Creates a CHECK from a portable expression, keeping the string constructor unambiguous.</summary>
    public static CheckConstraint FromExpression(string name, SqlExpression expression)
    {
        System.ArgumentNullException.ThrowIfNull(expression);
        return new CheckConstraint { Name = name, Expression = expression };
    }
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
}
