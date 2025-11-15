using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_ChangeColumnTests : Generic_ChangeColumnTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }
}
