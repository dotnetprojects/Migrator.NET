using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.SQLServer.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_ViewExistsTests : SQLServerTransformationProviderTestBase
{
    [Test]
    public void ViewExists_WithSchemaNameViewExists_Returns()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string myViewName = "MyView";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32)
        );

        Provider.ExecuteNonQuery($"CREATE VIEW dbo.{myViewName} AS SELECT {propertyName1} FROM dbo.{testTableName}");

        // Act
        var viewExists = Provider.ViewExists($"dbo.{myViewName}");

        // Assert
        Assert.That(viewExists, Is.True);
    }

    [Test]
    public void ViewExists_NoSchemaNameViewExists_Returns()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string myViewName = "MyView";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32)
        );

        Provider.ExecuteNonQuery($"CREATE VIEW dbo.{myViewName} AS SELECT {propertyName1} FROM dbo.{testTableName}");

        // Act
        var viewExists = Provider.ViewExists(myViewName);

        // Assert
        Assert.That(viewExists, Is.True);
    }

    [Test]
    public void ViewExists_ViewDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string myViewName = "MyView";

        // Act
        var viewExists = Provider.ViewExists(myViewName);

        // Assert
        Assert.That(viewExists, Is.False);
    }
}
