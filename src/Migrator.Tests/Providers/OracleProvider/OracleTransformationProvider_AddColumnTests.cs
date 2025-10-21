using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_AddColumn_Tests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }

    [Test]
    public void AddTable_NotNull_OtherColumnStillNotNull()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";


        Provider.AddTable(tableName,
            new Column(column1Name, DbType.Int32, ColumnProperty.NotNull)
        );

        // Act
        Provider.AddColumn(table: tableName, column: new Column(column2Name, DbType.DateTime, ColumnProperty.NotNull));


        // Assert
        var column1 = Provider.GetColumnByName(tableName, column1Name);
        var column2 = Provider.GetColumnByName(tableName, column2Name);

        Assert.That(column1.ColumnProperty.HasFlag(ColumnProperty.NotNull), Is.True);
        Assert.That(column2.ColumnProperty.HasFlag(ColumnProperty.NotNull), Is.True);
    }
}