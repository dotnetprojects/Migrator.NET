using System.Data;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumnContentSizeTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void GetColumnContentSize_DefaultValues_Succeeds()
    {
        // Arrange
        const string testTableName = "testtable";

        const string stringColumnName = "stringcolumn";

        Provider.AddTable(testTableName,
            new Column(stringColumnName, DbType.String, 5000)
        );

        Provider.Insert(testTableName, [stringColumnName], [new string('A', 44)]);
        Provider.Insert(testTableName, [stringColumnName], [new string('B', 444)]);
        Provider.Insert(testTableName, [stringColumnName], [new string('C', 4444)]);

        // Act
        var columnContentSize = Provider.GetColumnContentSize(testTableName, stringColumnName);

        // Assert
        Assert.That(columnContentSize, Is.EqualTo(4444));
    }
}
