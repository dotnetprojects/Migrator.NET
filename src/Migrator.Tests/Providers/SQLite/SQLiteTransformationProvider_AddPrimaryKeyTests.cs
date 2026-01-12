using System;
using System.Data;
using System.Data.SQLite;
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

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.String),
            new Column(columnName2, DbType.Int32),
            new Column(columnName3, DbType.Int32));

        // Act
        Provider.AddPrimaryKey(name: primaryKeyName, table: tableName, columns: [columnName3, columnName2]);

        // Assert
        var createTableScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);

        Assert.That(createTableScript, Does.Contain("PRIMARY KEY (TestColumn3, TestColumn2))"));
    }

    [Test]
    public void AddPrimaryKey_ColumnGuidNonComposite_ThrowsOnDuplicatesAndNulls()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        var guid = Guid.NewGuid();

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Guid, ColumnProperty.PrimaryKey)
        );

        Provider.Insert(tableName, [columnName1], [guid]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1], [guid]));
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1], [null]));
    }

    [Test]
    public void AddPrimaryKey_ColumnGuidComposite_ThrowsOnDuplicatesAndNulls()
    {
        // Arrange
        const string columnName1 = "TestColumn1";
        const string columnName2 = "TestColumn2";
        const string tableName = "TestTable";
        const string primaryKeyName = $"PK_{tableName}";
        var guid = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Guid),
            new Column(columnName2, DbType.Guid));

        // Act
        Provider.AddPrimaryKey(name: primaryKeyName, table: tableName, columns: [columnName1, columnName2]);

        // This is a normal SQLite behavior! 
        // NULL != NULL
        // (A, NULL) != (A, NULL)
        // Duplicates! You need to set NotNull if you want to prevent it!
        Provider.Insert(tableName, [columnName1, columnName2], [guid, null]);

        Provider.Insert(tableName, [columnName1, columnName2], [guid, null]);
        Provider.Insert(tableName, [columnName1, columnName2], [guid, null]);

        Provider.Insert(tableName, [columnName1, columnName2], [null, guid]);
        Provider.Insert(tableName, [columnName1, columnName2], [null, guid]);

        Provider.Insert(tableName, [columnName1, columnName2], [guid2, guid2]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [guid2, guid2]));
    }
}
