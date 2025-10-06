using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddPrimaryTests : Generic_AddPrimaryTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
    }

    [Test]
    public void AddPrimaryKey_ColumnsInOtherOrderThanInColumnsList_Success()
    {
        // Arrange
        const string columnName1 = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string columnName3 = "TestColumn3";
        const string tableName = "TestTable";
        const string primaryKeyName = $"PK_{tableName}";

        Provider.AddTable(tableName, new Column(columnName1, DbType.String), new Column(columnName2, DbType.Int32), new Column(columnName3, DbType.Int32));

        // Act
        Provider.AddPrimaryKey(name: primaryKeyName, table: tableName, columns: [columnName3, columnName2]);

        // Assert
        var createTableScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);

        Assert.That(createTableScript, Does.Contain("PRIMARY KEY (TestColumn3, TestColumn2))"));
    }
}
