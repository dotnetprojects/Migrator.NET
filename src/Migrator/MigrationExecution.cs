using System;
using System.Reflection;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
namespace DotNetProjects.Migrator;

internal static class MigrationExecution
{
    internal static void Execute(ITransformationProvider provider, IMigration migration, MigrationStep step, ILogger logger,
        bool transaction = true, bool inSession = false, bool recordHistory = true, bool callbacks = true)
    {
        var concrete = provider as TransformationProvider;
        try
        {
            void Body()
            {
                if (concrete != null) concrete.CurrentMigration = migration;
                if (step.IsUp) { logger.MigrateUp(step.Version, migration.Name); migration.Up(); }
                else { logger.MigrateDown(step.Version, migration.Name); migration.Down(); }
                if (provider is SQLiteTransformationProvider sqlite && !sqlite.CheckForeignKeyIntegrity())
                    throw new MigrationException("Migration would leave invalid SQLite foreign keys.");
                if (recordHistory)
                {
                    var scope = migration.GetType().GetCustomAttribute<MigrationAttribute>()?.Scope ?? (provider as IMigrationHistory)?.Scope;
                    if (step.IsUp) provider.MigrationApplied(step.Version, scope);
                    else provider.MigrationUnApplied(step.Version, scope);
                }
            }
            if (inSession) Body(); else InTransaction(provider, transaction, Body);
        }
        catch (Exception ex) { logger.Exception(step.Version, migration.Name, ex); throw; }
        finally { if (concrete != null) concrete.CurrentMigration = null; }
        // Session callbacks are deferred until the outer transaction commits.
        if (callbacks) After(migration, step.IsUp);
    }

    internal static void After(IMigration migration, bool up)
    { if (up) migration.AfterUp(); else migration.AfterDown(); }

    internal static void InTransaction(ITransformationProvider provider, bool transaction, Action body)
    {
        if ((provider as TransformationProvider)?.HasActiveTransaction == true)
            throw new MigrationException("The runner cannot take ownership of an existing provider transaction.");
        var sqlite = provider as SQLiteTransformationProvider;
        var foreignKeys = transaction && sqlite?.IsPragmaForeignKeysOn() == true;
        Exception failure = null;
        var began = false;
        try
        {
            if (foreignKeys) sqlite.SetPragmaForeignKeys(false);
            if (transaction) { provider.BeginTransaction(); began = true; }
            body();
            if (transaction) { provider.Commit(); began = false; }
        }
        catch (Exception ex)
        {
            failure = ex;
            if (began)
            {
                try { provider.Rollback(); }
                catch (Exception rollback) { ex.Data["RollbackException"] = rollback; }
            }
            throw;
        }
        finally
        {
            try { if (foreignKeys) sqlite.SetPragmaForeignKeys(true); }
            catch (Exception restore)
            {
                if (failure == null) throw;
                failure.Data["ConnectionRestoreException"] = restore;
            }
            (provider as IMigrationHistory)?.InvalidateHistory();
        }
    }
}
