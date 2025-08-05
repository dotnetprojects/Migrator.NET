using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.GuidServices.Interfaces;
using Migrator.Tests.Database.GuidServices;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Database.DerivedDatabaseIntegrationTestServices;
using System;
using DryIoc;
using Migrator.Test.Shared.Database;
using Migrator.Tests.Settings.Interfaces;
using Migrator.Tests.Settings;

namespace Migrator.Tests.Database;

public static class DatabaseCreationServiceRegistry
{
    public static void RegisterDatabaseIntegrationTestService(this IRegistrator container)
    {
        container.Register<IDatabaseIntegrationTestServiceFactory, DatabaseIntegrationTestServiceFactory>(reuse: Reuse.Transient);
        container.Register<IDatabaseNameService, DatabaseNameService>(reuse: Reuse.Transient);
        container.RegisterInstance(TimeProvider.System, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.Register<IGuidService, GuidService>(reuse: Reuse.Transient, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.Register<IDatabaseIntegrationTestService, OracleDatabaseIntegrationTestService>(serviceKey: DatabaseProviderType.Oracle);
        container.Register<IDatabaseIntegrationTestService, SQLiteDatabaseIntegrationTestService>(serviceKey: DatabaseProviderType.SQLite);
        container.Register<IDatabaseIntegrationTestService, PostgreSqlDatabaseIntegrationTestService>(serviceKey: DatabaseProviderType.Postgres);
        container.Register<IDatabaseIntegrationTestService, SqlServerDatabaseIntegrationTestService>(serviceKey: DatabaseProviderType.SQLServer);
        container.Register<IConfigurationReader, ConfigurationReader>(reuse: Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }
}
