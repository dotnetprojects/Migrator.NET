using System.Threading.Tasks;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_GetColumnsTests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }
}
