using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.SQLServer.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_TableExistsTests : SQLServerTransformationProviderTestBase
{
    [Test]
    public void TableExists_WithSchemaNameTableExists_Returns()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32)
        );

        // Act
        var tableExists = Provider.TableExists($"dbo.{testTableName}");

        // Assert
        Assert.That(tableExists, Is.True);
    }

    [Test]
    public void TableExists_NoSchemaNameTableExists_Returns()
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
        const string myTableName = "MyTableName";

        // Act
        var tableExists = Provider.TableExists(myTableName);

        // Assert
        Assert.That(tableExists, Is.False);
    }
}
