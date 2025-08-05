

namespace Migrator.Tests.Database.Interfaces;

public interface IDatabaseIntegrationTestServiceFactory
{
    /// <summary>
    /// Creates a <see cref="IDatabaseIntegrationTestService"/> depending on the provider type (Oracle, PostgreSQL etc.).
    /// </summary>
    /// <param name="providerType"></param>
    /// <returns></returns>
    IDatabaseIntegrationTestService Create(DatabaseProviderType providerType);
}
