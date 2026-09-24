namespace DotNetProjects.Migrator.Framework;

public enum DuplicateNullHandling
{
    /// <summary>NULL key values compare equal for duplicate grouping.</summary>
    Equal,
    /// <summary>Do not delete rows containing NULL in any key column.</summary>
    ExcludeNullKeys
}
