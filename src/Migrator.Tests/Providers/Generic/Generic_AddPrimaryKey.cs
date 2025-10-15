using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

[TestFixture]
public abstract class Generic_AddPrimaryTestsBase : TransformationProviderBase
{
    [Test]
    public void AddPrimaryKey_IdentityColumnWithData_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";
        const string columnName2 = "TestColumn2";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int32, property: ColumnProperty.Identity | ColumnProperty.PrimaryKey),
            new Column(columnName2, DbType.String)
        );

        // Act
        Provider.Insert(tableName, [columnName2], ["Hello"]);
        Provider.Insert(tableName, [columnName2], ["Hello2"]);

        // Assert

        List<(int, string)> list = [];

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, tableName, [columnName1, columnName2]);

        while (reader.Read())
        {
            list.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        list = list.OrderBy(x => x.Item1).ToList();

        Assert.That(list[0].Item1, Is.EqualTo(1));
        Assert.That(list[1].Item1, Is.EqualTo(2));
    }
}