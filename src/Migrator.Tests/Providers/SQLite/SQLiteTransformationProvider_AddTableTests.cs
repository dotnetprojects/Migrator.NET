using System;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddTableTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void AddTable_UniqueOnly_ContainsNull()
    {
        const string tableName = "MyTableName";
        const string columnName = "MyColumnName";

        // Arrange/Act
        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32, ColumnProperty.Unique));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That("CREATE TABLE MyTableName (MyColumnName INTEGER NULL UNIQUE)", Is.EqualTo(createScript));

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);

        // It is no named unique so it is not listed in the Uniques list. Unique on column level is marked as obsolete.
        Assert.That(sqliteInfo.Uniques, Is.Empty);
    }

    [Test]
    public void AddTable_CompositePrimaryKey_ContainsNull()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1, System.Data.DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(columnName2, System.Data.DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.NotNull)
        );

        Provider.ExecuteNonQuery($"INSERT INTO {tableName} ({columnName1}, {columnName2}) VALUES (1,1)");
        Assert.Throws<MigrationException>(() => Provider.ExecuteNonQuery($"INSERT INTO {tableName} ({columnName1}, {columnName2}) VALUES (1,1)"));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That("CREATE TABLE MyTableName (Column1 INTEGER NULL, Column2 INTEGER NOT NULL, PRIMARY KEY (Column1, Column2))", Is.EqualTo(createScript));

        var pragmaTableInfos = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);
        Assert.That(pragmaTableInfos.Single(x => x.Name == columnName1).NotNull, Is.False);
        Assert.That(pragmaTableInfos.Single(x => x.Name == columnName2).NotNull, Is.True);

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Columns.First().Name, Is.EqualTo(columnName1));
        Assert.That(sqliteInfo.Columns[1].Name, Is.EqualTo(columnName2));
    }

    [Test]
    public void AddTable_SinglePrimaryKey_ContainsNull()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1, System.Data.DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(columnName2, System.Data.DbType.Int32, ColumnProperty.NotNull)
        );

        Provider.ExecuteNonQuery($"INSERT INTO {tableName} ({columnName1}, {columnName2}) VALUES (1,1)");
        Assert.Throws<MigrationException>(() => Provider.ExecuteNonQuery($"INSERT INTO {tableName} ({columnName1}, {columnName2}) VALUES (1,2)"));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That("CREATE TABLE MyTableName (Column1 INTEGER NOT NULL PRIMARY KEY, Column2 INTEGER NOT NULL)", Is.EqualTo(createScript));

        var pragmaTableInfos = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);
        Assert.That(pragmaTableInfos.All(x => x.NotNull), Is.True);

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Columns.First().Name, Is.EqualTo(columnName1));
        Assert.That(sqliteInfo.Columns[1].Name, Is.EqualTo(columnName2));
    }

    [Test]
    public void AddTable_MiscellaneousColumns_Succeeds()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1, System.Data.DbType.Int32, ColumnProperty.NotNull | ColumnProperty.Identity | ColumnProperty.PrimaryKey),
            new Column(columnName2, System.Data.DbType.Int32, ColumnProperty.Null | ColumnProperty.Unique)
        );

        Provider.ExecuteNonQuery($"INSERT INTO {tableName} ({columnName1}, {columnName2}) VALUES (1,1)");
        Assert.Throws<MigrationException>(() => Provider.ExecuteNonQuery($"INSERT INTO {tableName} ({columnName1}, {columnName2}) VALUES (1,1)"));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That("CREATE TABLE MyTableName (Column1 INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Column2 INTEGER NULL UNIQUE)", Is.EqualTo(createScript));

        var pragmaTableInfos = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);
        Assert.That(pragmaTableInfos.First().NotNull, Is.True);
        Assert.That(pragmaTableInfos[1].NotNull, Is.False);

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Columns.First().Name, Is.EqualTo(columnName1));
        Assert.That(sqliteInfo.Columns[1].Name, Is.EqualTo(columnName2));
    }
}