using System.Collections.Generic;
namespace DotNetProjects.Migrator.Framework;

/// <summary>Optional provider contract for side-effect-free history inspection.</summary>
public interface IMigrationHistory
{
    string Scope { get; }
    IReadOnlyList<long> ReadAppliedMigrations();
    void InvalidateHistory();
}
