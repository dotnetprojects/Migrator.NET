using System;
using System.Reflection;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
namespace DotNetProjects.Migrator;

internal static class MigrationExecution
{
    internal static void Execute(ITransformationProvider provider, IMigration migration, MigrationStep step, ILogger logger)
    {
        var concrete = provider as TransformationProvider;
        if (concrete?.HasActiveTransaction == true)
            throw new MigrationException("The runner cannot take ownership of an existing provider transaction.");
        var sqlite = provider as SQLiteTransformationProvider;
        var foreignKeys = sqlite?.IsPragmaForeignKeysOn() == true;
        Exception failure = null;
        var began = false;
        try
        {
            if (foreignKeys) sqlite.SetPragmaForeignKeys(false);
            provider.BeginTransaction();
            began = true;
            if (concrete != null) concrete.CurrentMigration = migration;
            if (step.IsUp) { logger.MigrateUp(step.Version, migration.Name); migration.Up(); }
            else { logger.MigrateDown(step.Version, migration.Name); migration.Down(); }
            if (sqlite != null && !sqlite.CheckForeignKeyIntegrity())
                throw new MigrationException("Migration would leave invalid SQLite foreign keys.");
            if (step.IsUp) provider.MigrationApplied(step.Version, migration.GetType().GetCustomAttribute<MigrationAttribute>()?.Scope ?? (provider as IMigrationHistory)?.Scope);
            else provider.MigrationUnApplied(step.Version, migration.GetType().GetCustomAttribute<MigrationAttribute>()?.Scope ?? (provider as IMigrationHistory)?.Scope);
            provider.Commit();
            began = false;
        }
        catch (Exception ex)
        {
            failure = ex;
            if (began)
            {
                try { provider.Rollback(); }
                catch (Exception rollback) { ex.Data["RollbackException"] = rollback; }
            }
            logger.Exception(step.Version, migration.Name, ex);
            throw;
        }
        finally
        {
            if (concrete != null) concrete.CurrentMigration = null;
            try { if (foreignKeys) sqlite.SetPragmaForeignKeys(true); }
            catch (Exception restore)
            {
                if (failure == null) throw;
                failure.Data["ConnectionRestoreException"] = restore;
            }
        }
        // These callbacks intentionally run after commit; failure cannot be rolled back.
        After(provider, migration, step.IsUp);
    }
    internal static void After(ITransformationProvider provider, IMigration migration, bool up)
    {
        var concrete = provider as TransformationProvider;
        var previous = concrete?.CurrentMigration;
        if (concrete != null) concrete.CurrentMigration = migration;
        try { if (up) migration.AfterUp(); else migration.AfterDown(); }
        finally { if (concrete != null) concrete.CurrentMigration = previous; }
    }

}
