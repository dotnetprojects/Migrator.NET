using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_ChangeColumnTests : Generic_ChangeColumnTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }

    [Test]
    public void ChangeColumn_DateTimeToDateTime2_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";

        Provider.AddTable(tableName, new Column(columnName, DbType.DateTime, ColumnProperty.NotNull));
        var columnBefore = Provider.GetColumnByName(tableName, columnName);

        // Act
        Provider.ChangeColumn(tableName, new Column(columnName, DbType.DateTime2, ColumnProperty.NotNull));

        // Assert
        var columnAfter = Provider.GetColumnByName(tableName, columnName);

        Assert.That(columnBefore.Type == DbType.DateTime);
        Assert.That(columnAfter.Type == DbType.DateTime2);
    }

    [Test, Ignore("This issue is not yet fixed. See https://github.com/dotnetprojects/Migrator.NET/issues/132")]
    public void ChangeColumn_WithUniqueThenReChangeToNonUnique_UniqueConstraintShouldBeRemoved()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32, ColumnProperty.NotNull));

        // Act
        Provider.ChangeColumn(tableName, new Column(columnName, DbType.Int32, ColumnProperty.NotNull | ColumnProperty.Unique));
        Provider.ChangeColumn(tableName, new Column(columnName, DbType.Int32, ColumnProperty.NotNull));

        // Assert
        var indexes = Provider.GetIndexes(tableName);
        Assert.That(indexes, Is.Empty);
    }
}