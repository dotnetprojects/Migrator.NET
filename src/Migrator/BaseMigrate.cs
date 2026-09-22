using System;
using System.Collections.Generic;
using System.Linq;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator;

public abstract class BaseMigrate
{
    protected readonly ITransformationProvider _provider;
    protected List<long> _availableMigrations;
    protected long _current;
    protected bool _dryrun;
    protected ILogger _logger;
    protected List<long> _original;
    private bool _initialized;

    protected BaseMigrate(List<long> availableMigrations, ITransformationProvider provider, ILogger logger)
    {
        _provider = provider;
        _availableMigrations = availableMigrations.OrderBy(version => version).ToList();
        _logger = logger;
    }

    protected IReadOnlyList<long> ReadHistory()
    {
        if (_provider is IMigrationHistory history) return history.ReadAppliedMigrations();
        if (DryRun) throw new NotSupportedException("Legacy dry-run requires IMigrationHistory on custom providers.");
        return _provider.AppliedMigrations;
    }

    private void InitializeHistory()
    {
        if (_initialized) return;
        _original = new List<long>(ReadHistory());
        _current = _original.DefaultIfEmpty(0).Max();
        _initialized = true;
    }

    public List<long> AppliedVersions
    {
        get { InitializeHistory(); return _original; }
    }

    public virtual long Current
    {
        get { InitializeHistory(); return _current; }
        protected set { InitializeHistory(); _current = value; }
    }

    public virtual bool DryRun
    {
        get { return _dryrun; }
        set { _dryrun = value; }
    }

    public abstract long Previous { get; }
    public abstract long Next { get; }

    public static BaseMigrate GetInstance(List<long> availableMigrations, ITransformationProvider provider, ILogger logger)
    {
        return new MigrateAnywhere(availableMigrations, provider, logger);
    }

    public void Iterate()
    {
        Current = Next;
    }

    public abstract bool Continue(long targetVersion);

    public abstract void Migrate(IMigration migration);

    /// <summary>
    /// Finds the next migration available to be applied.  Only returns
    /// migrations that have NOT already been applied.
    /// </summary>
    /// <returns>The migration number of the next available Migration.</returns>
    protected long NextMigration()
    {
        if (_availableMigrations.Count == 0) return 0;
        // Start searching at the current index
        var migrationSearch = _availableMigrations.IndexOf(Current) + 1;

        // See if we can find a migration that matches the requirement
        while (migrationSearch < _availableMigrations.Count
               && ReadHistory().Contains(_availableMigrations[migrationSearch]))
        {
            migrationSearch++;
        }

        // did we exhaust the list?
        if (migrationSearch == _availableMigrations.Count)
        {
            // we're at the last one.  Done!
            return _availableMigrations[migrationSearch - 1] + 1;
        }
        // found one.
        return _availableMigrations[migrationSearch];
    }

    /// <summary>
    /// Finds the previous migration that has been applied.  Only returns
    /// migrations that HAVE already been applied.
    /// </summary>
    /// <returns>The most recently applied Migration.</returns>
    protected long PreviousMigration()
    {
        // Start searching at the current index
        var migrationSearch = _availableMigrations.IndexOf(Current) - 1;

        // See if we can find a migration that matches the requirement
        while (migrationSearch > -1
               && !ReadHistory().Contains(_availableMigrations[migrationSearch]))
        {
            migrationSearch--;
        }

        // did we exhaust the list?
        if (migrationSearch < 0)
        {
            // we're at the first one.  Done!
            return 0;
        }

        // found one.
        return _availableMigrations[migrationSearch];
    }
}