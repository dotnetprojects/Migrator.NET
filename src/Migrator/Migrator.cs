#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Loggers;
using DotNetProjects.Migrator.Providers;

namespace DotNetProjects.Migrator;

/// <summary>
/// Migrations mediator.
/// </summary>
public class Migrator
{
    public RunnerOptions Options { get; init; } = new();
    private readonly MigrationLoader _migrationLoader;
    private readonly ITransformationProvider _provider;

    private string[] _args;
    protected bool _dryrun;
    private ILogger _logger = new Logger(false);

    public Migrator(ProviderTypes provider, string connectionString, string defaultSchema, Assembly migrationAssembly)
        : this(provider, connectionString, defaultSchema, migrationAssembly, false)
    {
    }

    public Migrator(ProviderTypes provider, string connectionString, string defaultSchema, params Type[] migrationTypes)
        : this(provider, connectionString, defaultSchema, false, migrationTypes)
    {
    }

    public Migrator(ProviderTypes provider, string connectionString, string defaultSchema, Assembly migrationAssembly, bool trace)
        : this(ProviderFactory.Create(provider, connectionString, defaultSchema), migrationAssembly, trace)
    {
    }

    public Migrator(ProviderTypes provider, string connectionString, string defaultSchema, bool trace, params Type[] migrationTypes)
        : this(ProviderFactory.Create(provider, connectionString, defaultSchema), trace, migrationTypes)
    {
    }

    public Migrator(ProviderTypes provider, string connectionString, string defaultSchema, Assembly migrationAssembly, bool trace, ILogger logger)
        : this(ProviderFactory.Create(provider, connectionString, defaultSchema), migrationAssembly, trace, logger)
    {
    }

    public Migrator(ProviderTypes provider, string connectionString, string defaultSchema, bool trace, ILogger logger, params Type[] migrationTypes)
        : this(ProviderFactory.Create(provider, connectionString, defaultSchema), trace, logger, migrationTypes)
    {
    }

    public Migrator(ITransformationProvider provider, Assembly migrationAssembly, bool trace)
        : this(provider, migrationAssembly, trace, new Logger(trace, new ConsoleWriter()))
    {
    }

    public Migrator(ITransformationProvider provider, bool trace, params Type[] migrationTypes)
        : this(provider, trace, new Logger(trace, new ConsoleWriter()), migrationTypes)
    {
    }

    public Migrator(ITransformationProvider provider, Assembly migrationAssembly, bool trace, ILogger logger)
    {
        _provider = provider;
        Logger = logger;

        _migrationLoader = new MigrationLoader(provider, migrationAssembly, trace);
        _migrationLoader.CheckForDuplicatedVersion();
    }

    public Migrator(ITransformationProvider provider, bool trace, ILogger logger, params Type[] migrationTypes)
    {
        _provider = provider;
        Logger = logger;

        _migrationLoader = new MigrationLoader(provider, trace, migrationTypes);
        _migrationLoader.CheckForDuplicatedVersion();
    }

    public Migrator(ITransformationProvider provider, ILogger logger, MigrationLoader migrationLoader)
    {
        _provider = provider;
        Logger = logger;

        _migrationLoader = migrationLoader;
        _migrationLoader.CheckForDuplicatedVersion();
    }

    public string[] args
    {
        get { return _args; }
        set { _args = value; }
    }

    /// <summary>
    /// Returns registered migration <see cref="System.Type">types</see>.
    /// </summary>
    public List<Type> MigrationsTypes
    {
        get { return _migrationLoader.MigrationsTypes; }
    }

    /// <summary>
    /// Set or get the Schema Info table name, where the migration applied are saved
    /// Default is: SchemaInfo
    /// </summary>
    public string SchemaInfoTableName
    {
        get
        {
            return _provider.SchemaInfoTable;
        }

        set
        {
            _provider.SchemaInfoTable = value;
        }
    }

    /// <summary>
    /// Returns the current migrations applied to the database.
    /// </summary>
    public List<long> AppliedMigrations
    {
        get { return _provider.AppliedMigrations; }
    }

