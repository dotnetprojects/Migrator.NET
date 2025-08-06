using System.Data;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_ViewExistsTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void ViewExists_ViewExists_Returns()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string myViewName = "MyView";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32)
        );

        Provider.ExecuteNonQuery($"CREATE VIEW {myViewName} AS SELECT {propertyName1} FROM {testTableName}");

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
