namespace DotNetProjects.Migrator.Providers.Models.Indexes.Enums;

public enum FilterType
{
    None = 0,

    /// <summary>
    /// Greater than
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Greater than or equal to
    /// </summary>
    GreaterThanOrEqualTo,

    /// <summary>
    /// Equal to
    /// </summary>
    EqualTo,

    /// <summary>
    /// Smaller than
    /// </summary>
    SmallerThan,

    /// <summary>
    /// Smaller than or equal to
    /// </summary>
    SmallerThanOrEqualTo,

    /// <summary>
    /// Not equal to
    /// </summary>
    NotEqualTo
}