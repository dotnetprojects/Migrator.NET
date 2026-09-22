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
    public void AddColumn_PrecisionAndScale_PersistFractionalValue()
    {
        Provider.AddTable("PrecisionRoundTrip", new Column("Id", DbType.Int32));
        Provider.AddColumn("PrecisionRoundTrip", new Column("Amount", DbType.Decimal) { Precision = 12, Scale = 4 });
        Provider.Insert("PrecisionRoundTrip", new[] { "Id", "Amount" }, new object[] { 1, 12.3456m });
        using var command = Provider.CreateCommand();
        using var reader = Provider.Select(command, "PrecisionRoundTrip", new[] { "Amount" });
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetDecimal(0), Is.EqualTo(12.3456m));
    }

    [Test]
    public void ChangeColumn_NotNullAndNullToNotNull_Success()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";

        // Act
        Provider.AddTable(tableName,
            new Column(column1Name,DbType.DateTime){IsNullable = false},
            new Column(column2Name,DbType.DateTime)        );

        // Assert
        Provider.ChangeColumn(tableName, new Column(column1Name,DbType.DateTime2){IsNullable = false});
        Provider.ChangeColumn(tableName, new Column(column2Name,DbType.DateTime2){IsNullable = false});
        var column1 = Provider.ReadLegacyColumn(tableName, column1Name);
        var column2 = Provider.ReadLegacyColumn(tableName, column2Name);

        Assert.That(column1.IsNullable, Is.False);
        Assert.That(column2.IsNullable, Is.False);
    }

    [Test]
    public void ChangeColumn_RemoveDefaultValue_Success()
    {
        // Arrange
        var tableName = "TableName";
        var column1Name = "Column1";
        var column2Name = "Column2";

        var testTime = new DateTime(2025, 5, 5, 5, 5, 5, DateTimeKind.Utc);

        Provider.AddTable(tableName,
            new Column(name: column1Name,type: DbType.Int32){IsNullable = false},
            new Column(name: column2Name,type: DbType.DateTime2,defaultValue: testTime)        );

        // Act
        Provider.Insert(table: tableName, [column1Name], [1]);
        Provider.ChangeColumn(table: tableName, column: new Column(name: column2Name,type: DbType.DateTime2));

        // Assert
        Provider.Insert(table: tableName, [column1Name], [2]);

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd: cmd, table: tableName, columns: [column1Name, column2Name]);

        List<(int, DateTime?)> records = [];

        while (reader.Read())
        {
            records.Add((reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1)));
        }

        Assert.That(records.Count, Is.EqualTo(2));
        Assert.That(records.Single(x => x.Item1 == 1).Item2, Is.EqualTo(testTime));
        Assert.That(records.Single(x => x.Item1 == 2).Item2, Is.Null);
    }
}