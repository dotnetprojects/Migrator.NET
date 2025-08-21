using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_AddIndex_Tests : GenericAddIndexTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }
}