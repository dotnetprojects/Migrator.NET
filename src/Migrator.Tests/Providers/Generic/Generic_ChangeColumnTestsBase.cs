using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_ChangeColumnTestsBase : TransformationProviderBase
{
    [Test]
    public void ChangeColumn_NotNullAndNullToNotNull_Success()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";

        // Act
        Provider.AddTable(tableName,
            new Column(column1Name, DbType.DateTime, ColumnProperty.NotNull),
            new Column(column2Name, DbType.DateTime, ColumnProperty.Null)
        );

        // Assert
        Provider.ChangeColumn(tableName, new Column(column1Name, DbType.DateTime2, ColumnProperty.NotNull));
        Provider.ChangeColumn(tableName, new Column(column2Name, DbType.DateTime2, ColumnProperty.NotNull));
        var column1 = Provider.GetColumnByName(tableName, column1Name);
        var column2 = Provider.GetColumnByName(tableName, column2Name);

        Assert.That(column1.ColumnProperty.HasFlag(ColumnProperty.NotNull), Is.True);
        Assert.That(column2.ColumnProperty.HasFlag(ColumnProperty.NotNull), Is.True);
    }

    [Test, Ignore("Not yet implemented. See issue https://github.com/dotnetprojects/Migrator.NET/issues/139")]
    public void ChangeColumn_RemoveDefaultValue_Success()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";

        var testTime = new DateTime(2025, 5, 5, 5, 5, 5, DateTimeKind.Utc);

        Provider.AddTable(tableName,
            new Column(name: column1Name, type: DbType.Int32, property: ColumnProperty.NotNull),
            new Column(name: column2Name, type: DbType.DateTime2, property: ColumnProperty.Null, defaultValue: testTime)
        );

        // Act
        Provider.Insert(table: tableName, [column1Name], [1]);
        Provider.ChangeColumn(table: tableName, column: new Column(name: column2Name, type: DbType.DateTime2, property: ColumnProperty.Null));

        // Assert
        Provider.Insert(table: tableName, [column1Name], [2]);

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd: cmd, table: tableName, columns: [column1Name, column2Name]);

        List<(int, DateTime)> records = [];

        while (reader.Read())
        {
            records.Add((reader.GetInt32(0), reader.GetDateTime(1)));
        }

        Assert.That(records.Count, Is.EqualTo(2));
        Assert.That(records.Single(x => x.Item1 == 1).Item2, Is.EqualTo(testTime));
        Assert.That(records.Single(x => x.Item1 == 2).Item2, Is.Null);
    }
}