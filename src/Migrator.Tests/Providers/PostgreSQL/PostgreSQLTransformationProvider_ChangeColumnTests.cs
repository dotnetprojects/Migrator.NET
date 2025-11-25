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
}
