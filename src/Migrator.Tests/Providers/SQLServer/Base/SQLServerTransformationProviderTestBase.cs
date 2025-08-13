using System.Threading.Tasks;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer.Base;

[TestFixture]
[Category("SQLServer")]
public abstract class SQLServerTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();

        AddDefaultTable();
    }
}
