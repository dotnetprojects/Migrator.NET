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
        Provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        Provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");

        // Act
        Provider.AddForeignKey(name: "FKName", childTable: "TestTwo", childColumn: "TestId", parentTable: "Test", parentColumn: "Id", constraint: ForeignKeyConstraintType.Cascade);

        // Assert
        var foreignKeyConstraints = ((SQLiteTransformationProvider)Provider).GetForeignKeyConstraints("TestTwo");
        var tableSQLCreateScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("TestTwo");

        Assert.That(foreignKeyConstraints.Single().Name, Is.EqualTo("FKName"));
        Assert.That(foreignKeyConstraints.Single().ChildTable, Is.EqualTo("TestTwo"));
        Assert.That(foreignKeyConstraints.Single().ParentTable, Is.EqualTo("Test"));
        Assert.That(foreignKeyConstraints.Single().ChildColumns.Single(), Is.EqualTo("TestId"));
        Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("Id"));

        // Cascade is not supported in this migrator see https://github.com/dotnetprojects/Migrator.NET/issues/33
        // TODO add cascade tests as soon as it is supported.

        Assert.That(tableSQLCreateScript, Does.Contain("CREATE TABLE \"TestTwo\""));
        Assert.That(tableSQLCreateScript, Does.Contain(", CONSTRAINT FKName FOREIGN KEY (TestId) REFERENCES Test(Id))"));

        var result = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        Assert.That(result, Is.True);
    }

    [Test]
    public void AddForeignKey_RenameParentColumWithForeignKeyAndData_ForeignKeyPointsToRenamedColumn()
    {
        // Arrange
        AddTableWithPrimaryKey();
        Provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        Provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");

        // Act
        Provider.AddForeignKey(name: "FKName", childTable: "TestTwo", childColumn: "TestId", parentTable: "Test", parentColumn: "Id", constraint: ForeignKeyConstraintType.Cascade);

        // Rename column in parent
        Provider.RenameColumn("Test", "Id", "IdNew");

        // Assert
        var foreignKeyConstraints = ((SQLiteTransformationProvider)Provider).GetForeignKeyConstraints("TestTwo");
        var tableSQLCreateScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("TestTwo");

        Assert.That(tableSQLCreateScript, Does.Contain("CREATE TABLE \"TestTwo\""));
        Assert.That(tableSQLCreateScript, Does.Contain(", CONSTRAINT FKName FOREIGN KEY (TestId) REFERENCES Test(IdNew))"));
        Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("IdNew"));

        var result = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        Assert.That(result, Is.True);
    }
}