    /// <summary>
    /// Get or set the event logger.
    /// </summary>
    public ILogger Logger
    {
        get { return _logger; }
        set
        {
            _logger = value;
            _provider.Logger = value;
        }
    }

    public virtual bool DryRun
    {
        get { return _dryrun; }
        set { _dryrun = value; }
    }

    public long AssemblyLastMigrationVersion
    {
        get { return _migrationLoader.LastVersion; }
    }

    public long? LastAppliedMigrationVersion
    {
        get
        {
            if (AppliedMigrations.Count() == 0)
            {
                return null;
            }

            return AppliedMigrations.Max();
        }
    }

    /// <summary>
    /// Run all migrations up to the latest.  Make no changes to database if
    /// dryrun is true.
    /// </summary>
    public void MigrateToLastVersion()
    {
        MigrateTo(SelectedMigrationTypes.Select(MigrationLoader.GetMigrationVersion).DefaultIfEmpty(0).Max());
    }

    /// <summary>
    /// Migrate the database to a specific version.
    /// Runs all migration between the actual version and the
    /// specified version.
    /// If <c>version</c> is greater then the current version,
    /// the <c>Up()</c> method will be invoked.
    /// If <c>version</c> lower then the current version,
    /// the <c>Down()</c> method of previous migration will be invoked.
    /// If <c>dryrun</c> is set, don't write any changes to the database.
    /// </summary>
    /// <param name="version">The version that must became the current one</param>
    private IEnumerable<Type> SelectedMigrationTypes => _migrationLoader.SelectedTypes.Where(t =>
    {
        if (Options.Tags.Count == 0) return true;
        var tags = t.GetCustomAttribute<TagsAttribute>()?.Tags ?? Array.Empty<string>();
        return Options.TagMatch == TagMatchMode.All ? Options.Tags.All(tags.Contains) : Options.Tags.Any(tags.Contains);
    });

    private IReadOnlyList<MigrationStep> CreatePlan(IEnumerable<long> applied, long version)
    {
        _migrationLoader.CheckForDuplicatedVersion();
        var selected = SelectedMigrationTypes.Select(MigrationLoader.GetMigrationVersion).ToHashSet();
        var known = _migrationLoader.GetAvailableMigrations().ToHashSet();
        // Filtered migrations stay applied; unknown history must still fail a downgrade.
        return MigrationPlanner.Create(selected, applied.Where(v => selected.Contains(v) || !known.Contains(v)), version);
    }

    public IReadOnlyList<MigrationStep> Plan(long version)
    {
        if (_provider is not IMigrationHistory history)
            throw new NotSupportedException("Read-only planning requires IMigrationHistory on custom providers.");
        return CreatePlan(history.ReadAppliedMigrations(), version);
    }

    public string PreviewSql(long version, ProviderTypes provider, bool allowLegacyBodies = false)
    {
        _migrationLoader.Activator = Options.Activator;
        var plan = Plan(version);
        var migrations = new List<(IMigration, bool)>();
        void AddMaintenance(MaintenanceStage stage)
        {
            foreach (var type in _migrationLoader.AuxiliaryTypes.Where(t => t.GetCustomAttribute<MaintenanceAttribute>() is { } a && a.Stage == stage && _migrationLoader.InScope(a.Scope))
                .OrderBy(t => t.GetCustomAttribute<MaintenanceAttribute>().Order).ThenBy(t => t.FullName, StringComparer.Ordinal))
                migrations.Add((_migrationLoader.CreateInstance(type), true));
        }
        AddMaintenance(MaintenanceStage.BeforeRun);
        foreach (var step in plan)
        {
            AddMaintenance(MaintenanceStage.BeforeMigration);
            migrations.Add((_migrationLoader.GetMigration(step.Version), step.IsUp));
            AddMaintenance(MaintenanceStage.AfterMigration);
        }
        foreach (var name in Options.Profiles)
            if (!_migrationLoader.AuxiliaryTypes.Any(t => t.GetCustomAttribute<ProfileAttribute>() is { } a && a.Name == name && _migrationLoader.InScope(a.Scope)))
                throw new MigrationException("Unknown profile: " + name);
        foreach (var type in _migrationLoader.AuxiliaryTypes.Where(t => t.GetCustomAttribute<ProfileAttribute>() is { } a && Options.Profiles.Contains(a.Name) && _migrationLoader.InScope(a.Scope))
            .OrderBy(t => t.GetCustomAttribute<ProfileAttribute>().Order).ThenBy(t => t.FullName, StringComparer.Ordinal))
            migrations.Add((_migrationLoader.CreateInstance(type), true));
        AddMaintenance(MaintenanceStage.AfterRun);
        return MigrationSqlPreview.Generate(provider, migrations, allowLegacyBodies,
            table => _provider.TableExists(table) ? _provider.GetColumns(table) : throw new MigrationException("Preview table does not exist: " + table));
    }

