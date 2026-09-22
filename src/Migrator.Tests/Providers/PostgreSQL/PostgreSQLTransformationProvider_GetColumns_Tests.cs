using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("PostgreSQL")]
public class PostgreSQLTransformationProvider_GetColumns_Tests : Generic_GetColumnsTestsBase
{
    [Test]
    public void RawSqlDefaultsRoundTripThroughMetadata() => RawDefaultRegression.AssertRoundTrip(Provider);

    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }
}
