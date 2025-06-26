#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Providers.SQLite;
using Migrator.Tests.Settings;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProviderAddForeignKeyTests : TransformationProviderBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById("SQLiteConnectionString")
            .ConnectionString;

        _provider = new SQLiteTransformationProvider(new SQLiteDialect(), connectionString, "default", null);
        _provider.BeginTransaction();

        AddDefaultTable();
    }

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
        _provider.AddForeignKey("FK name is not supported by SQLite", parentTable: "Test", parentColumn: "Id", childTable: "TestTwo", childColumn: "TestId", ForeignKeyConstraintType.Cascade);

        // Act
        _provider.RenameColumn("Test", "Id", "IdNew");

        // Assert
        var foreignKeyConstraints = ((SQLiteTransformationProvider)_provider).GetForeignKeyConstraints("TestTwo");
        var tableSQLCreateScript = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript("TestTwo");

        Assert.That(tableSQLCreateScript, Does.Contain("CREATE TABLE \"TestTwo\""));
        Assert.That(tableSQLCreateScript, Does.Contain(", FOREIGN KEY (TestId) REFERENCES Test(Id))"));

        var result = ((SQLiteTransformationProvider)_provider).CheckForeignKeyIntegrity();
        Assert.That(result, Is.True);
    }
}
