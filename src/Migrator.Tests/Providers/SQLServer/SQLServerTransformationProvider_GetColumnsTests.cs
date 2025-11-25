using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_GetColumnsTests : Generic_GetColumnsTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }

    [Test]
    public void GetColumns_GetIdentity_Succeeds()
    {
        // Arrange
        var tableName1 = "Table1";
        var tableName2 = "Table2";
        var tableName3 = "Table3";
        var tableName4 = "Table4";
        var columnName1 = "ColumnName1";

        Provider.ExecuteNonQuery($"CREATE TABLE {tableName1} ({columnName1} INT IDENTITY(1,1) PRIMARY KEY)");
        Provider.ExecuteNonQuery($"CREATE TABLE {tableName2} ({columnName1} INT PRIMARY KEY)");

        Provider.AddTable(name: tableName3, new Column(columnName1, DbType.Int32, ColumnProperty.Identity | ColumnProperty.PrimaryKey));
        Provider.AddTable(name: tableName4, new Column(columnName1, DbType.Int32, ColumnProperty.PrimaryKey));

        // Act
        var columnTable1 = Provider.GetColumnByName(table: tableName1, column: columnName1);
        var columnTable2 = Provider.GetColumnByName(table: tableName2, column: columnName1);
        var columnTable3 = Provider.GetColumnByName(table: tableName3, column: columnName1);
        var columnTable4 = Provider.GetColumnByName(table: tableName4, column: columnName1);

        // Assert
        Assert.That(columnTable1.IsIdentity, Is.True);
        Assert.That(columnTable2.IsIdentity, Is.False);
        Assert.That(columnTable3.IsIdentity, Is.True);
        Assert.That(columnTable4.IsIdentity, Is.False);
    }
}
