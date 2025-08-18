using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.PostgreSQL.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
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
