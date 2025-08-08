using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Npgsql;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumnsDefaultValueTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void GetColumns_DataTypeResolveSucceeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string dateTimeColumnName1 = "datetimecolumn1";
        const string dateTimeColumnName2 = "datetimecolumn2";
        const string decimalColumnName1 = "decimalcolumn";

        Provider.AddTable(testTableName,
            new Column(dateTimeColumnName1, DbType.DateTime),
            new Column(dateTimeColumnName2, DbType.DateTime2),
            new Column(decimalColumnName1, DbType.Decimal)
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        var dateTimeColumn1 = columns.Single(x => x.Name == dateTimeColumnName1);
        var dateTimeColumn2 = columns.Single(x => x.Name == dateTimeColumnName2);
        var decimalColumn1 = columns.Single(x => x.Name == decimalColumnName1);

        // Assert
        Assert.That(dateTimeColumn1.Type, Is.EqualTo(DbType.DateTime));
        Assert.That(dateTimeColumn1.Precision, Is.EqualTo(3));

        Assert.That(dateTimeColumn2.Type, Is.EqualTo(DbType.DateTime2));
        Assert.That(dateTimeColumn2.Precision, Is.EqualTo(6));

        Assert.That(decimalColumn1.Type, Is.EqualTo(DbType.Decimal));

        // Assert
        // using (var command = Provider.GetCommand())
        // {
        //     using var reader = Provider.ExecuteQuery(command, $"SELECT max({propertyName1}) as max from {testTableName}");
        //     reader.Read();

        //     var primaryKeyValue = reader.GetInt32(reader.GetOrdinal("max"));
        //     Assert.That(primaryKeyValue, Is.EqualTo(2));
        // }

        // // Act II
        // var exception = Assert.Throws<PostgresException>(() => Provider.Insert(testTableName, [propertyName1, propertyName2], [1, 888]));

        // // Assert II
        // Assert.That(exception.SqlState, Is.EqualTo("428C9"));

        throw new NotImplementedException();
    }
}
