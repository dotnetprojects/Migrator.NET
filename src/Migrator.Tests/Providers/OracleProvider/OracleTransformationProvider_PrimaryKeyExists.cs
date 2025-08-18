using System.Threading.Tasks;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_PrimaryKeyExistsTests : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }

    [Test]
    public void CanAddPrimaryKey()
    {
        AddTable();
        AddPrimaryKey();
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True);
    }
}