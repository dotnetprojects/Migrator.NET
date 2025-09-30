using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_UpdateFromTableToTableTests : Generic_UpdateFromTableToTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
    }
}