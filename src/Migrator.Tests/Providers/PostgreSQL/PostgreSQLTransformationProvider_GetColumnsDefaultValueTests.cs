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
        const string dateTimeColumnName = "datetimecolumn";
        const string decimalColumnName = "decimalcolumn";

        Provider.AddTable(testTableName,
            new Column(dateTimeColumnName, DbType.DateTime2),
            new Column(decimalColumnName, DbType.Decimal)
        );

        // Act
        var dateTimeColumn = Provider.GetColumns(testTableName).Single(x => x.Name.Equals(dateTimeColumnName, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.That(dateTimeColumn.Type, Is.EqualTo(DbType.DateTime2));

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
