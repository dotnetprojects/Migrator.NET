using DryIoc;
using Migrator.Tests.Database.Interfaces;

namespace Migrator.Tests.Database;

public class DatabaseIntegrationTestServiceFactory(IResolver resolver) : IDatabaseIntegrationTestServiceFactory
{
    public IDatabaseIntegrationTestService Create(DatabaseProviderType providerType)
    {
        return resolver.Resolve<IDatabaseIntegrationTestService>(serviceKey: providerType);
    }
}