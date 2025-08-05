using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using DotNetProjects.Migrator.Framework;

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
                provider.Logger.Trace("{0} {1}", GetMigrationVersion(t).ToString().PadLeft(5), StringUtils.ToHumanName(t.Name));
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
                provider.Logger.Trace("{0} {1}", GetMigrationVersion(t).ToString().PadLeft(5), StringUtils.ToHumanName(t.Name));
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

            return GetMigrationVersion(_migrationsTypes[_migrationsTypes.Count - 1]);
        }
    }

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
        foreach (var t in _migrationsTypes)
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


#if NETSTANDARD
            var attrib = t.GetTypeInfo().GetCustomAttribute<MigrationAttribute>();
            if (attrib != null && typeof(IMigration).GetTypeInfo().IsAssignableFrom(t) && !attrib.Ignore)
            {
                migrations.Add(t);
            }
#else
            var attrib = (MigrationAttribute)Attribute.GetCustomAttribute(t, typeof(MigrationAttribute));
            if (attrib != null && typeof(IMigration).IsAssignableFrom(t) && !attrib.Ignore)
            {
                migrations.Add(t);
            }
#endif


        }

        migrations.Sort(new MigrationTypeComparer(true));
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
        return attrib.Version;
    }

    public List<long> GetAvailableMigrations()
    {
        _migrationsTypes.Sort(new MigrationTypeComparer(true));
        return _migrationsTypes.Select(x => GetMigrationVersion(x)).ToList();
    }

    public virtual IMigration GetMigration(long version)
    {
        foreach (var t in _migrationsTypes)
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
        return (IMigration)Activator.CreateInstance(migrationType);
    }
}
