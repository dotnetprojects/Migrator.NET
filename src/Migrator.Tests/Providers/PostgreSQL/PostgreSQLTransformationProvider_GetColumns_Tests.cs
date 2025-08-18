using System.Threading.Tasks;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumns_Tests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }
}
