using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SqlServerTransformationProviderGenericTests : TransformationProviderGenericMiscConstraintBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();

        AddDefaultTable();
    }

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
