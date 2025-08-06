using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetPragmaTableInfoItemsTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void AddTable_NoNotNullColumn_NotNullIsFalse()
    {
        const string tableName = "MyTableName";
        const string columnName = "MyColumnName";

        // Arrange
        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32));
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);

        // Act
        var tableInfoItems = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);


        Assert.That(tableInfoItems.First(x => x.Name == columnName).NotNull, Is.False);
    }

    [Test]
    public void AddTable_NotNullColumn_NotNullIsTrue()
    {
        const string tableName = "MyTableName";
        const string columnName = "MyColumnName";

        // Arrange
        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32, ColumnProperty.NotNull));
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);

        // Act
        var tableInfoItems = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);


        Assert.That(tableInfoItems.First(x => x.Name == columnName).NotNull, Is.True);
    }
}