namespace DotNetProjects.Migrator.Framework;

/// <summary>Controls index creation when the provider cannot apply the requested filters.</summary>
public enum UnsupportedIndexFilterBehavior
{
    /// <summary>Fail before creating the index.</summary>
    Throw,

    /// <summary>Create the index without any filters. Unique indexes then constrain all rows.</summary>
    Ignore
}
