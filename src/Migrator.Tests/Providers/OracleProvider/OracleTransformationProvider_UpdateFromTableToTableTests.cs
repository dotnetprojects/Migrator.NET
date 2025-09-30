using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_UpdateFromTableToTableTests : Generic_UpdateFromTableToTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }
}