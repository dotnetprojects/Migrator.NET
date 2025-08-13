using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetColumnsTests : TransformationProvider_GetColumns_GenericTests
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
    }

    [Test]
    public void GetColumns_UniqueButNotPrimaryKey_ReturnsFalse()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Id", System.Data.DbType.Int32, ColumnProperty.Unique));

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns.Single().ColumnProperty, Is.EqualTo(ColumnProperty.Null | ColumnProperty.Unique));
    }

    [Test]
    public void GetColumns_PrimaryAndUnique_ReturnsFalse()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Id", System.Data.DbType.Int32, ColumnProperty.Unique | ColumnProperty.PrimaryKey));

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns.Single().ColumnProperty, Is.EqualTo(
            ColumnProperty.NotNull |
            ColumnProperty.Identity |
            ColumnProperty.Unique |
            ColumnProperty.PrimaryKey));
    }

    [Test]
    public void GetColumns_Primary_ColumnPropertyOk()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Id", System.Data.DbType.Int32, ColumnProperty.PrimaryKey));
        Provider.GetColumns(tableName);

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns.Single().ColumnProperty, Is.EqualTo(ColumnProperty.NotNull |
            ColumnProperty.Identity |
            ColumnProperty.PrimaryKey));
    }

    [Test]
    public void GetColumns_PrimaryKeyOnTwoColumns_BothColumnsHavePrimaryKeyAndAreNotNull()
    {
        // Arrange
        const string tableName = "GetColumnsTest";

        Provider.AddTable(tableName,
            new Column("Id", System.Data.DbType.Int32, ColumnProperty.PrimaryKey),
            new Column("Id2", System.Data.DbType.Int32, ColumnProperty.PrimaryKey)
        );

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns[0].ColumnProperty, Is.EqualTo(ColumnProperty.PrimaryKey | ColumnProperty.Null));
        Assert.That(columns[1].ColumnProperty, Is.EqualTo(ColumnProperty.PrimaryKey | ColumnProperty.Null));
    }

    [Test]
    public void GetColumns_AddUniqueWithTwoColumns_NoUniqueOnColumnLevel()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Bla1", System.Data.DbType.Int32), new Column("Bla2", System.Data.DbType.Int32));

        Provider.AddUniqueConstraint("IndexName", tableName, "Bla1", "Bla2");

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns[0].ColumnProperty, Is.EqualTo(ColumnProperty.Null));
    }

    [Test, Description("Add index. Should be added and detected as index")]
    public void GetSQLiteTableInfo_GetIndexesAndColumnsWithIndex_NoUniqueOnTheColumnsAndIndexExists()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Bla1", System.Data.DbType.Int32), new Column("Bla2", System.Data.DbType.Int32));
        Provider.AddIndex("IndexName", tableName, ["Bla1", "Bla2"]);

        // Act
        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);

        // Assert
        Assert.That(sqliteInfo.Columns[0].ColumnProperty, Is.EqualTo(ColumnProperty.Null));
        Assert.That(sqliteInfo.Columns[1].ColumnProperty, Is.EqualTo(ColumnProperty.Null));
        Assert.That(sqliteInfo.Uniques, Is.Empty);
        Assert.That(sqliteInfo.Indexes.Single().Unique, Is.False);
    }
}
