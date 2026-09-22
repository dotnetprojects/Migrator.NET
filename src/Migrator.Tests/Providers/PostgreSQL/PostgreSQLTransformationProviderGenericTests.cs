using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("PostgreSQL")]
public class PostgreSQLTransformationProviderGenericTests : TransformationProviderGenericMiscConstraintBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();

        AddDefaultTable();
    }
}
