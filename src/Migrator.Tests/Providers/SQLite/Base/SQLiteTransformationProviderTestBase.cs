using System.Threading.Tasks;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite.Base;

[TestFixture]
[Category("SQLite")]
public abstract class SQLiteTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
        AddDefaultTable();
    }
}
