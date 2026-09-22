using System.Data;
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
    [TestCase(ForeignKeyConstraintType.Cascade, "CASCADE")]
    [TestCase(ForeignKeyConstraintType.SetNull, "SET NULL")]
    [TestCase(ForeignKeyConstraintType.SetDefault, "SET DEFAULT")]
    [TestCase(ForeignKeyConstraintType.Restrict, "RESTRICT")]
    [TestCase(ForeignKeyConstraintType.NoAction, "NO ACTION")]
    public void AddForeignKey(ForeignKeyConstraintType constraint, string expectedAction)
    {
        // Arrange
        AddTableWithPrimaryKey();
        Provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        Provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");

        // Act
        Provider.AddForeignKey(name: "FKName", childTable: "TestTwo", childColumn: "TestId", parentTable: "Test", parentColumn: "Id", constraint: constraint);

        // Assert
        var foreignKeyConstraints = ((SQLiteTransformationProvider)Provider).GetForeignKeyConstraints("TestTwo");
        var tableSQLCreateScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("TestTwo");

        Assert.That(foreignKeyConstraints.Single().Name, Is.EqualTo("FKName"));
        Assert.That(foreignKeyConstraints.Single().ChildTable, Is.EqualTo("TestTwo"));
        Assert.That(foreignKeyConstraints.Single().ParentTable, Is.EqualTo("Test"));
        Assert.That(foreignKeyConstraints.Single().ChildColumns.Single(), Is.EqualTo("TestId"));
        Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("Id"));

        Assert.That(foreignKeyConstraints.Single().OnDelete, Is.EqualTo(expectedAction));
        var expectedClause = constraint == ForeignKeyConstraintType.NoAction ? "" : $" ON DELETE {expectedAction}";
        Assert.That(tableSQLCreateScript.Replace("\"", ""), Does.Contain("CREATE TABLE TestTwo"));
        Assert.That(tableSQLCreateScript.Replace("\"", ""), Does.Contain($", CONSTRAINT FKName FOREIGN KEY (TestId) REFERENCES Test(Id){expectedClause})"));

        // Reading and rebuilding an existing foreign key must retain its action.
        Provider.RenameColumn("TestTwo", "TestId", "ParentId");
        Assert.That(Provider.GetForeignKeyConstraints("TestTwo").Single().OnDelete, Is.EqualTo(expectedAction));

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

        Assert.That(tableSQLCreateScript.Replace("\"", ""), Does.Contain("CREATE TABLE TestTwo"));
        Assert.That(tableSQLCreateScript.Replace("\"", ""), Does.Contain(", CONSTRAINT FKName FOREIGN KEY (TestId) REFERENCES Test(IdNew) ON DELETE CASCADE)"));
        Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("IdNew"));

        var result = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        Assert.That(result, Is.True);
    }

    [Test]
    public void AddForeignKey_3_Success()
    {
        Provider.AddTable("Task",
           new Column(name: "BinId", type: DbType.Int32, property: ColumnProperty.NotNull),
           new Column(name: "CreationTimeStamp", type: DbType.DateTime2, property: ColumnProperty.NotNull),
           new Column(name: "EstimatedPickTime", type: DbType.Int32, property: ColumnProperty.Null),
           new Column(name: "Id", type: DbType.Int32, property: ColumnProperty.NotNull),
           new Column(name: "Item", type: DbType.Int32, property: ColumnProperty.Null),
           new Column(name: "Order", type: DbType.Int32, property: ColumnProperty.Null),
           new Column(name: "TaskGroupId", type: DbType.Int32, property: ColumnProperty.Null)
       );

        Provider.AddTable("TaskGroup",
             new Column(name: "CreationTimeStamp", type: DbType.DateTime2, property: ColumnProperty.NotNull),
             new Column(name: "Id", type: DbType.Int32)
         );

        // TODO CK add more columns.
        Provider.AddForeignKey(name: "FK_Task_TaskGroup", childTable: "Task", childColumn: "TaskGroupId", parentTable: "TaskGroup", parentColumn: "Id");
        Assert.That(Provider.GetForeignKeyConstraints("Task").Single().OnDelete, Is.EqualTo("NO ACTION"));
        Assert.That(((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Task"), Does.Not.Contain("ON DELETE"));
    }

    [Test]
    public void AddForeignKey_Cascade_DeletingParentDeletesReferencingChildren()
    {
        // Enable enforcement before any transaction; SQLite ignores PRAGMA changes inside one.
        using var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);

        provider.AddTable("Parent", new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey));
        provider.AddTable("Child", new Column("ParentId", DbType.Int32));
        provider.ExecuteNonQuery("INSERT INTO Parent (Id) VALUES (1), (2)");
        provider.ExecuteNonQuery("INSERT INTO Child (ParentId) VALUES (1), (1), (2)");
        provider.AddForeignKey("FK_Child_Parent", "Child", "ParentId", "Parent", "Id", ForeignKeyConstraintType.Cascade);

        Assert.That(System.Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child")), Is.EqualTo(3));
        provider.ExecuteNonQuery("DELETE FROM Parent WHERE Id = 1");

        Assert.That(System.Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child WHERE ParentId = 1")), Is.Zero);
        Assert.That(System.Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child WHERE ParentId = 2")), Is.EqualTo(1));
    }
}
