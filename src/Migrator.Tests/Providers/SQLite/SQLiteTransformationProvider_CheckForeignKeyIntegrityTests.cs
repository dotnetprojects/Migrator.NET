using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_CheckForeignKeyIntegrityTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void CheckForeignKeyIntegrity_IntegrityViolated_ReturnsFalse()
    {
        // Arrange
        AddTableWithPrimaryKey();
        Provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        Provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (44444)");
        Provider.AddForeignKey(name: "FK name is not supported by SQLite", childTable: "TestTwo", childColumn: "TestId", parentTable: "Test", parentColumn: "Id", constraint: ForeignKeyConstraintType.Cascade);

        // Act
        var result = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CheckForeignKeyIntegrity_IntegrityOk_ReturnsTrue()
    {
        // Arrange
        AddTableWithPrimaryKey();
        Provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        Provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");
        Provider.AddForeignKey(name: "FK name is not supported by SQLite", childTable: "TestTwo", childColumn: "TestId", parentTable: "Test", parentColumn: "Id", constraint: ForeignKeyConstraintType.Cascade);

        // Act
        var result = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();

        // Assert
        Assert.That(result, Is.True);
    }
}
