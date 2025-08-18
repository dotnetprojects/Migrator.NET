using System.Threading.Tasks;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL.Base;

[TestFixture]
[Category("Postgre")]
public abstract class PostgreSQLTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }
}
