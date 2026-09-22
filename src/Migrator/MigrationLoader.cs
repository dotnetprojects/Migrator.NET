using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;

namespace DotNetProjects.Migrator;

/// <summary>
/// Handles inspecting code to find all of the Migrations in assemblies and reading
/// other metadata such as the last revision, etc.
/// </summary>
public class MigrationLoader
{
    private readonly List<Type> _migrationsTypes = new List<Type>();
    private readonly ITransformationProvider _provider;

    public MigrationLoader(ITransformationProvider provider, Assembly migrationAssembly, bool trace)
    {
        _provider = provider;
        AddMigrations(migrationAssembly);

        if (trace)
        {
            provider.Logger.Trace("Loaded migrations:");
            foreach (var t in _migrationsTypes)
            {
                provider.Logger.Trace("{0} {1}", (t.GetCustomAttribute<MigrationAttribute>()?.Version.ToString() ?? "aux").PadLeft(5), StringUtils.ToHumanName(t.Name));
            }
        }
    }

    public MigrationLoader(ITransformationProvider provider, bool trace, params Type[] migrationTypes)
    {
        _provider = provider;
        _migrationsTypes.AddRange(migrationTypes);

        if (trace)
        {
            provider.Logger.Trace("Loaded migrations:");
            foreach (var t in _migrationsTypes)
            {
                provider.Logger.Trace("{0} {1}", (t.GetCustomAttribute<MigrationAttribute>()?.Version.ToString() ?? "aux").PadLeft(5), StringUtils.ToHumanName(t.Name));
            }
        }
    }

    /// <summary>
    /// Returns registered migration <see cref="System.Type">types</see>.
    /// </summary>
    public virtual List<Type> MigrationsTypes
    {
        get { return _migrationsTypes; }
    }

    /// <summary>
    /// Returns the last version of the migrations.
    /// </summary>
    public virtual long LastVersion
    {
        get
        {
            if (_migrationsTypes.Count == 0)
            {
                return 0;
            }

            return SelectedTypes.Select(GetMigrationVersion).DefaultIfEmpty(0).Max();
        }
    }

    public Func<Type, IMigration> Activator { get; set; }

    public IEnumerable<Type> SelectedTypes => _migrationsTypes.Where(t =>
        t.GetCustomAttribute<MigrationAttribute>() != null && InScope(t.GetCustomAttribute<MigrationAttribute>().Scope));

    internal bool InScope(string scope) => scope == null || _provider is not IMigrationHistory history || scope == history.Scope;
    internal IEnumerable<Type> AuxiliaryTypes => _migrationsTypes.Where(t => t.GetCustomAttribute<MigrationAttribute>() == null);


    public virtual void AddMigrations(Assembly migrationAssembly)
    {
        if (migrationAssembly != null)
        {
            _migrationsTypes.AddRange(GetMigrationTypes(migrationAssembly));
        }
    }

    /// <summary>
    /// Check for duplicated version in migrations.
    /// </summary>
    /// <exception cref="CheckForDuplicatedVersion">CheckForDuplicatedVersion</exception>
    public virtual void CheckForDuplicatedVersion()
    {
        var versions = new List<long>();
        foreach (var t in SelectedTypes)
        {
            var version = GetMigrationVersion(t);

            if (versions.Contains(version))
            {
                throw new DuplicatedVersionException(version);
            }

            versions.Add(version);
        }
    }

    /// <summary>
    /// Collect migrations in one <c>Assembly</c>.
    /// </summary>
    /// <param name="asm">The <c>Assembly</c> to browse.</param>
    /// <returns>The migrations collection</returns>
    public static List<Type> GetMigrationTypes(Assembly asm)
    {
        var migrations = new List<Type>();
        foreach (var t in asm.GetExportedTypes())
        {
            if (t.IsAbstract || !typeof(IMigration).IsAssignableFrom(t)) continue;
            var versioned = t.GetCustomAttribute<MigrationAttribute>();
            if (versioned != null ? !versioned.Ignore :
                t.GetCustomAttribute<ProfileAttribute>() != null || t.GetCustomAttribute<MaintenanceAttribute>() != null)
                migrations.Add(t);
        }
        migrations = migrations.OrderBy(t => t.GetCustomAttribute<MigrationAttribute>()?.Version ?? 0).ThenBy(t => t.FullName, StringComparer.Ordinal).ToList();
        return migrations;
    }

    /// <summary>
    /// Returns the version of the migration
    /// <see cref="MigrationAttribute">MigrationAttribute</see>.
    /// </summary>
    /// <param name="t">Migration type.</param>
    /// <returns>Version number sepcified in the attribute</returns>
    public static long GetMigrationVersion(Type t)
    {
        var attrib = (MigrationAttribute)Attribute.GetCustomAttribute(t, typeof(MigrationAttribute));
        return attrib?.Version ?? throw new ArgumentException($"{t.FullName} has no Migration attribute.");
    }

    public List<long> GetAvailableMigrations()
    {
        return SelectedTypes.Select(GetMigrationVersion).OrderBy(v => v).ToList();
    }

    public virtual IMigration GetMigration(long version)
    {
        foreach (var t in SelectedTypes)
        {
            if (GetMigrationVersion(t) == version)
            {
                var migration = CreateInstance(t);
                migration.Database = _provider;
                return migration;
            }
        }

        return null;
    }

    public virtual IMigration CreateInstance(Type migrationType)
    {
        return Activator != null ? Activator(migrationType) ?? throw new MigrationException("Migration activator returned null.") : (IMigration)System.Activator.CreateInstance(migrationType);
    }
}
