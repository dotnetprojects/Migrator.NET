using System;
using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_ChangeColumnTests : Generic_ChangeColumnTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }

    [Test]
    public void ChangeColumn_DateTimeOffsetToDateTime_Success()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";
        var dateTimeDefaultValue = new DateTime(2025, 5, 4, 3, 2, 1, DateTimeKind.Utc);
        var dateTimeInsert = new DateTime(2001, 2, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        // Act
        Provider.AddTable(tableName,
            new Column(column1Name, DbType.Int32, ColumnProperty.Null),
            new Column(column2Name, DbType.DateTimeOffset, ColumnProperty.Null, defaultValue: dateTimeDefaultValue)
        );

        Provider.Insert(table: tableName, columns: [column2Name], values: [dateTimeInsert]);

        // Assert
        Provider.ChangeColumn(tableName, new Column(column2Name, DbType.DateTime2, ColumnProperty.NotNull));
        var column2 = Provider.GetColumnByName(tableName, column2Name);

        Assert.That(column2.MigratorDbType, Is.EqualTo(MigratorDbType.DateTime2));
        Assert.That(column2.DefaultValue, Is.Null);
    }

    [Test]
    public void ChangeColumn_DateTimeOffsetToDateTimeGetDefaultValueAndReuseIt_DefaultValueIsEqualAndValueIsEqual()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";
        var dateTimeOffsetDefaultValue = new DateTimeOffset(2022, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));
        var dateTimeOffsetInsert = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));

        Provider.AddTable(tableName,
            new Column(column1Name, DbType.Int32, ColumnProperty.Null),
            new Column(column2Name, DbType.DateTimeOffset, ColumnProperty.Null, defaultValue: dateTimeOffsetDefaultValue)
        );

        Provider.Insert(table: tableName, columns: [column2Name], values: [dateTimeOffsetInsert]);
        // Act

        var column2 = Provider.GetColumnByName(tableName, column2Name);
        Assert.That(((DateTimeOffset)column2.DefaultValue).UtcDateTime, Is.EqualTo(dateTimeOffsetDefaultValue.UtcDateTime));
        Provider.ChangeColumn(tableName, new Column(column2Name, DbType.DateTime2, ColumnProperty.NotNull, defaultValue: column2.DefaultValue));


        // Assert
        column2 = Provider.GetColumnByName(tableName, column2Name);

        // using var reader = Provider.Select(Provider.GetCommand(), what: column2Name, from: tableName);
        // var valueFromDatabase = reader.GetDateTime(0);

        Assert.That(column2.MigratorDbType, Is.EqualTo(MigratorDbType.DateTime2));
        Assert.That(column2.DefaultValue, Is.EqualTo(dateTimeOffsetDefaultValue.UtcDateTime));
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

        Provider.ExecuteNonQuery($"CREATE TABLE {tableName1} ({columnName1} INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY)");
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
