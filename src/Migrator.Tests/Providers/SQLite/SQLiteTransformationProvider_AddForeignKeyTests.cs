using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddForeignKeyTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void AddForeignKey()
    {
        // Arrange
        AddTableWithPrimaryKey();
        _provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        _provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");

        // Act
        _provider.AddForeignKey("FK name is not supported by SQLite", parentTable: "Test", parentColumn: "Id", childTable: "TestTwo", childColumn: "TestId", ForeignKeyConstraintType.Cascade);

        // Assert
        var foreignKeyConstraints = ((SQLiteTransformationProvider)_provider).GetForeignKeyConstraints("TestTwo");
        var tableSQLCreateScript = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript("TestTwo");

        Assert.That(foreignKeyConstraints.Single().Name, Is.Null);
        Assert.That(foreignKeyConstraints.Single().ChildTable, Is.EqualTo("TestTwo"));
        Assert.That(foreignKeyConstraints.Single().ParentTable, Is.EqualTo("Test"));
        Assert.That(foreignKeyConstraints.Single().ChildColumns.Single(), Is.EqualTo("TestId"));
        Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("Id"));
        // Cascade is not supported

        Assert.That(tableSQLCreateScript, Does.Contain("CREATE TABLE \"TestTwo\""));
        Assert.That(tableSQLCreateScript, Does.Contain(", FOREIGN KEY (TestId) REFERENCES Test(Id))"));

        var result = ((SQLiteTransformationProvider)_provider).CheckForeignKeyIntegrity();
        Assert.That(result, Is.True);
    }

    [Test]
    public void AddForeignKey_RenameParentColumWithForeignKeyAndData_ForeignKeyPointsToRenamedColumn()
    {
        // Arrange
        AddTableWithPrimaryKey();
        _provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        _provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");

        // Act
        _provider.AddForeignKey("FK name is not supported by SQLite", parentTable: "Test", parentColumn: "Id", childTable: "TestTwo", childColumn: "TestId", ForeignKeyConstraintType.Cascade);

        // Rename column in parent
        _provider.RenameColumn("Test", "Id", "IdNew");

        // Assert
        var foreignKeyConstraints = ((SQLiteTransformationProvider)_provider).GetForeignKeyConstraints("TestTwo");
        var tableSQLCreateScript = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript("TestTwo");

        Assert.That(tableSQLCreateScript, Does.Contain("CREATE TABLE \"TestTwo\""));
        Assert.That(tableSQLCreateScript, Does.Contain(", FOREIGN KEY (TestId) REFERENCES Test(IdNew))"));
        Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("IdNew"));

        var result = ((SQLiteTransformationProvider)_provider).CheckForeignKeyIntegrity();
        Assert.That(result, Is.True);
    }
}
