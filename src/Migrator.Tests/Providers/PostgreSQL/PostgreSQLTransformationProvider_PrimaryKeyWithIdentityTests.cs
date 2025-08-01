using System;
using System.Data;
using Migrator.Framework;
using Npgsql;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_PrimaryKeyWithIdentityTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void AddTableWithPrimaryKeyIdentity_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKeyWithIdentity),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unsigned)
        );

        // Act
        Provider.Insert(testTableName, [propertyName2], [1]);
        Provider.Insert(testTableName, [propertyName2], [1]);

        // Assert
        using (var command = Provider.GetCommand())
        {
            using var reader = Provider.ExecuteQuery(command, $"SELECT max({propertyName1}) as max from {testTableName}");
            reader.Read();

            var primaryKeyValue = reader.GetInt32(reader.GetOrdinal("max"));
            Assert.That(primaryKeyValue, Is.EqualTo(2));
        }

        // Act II
        var exception = Assert.Throws<PostgresException>(() => Provider.Insert(testTableName, [propertyName1, propertyName2], [1, 888]));

        // Assert II
        Assert.That(exception.SqlState, Is.EqualTo("428C9"));
    }
}
