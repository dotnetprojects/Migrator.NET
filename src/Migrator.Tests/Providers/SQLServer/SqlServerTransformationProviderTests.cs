using System.Data;
using Migrator.Providers;
using Migrator.Providers.SqlServer;
using Migrator.Tests.Providers.SQLServer.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SqlServerTransformationProviderTests : SQLServerTransformationProviderTestBase
{
    [Test]
    public void ByteColumnWillBeCreatedAsBlob()
    {
        Provider.AddColumn("TestTwo", "BlobColumn", DbType.Byte);
        Assert.That(Provider.ColumnExists("TestTwo", "BlobColumn"), Is.True);
    }

    [Test]
    public void InstanceForProvider()
    {
        var localProv = Provider["sqlserver"];
        Assert.That(localProv is SqlServerTransformationProvider, Is.True);

        var localProv2 = Provider["foo"];
        Assert.That(localProv2 is NoOpTransformationProvider, Is.True);
    }

    [Test]
    public void QuoteCreatesProperFormat()
    {
        var dialect = new SqlServerDialect();

        Assert.That("[foo]", Is.EqualTo(dialect.Quote("foo")));
    }

    [Test]
    public void TableExistsShouldWorkWithBracketsAndSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("[dbo].[TestTwo]"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("dbo.TestTwo"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithTableNamesWithBracket()
    {
        Assert.That(Provider.TableExists("[TestTwo]"), Is.True);
    }
}
