using System;
using System.Collections.Generic;
using System.Linq;
using DotNetProjects.Migrator.Framework;
namespace DotNetProjects.Migrator;

public sealed record MigrationStep(long Version, bool IsUp);

public static class MigrationPlanner
{
    public static IReadOnlyList<MigrationStep> Create(IEnumerable<long> available, IEnumerable<long> applied, long target)
    {
        if (target < 0) throw new ArgumentOutOfRangeException(nameof(target));
        var versions = available.ToArray();
        var duplicate = versions.GroupBy(v => v).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null) throw new DuplicatedVersionException(duplicate.Key);
        if (versions.Any(v => v <= 0)) throw new MigrationException("Migration versions must be positive.");
        var history = applied.ToHashSet();
        var missing = history.Where(v => v > target && !versions.Contains(v)).ToArray();
        if (missing.Length != 0) throw new MigrationException("Missing downgrade migrations: " + string.Join(", ", missing));
        return history.Where(v => v > target).OrderByDescending(v => v).Select(v => new MigrationStep(v, false))
            .Concat(versions.Where(v => v <= target && !history.Contains(v)).OrderBy(v => v).Select(v => new MigrationStep(v, true)))
            .ToArray();
    }
}
