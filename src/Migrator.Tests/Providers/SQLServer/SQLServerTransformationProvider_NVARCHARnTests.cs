using System.Data;
using Microsoft.Data.SqlClient;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLServer.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SqlServerTransformationProvider_NVARCHARnTests : SQLServerTransformationProviderTestBase
{
    [Test]
    public void AddTableWithFixedLengthEqualTo4000Characters_ShouldCreateNVARCHAR4000()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.String, 4000)
        );

        var stringLength4001 = new string('A', 4001);

        // Act
        var exception = Assert.Throws<SqlException>(() => Provider.Insert(testTableName, [propertyName1], [stringLength4001]));

        Assert.That(exception.Errors[0].Message, Does.Contain("String or binary data would be truncated"));
    }

    [Test]
    public void AddTableWithFixedLengthGreaterThan4000Characters_ShouldCreateNVARCHARMAX()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";


        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.String, 4001)
        );

        var stringLength5000 = new string('A', 5000);

        // Act
        Provider.Insert(testTableName, [propertyName1], [stringLength5000]);
    }
}
