using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_UpdateFromTableToTableTests : Generic_UpdateFromTableToTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }
}
