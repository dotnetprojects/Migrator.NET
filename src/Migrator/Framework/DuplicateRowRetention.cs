namespace DotNetProjects.Migrator.Framework;

/// <summary>Which row survives within each duplicate key group.</summary>
public enum DuplicateRowRetention
{
    /// <summary>Keep one arbitrary row; other column values do not determine the survivor.</summary>
    Any
}
