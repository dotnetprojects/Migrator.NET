using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.PostgreSQL.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_TableExistsTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void TableExists_TableExists_Returns()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32)
        );

        // Act
        var tableExists = Provider.TableExists(testTableName);

        // Assert
        Assert.That(tableExists, Is.True);
    }

    [Test]
    public void TableExists_TableDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string myTableName = "MyTable";

        // Act
        var tableExists = Provider.TableExists(myTableName);

        // Assert
        Assert.That(tableExists, Is.False);
    }
}
