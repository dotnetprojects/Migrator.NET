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
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.DateTime2, new DateTime(1980, 1, 1)),
            new Column(propertyName2, DbType.Decimal)
        );

        // Act
        var column = Provider.GetColumns(testTableName).Single(x => x.Name.Equals(propertyName1, StringComparison.OrdinalIgnoreCase));
        Provider.Insert(testTableName, [propertyName2], [3.448484]);

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
