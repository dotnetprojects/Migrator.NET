using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_AddTableTests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }

    [Test]
    public void AddTableWithCompoundPrimaryKey()
    {
        Provider.AddTable("Test",
            new Column("PersonId", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column("AddressId", DbType.Int32, ColumnProperty.PrimaryKey)
        );

        Assert.That(Provider.TableExists("Test"), Is.True, "Table doesn't exist");
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True, "Constraint doesn't exist");
    }
}
