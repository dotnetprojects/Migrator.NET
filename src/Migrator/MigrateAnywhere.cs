using System;
using System.Collections.Generic;
using Migrator.Framework;
using Migrator.Providers;
using System.Reflection;

namespace Migrator
{
    /// <summary>
    /// Description of MigrateAnywhere.
    /// </summary>
    public class MigrateAnywhere : BaseMigrate
    {
        bool _goForward;

        public MigrateAnywhere(List<long> availableMigrations, ITransformationProvider provider, ILogger logger)
            : base(availableMigrations, provider, logger)
        {
            _current = 0;
            if (provider.AppliedMigrations.Count > 0)
            {
                _current = provider.AppliedMigrations[provider.AppliedMigrations.Count - 1];
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
            _provider.BeginTransaction();
#if NETSTANDARD
			var attr = migration.GetType().GetTypeInfo().GetCustomAttribute<MigrationAttribute>();
#else
            var attr = (MigrationAttribute)Attribute.GetCustomAttribute(migration.GetType(), typeof(MigrationAttribute));
#endif


            if (_provider.AppliedMigrations.Contains(attr.Version))
            {
                RemoveMigration(migration, attr);
            }
            else
            {
                ApplyMigration(migration, attr);
            }
        }

        void ApplyMigration(IMigration migration, MigrationAttribute attr)
        {
            // we're adding this one
            _logger.MigrateUp(Current, migration.Name);
            if (!DryRun)
            {
                var tProvider = _provider as TransformationProvider;
                if (tProvider != null)
                    tProvider.CurrentMigration = migration;

                migration.Up();
                _provider.MigrationApplied(attr.Version, attr.Scope);
                _provider.Commit();
                migration.AfterUp();
            }
        }

        void RemoveMigration(IMigration migration, MigrationAttribute attr)
        {
            // we're removing this one
            _logger.MigrateDown(Current, migration.Name);
            if (!DryRun)
            {
                var tProvider = _provider as TransformationProvider;
                if (tProvider != null)
                    tProvider.CurrentMigration = migration;

                migration.Down();
                _provider.MigrationUnApplied(attr.Version, attr.Scope);
                _provider.Commit();
                migration.AfterDown();
            }
        }
    }
}