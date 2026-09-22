using System;
using System.Linq;
using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;

namespace DotNetProjects.Migrator;

/// <summary>
/// Description of MigrateAnywhere.
/// </summary>
public class MigrateAnywhere : BaseMigrate
{
    private bool _goForward;

    public MigrateAnywhere(List<long> availableMigrations, ITransformationProvider provider, ILogger logger)
        : base(availableMigrations, provider, logger)
    {
        _current = 0;
        if (provider.AppliedMigrations.Count > 0)
        {
            _current = provider.AppliedMigrations.Max();
        }
        _goForward = false;
    }

    public override long Next
    {
        get
        {
            return _goForward
                    ? NextMigration()
                    : PreviousMigration();
        }
    }

    public override long Previous
    {
        get
        {
            return _goForward
                    ? PreviousMigration()
                    : NextMigration();
        }
    }

    public override bool Continue(long version)
    {
        // If we're going backwards and our current is less than the target, 
        // reverse direction.  Also, start over at zero to make sure we catch
        // any merged migrations that are less than the current target.
        if (!_goForward && version >= Current)
        {
            _goForward = true;
            Current = 0;
            Iterate();
        }

        // We always finish on going forward. So continue if we're still 
        // going backwards, or if there are no migrations left in the forward direction.
        return !_goForward || Current <= version;
    }

    public override void Migrate(IMigration migration)
    {
        if (DryRun) return;
        var version = MigrationLoader.GetMigrationVersion(migration.GetType());
        MigrationExecution.Execute(_provider, migration,
            new MigrationStep(version, !_provider.AppliedMigrations.Contains(version)), _logger);
    }
}