    public void MigrateTo(long version)
    {
        if (DryRun)
        {
            foreach (var step in Plan(version))
                if (step.IsUp) Logger.MigrateUp(step.Version, "Preview"); else Logger.MigrateDown(step.Version, "Preview");
            return;
        }
        if (Options.LockTimeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Options.LockTimeout));
        var session = Options.TransactionMode == MigrationTransactionMode.WholeSession;
        if (session && _provider.Dialect is not (Providers.Impl.SQLite.SQLiteDialect or Providers.Impl.PostgreSQL.PostgreSQLDialect or Providers.Impl.SqlServer.SqlServerDialect))
            throw new NotSupportedException("Whole-session transactions require a verified transactional DDL provider (SQLite, PostgreSQL or SQL Server).");
        _migrationLoader.Activator = Options.Activator;
        using var lease = Options.Lock?.Acquire(_provider, (_provider as IMigrationHistory)?.Scope, Options.LockTimeout);
        (_provider as IMigrationHistory)?.InvalidateHistory();
        var history = new List<long>(_provider.AppliedMigrations);
        var plan = CreatePlan(history, version);
        var profiles = _migrationLoader.AuxiliaryTypes.Where(t => t.GetCustomAttribute<ProfileAttribute>() is { } p && Options.Profiles.Contains(p.Name) && _migrationLoader.InScope(p.Scope))
            .OrderBy(t => t.GetCustomAttribute<ProfileAttribute>().Order).ThenBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        foreach (var name in Options.Profiles)
            if (!profiles.Any(t => t.GetCustomAttribute<ProfileAttribute>().Name == name)) throw new MigrationException("Unknown profile: " + name);
        var afterCommit = new List<Action>();
        var firstRun = true;
        void Execute(IMigration migration, MigrationStep step, bool record)
        {
            migration.Database = _provider;
            if (firstRun) { migration.InitializeOnce(_args); firstRun = false; }
            MigrationExecution.Execute(_provider, migration, step, Logger,
                Options.TransactionMode == MigrationTransactionMode.PerMigration, session, record, !session);
            if (session) afterCommit.Add(() => MigrationExecution.After(migration, step.IsUp));
        }
        void Maintenance(MaintenanceStage stage)
        {
            foreach (var type in _migrationLoader.AuxiliaryTypes.Where(t => t.GetCustomAttribute<MaintenanceAttribute>() is { } a && a.Stage == stage && _migrationLoader.InScope(a.Scope))
                .OrderBy(t => t.GetCustomAttribute<MaintenanceAttribute>().Order).ThenBy(t => t.FullName, StringComparer.Ordinal))
                Execute(_migrationLoader.CreateInstance(type), new MigrationStep(0, true), false);
        }
        void Run()
        {
            Maintenance(MaintenanceStage.BeforeRun);
            foreach (var step in plan)
            {
                Maintenance(MaintenanceStage.BeforeMigration);
                Execute(_migrationLoader.GetMigration(step.Version), step, true);
                if (step.IsUp) history.Add(step.Version); else history.Remove(step.Version);
                Maintenance(MaintenanceStage.AfterMigration);
            }
            foreach (var type in profiles) Execute(_migrationLoader.CreateInstance(type), new MigrationStep(0, true), false);
            Maintenance(MaintenanceStage.AfterRun);
        }
        Logger.Started(history, version);
        if (session) MigrationExecution.InTransaction(_provider, true, Run); else Run();
        foreach (var callback in afterCommit) callback();
        history.Sort();
        Logger.Finished(history, version);
    }
}

