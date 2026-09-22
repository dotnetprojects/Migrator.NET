using System.Data;
using System;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using Migrator.Tests.Providers.SQLServer.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SQLServer")]
public class SqlServerTransformationProviderTests : SQLServerTransformationProviderTestBase
{
    [Test]
    public void IndependentForeignKeyActionsCascadeUpdateAndSetNullOnDelete()
    {
        Provider.AddTable("ActionParent", new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.NotNull));
        Provider.AddTable("ActionChild", new Column("ParentId", DbType.Int32, ColumnProperty.Null));
        ((IForeignKeyActions)Provider).AddForeignKey("ActionForeignKey", "ActionChild", new[] { "ParentId" },
            "ActionParent", new[] { "Id" }, ForeignKeyConstraintType.SetNull, ForeignKeyConstraintType.Cascade);
        Provider.ExecuteNonQuery("INSERT INTO ActionParent VALUES (1); INSERT INTO ActionChild VALUES (1); UPDATE ActionParent SET Id=2 WHERE Id=1");
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT ParentId FROM ActionChild")), Is.EqualTo(2));
        Provider.ExecuteNonQuery("DELETE FROM ActionParent WHERE Id=2");
        Assert.That(Provider.ExecuteNullableScalar<int>("SELECT ParentId FROM ActionChild"), Is.Null);
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM ActionChild")), Is.EqualTo(1));
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
