using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_AddTable_Tests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
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

    [Test]
    public void AddTable_NotNull_Success()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";

        // Act
        Provider.AddTable(tableName,
            new Column(column1Name, DbType.Int32, ColumnProperty.NotNull)
        );

        // Assert
        var column1 = Provider.GetColumnByName(tableName, column1Name);

        Assert.That(column1.ColumnProperty.HasFlag(ColumnProperty.NotNull), Is.True);
    }
}