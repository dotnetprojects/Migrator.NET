using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_UpdateFromTableToTableTests : Generic_UpdateFromTableToTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }
}
