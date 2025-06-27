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
        _provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        _provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (44444)");
        _provider.AddForeignKey("FK name is not supported by SQLite", parentTable: "Test", parentColumn: "Id", childTable: "TestTwo", childColumn: "TestId", ForeignKeyConstraintType.Cascade);

        // Act
        var result = ((SQLiteTransformationProvider)_provider).CheckForeignKeyIntegrity();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CheckForeignKeyIntegrity_IntegrityOk_ReturnsTrue()
    {
        // Arrange
        AddTableWithPrimaryKey();
        _provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        _provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");
        _provider.AddForeignKey("FK name is not supported by SQLite", parentTable: "Test", parentColumn: "Id", childTable: "TestTwo", childColumn: "TestId", ForeignKeyConstraintType.Cascade);

        // Act
        var result = ((SQLiteTransformationProvider)_provider).CheckForeignKeyIntegrity();

        // Assert
        Assert.That(result, Is.True);
    }
}
