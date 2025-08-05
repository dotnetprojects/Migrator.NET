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
        MigrateTo(_migrationLoader.LastVersion);
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
    public void MigrateTo(long version)
    {
        if (_migrationLoader.MigrationsTypes.Count == 0)
        {
            _logger.Warn("No public classes with the Migration attribute were found.");
            return;
        }

        var firstRun = true;
        var migrate = BaseMigrate.GetInstance(_migrationLoader.GetAvailableMigrations(), _provider, _logger);
        migrate.DryRun = DryRun;
        Logger.Started(migrate.AppliedVersions, version);

        while (migrate.Continue(version))
        {
            var migration = _migrationLoader.GetMigration(migrate.Current);
            if (null == migration)
            {
                _logger.Skipping(migrate.Current);
                migrate.Iterate();
                continue;
            }

            try
            {
                if (firstRun)
                {
                    migration.InitializeOnce(_args);
                    firstRun = false;
                }

                migrate.Migrate(migration);
            }
            catch (Exception ex)
            {
                Logger.Exception(migrate.Current, migration.Name, ex);

                // Oho! error! We rollback changes.
                Logger.RollingBack(migrate.Previous);
                _provider.Rollback();

                throw;
            }

            migrate.Iterate();
        }

        Logger.Finished(migrate.AppliedVersions, version);
    }
}
