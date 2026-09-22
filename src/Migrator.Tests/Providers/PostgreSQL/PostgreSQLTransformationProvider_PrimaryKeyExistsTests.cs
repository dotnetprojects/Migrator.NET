using Migrator.Tests.Providers.PostgreSQL.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("PostgreSQL")]
public class PostgreSQLTransformationProvider_PrimaryKeyExistsTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void CanAddPrimaryKey()
    {
        AddTable();
        AddPrimaryKey();
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True);
    }
}
