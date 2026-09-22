using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetColumnsTests : Generic_GetColumnsTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
    }

    [Test]
    public void GetColumns_PrimaryAndUnique_ReturnsFalse()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Id",DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, "Id"),new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_" + tableName + "_" + "Id", "Id"));

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns.Single().IsNullable, Is.False);
        Assert.That(columns.Single().IsIdentity, Is.False);
        Assert.That(Provider.GetTableConstraints(tableName).OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Id" }));
    }

    [Test]
    public void GetColumns_Primary_ColumnPropertyOk()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Id",DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, "Id"));
        Provider.GetColumns(tableName);

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns.Single().IsNullable, Is.False);
        Assert.That(columns.Single().IsIdentity, Is.False);
    }

    [Test]
    public void GetColumns_PrimaryKeyOnTwoColumns_BothColumnsHavePrimaryKeyAndAreNotNull()
    {
        // Arrange
        const string tableName = "GetColumnsTest";

        Provider.AddTable(tableName,
            new Column("Id",DbType.Int32){IsNullable = false},
            new Column("Id2",DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, "Id", "Id2")        );

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns[0].IsNullable, Is.False);
        Assert.That(columns[1].IsNullable, Is.False);
    }

    [Test]
    public void GetColumns_AddUniqueConstraintWithTwoColumns_NoUniqueOnColumnLevel()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        const string column1Name = "Column1";
        const string column2Name = "Column2";
        const string constraintName = "ConstraintName";

        Provider.AddTable(tableName, new Column(column1Name, DbType.Int32), new Column(column2Name, DbType.Int32));

        Provider.AddUniqueConstraint(constraintName, tableName, column1Name, column2Name);

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns[0].IsNullable, Is.True);
    }

    [Test, Description("Add index. The index should be added and then being detected as index.")]
    public void GetSQLiteTableInfo_GetIndexesAndColumnsWithIndex_NoUniqueOnTheColumnsAndIndexExists()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Bla1", DbType.Int32), new Column("Bla2", DbType.Int32));
        Provider.AddIndex("IndexName", tableName, ["Bla1", "Bla2"]);

        // Act
        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);

        // Assert
        Assert.That(sqliteInfo.Columns[0].IsNullable, Is.True);
        Assert.That(sqliteInfo.Columns[1].IsNullable, Is.True);
        Assert.That(sqliteInfo.Uniques, Is.Empty);
        Assert.That(sqliteInfo.Indexes.Single().Unique, Is.False);
    }
}
