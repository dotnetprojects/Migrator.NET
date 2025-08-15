using System;
using System.Data.SQLite;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetCheckConstraintsTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void GetCheckConstraints_AddCheckConstraintsViaAddTable_CreatesTableCorrectly()
    {
        const string tableName = "MyTableName";
        const string columnName = "MyColumnName";
        const string checkConstraint1 = "MyCheckConstraint1";
        const string checkConstraint2 = "MyCheckConstraint2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName, System.Data.DbType.Int32),
            new CheckConstraint(checkConstraint1, $"{columnName} > 10"),
            new CheckConstraint(checkConstraint2, $"{columnName} < 100")
        );

        var checkConstraints = ((SQLiteTransformationProvider)Provider).GetCheckConstraints(tableName);

        // Assert
        Assert.That(checkConstraints[0].Name, Is.EqualTo(checkConstraint1));
        Assert.That(checkConstraints[0].CheckConstraintString, Is.EqualTo($"{columnName} > 10"));

        Assert.That(checkConstraints[1].Name, Is.EqualTo(checkConstraint2));
        Assert.That(checkConstraints[1].CheckConstraintString, Is.EqualTo($"{columnName} < 100"));

        Provider.Insert(tableName, [columnName], [11]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName], [1]));
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName], [200]));

        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That(createScript, Is.EqualTo("CREATE TABLE MyTableName (MyColumnName INTEGER NULL, CONSTRAINT MyCheckConstraint1 CHECK (MyColumnName > 10), CONSTRAINT MyCheckConstraint2 CHECK (MyColumnName < 100))"));
    }

    [Test]
    public void CheckForeignKeyIntegrity_IntegrityOk_ReturnsTrue()
    {
        // Arrange
        AddTableWithPrimaryKey();
        Provider.ExecuteNonQuery("INSERT INTO Test (Id, name) VALUES (1, 'my name')");
        Provider.ExecuteNonQuery("INSERT INTO TestTwo (TestId) VALUES (1)");
        Provider.AddForeignKey(name: "FKName", childTable: "TestTwo", childColumn: "TestId", parentTable: "Test", parentColumn: "Id", constraint: ForeignKeyConstraintType.Cascade);

        // Act
        var result = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();

        // Assert
        Assert.That(result, Is.True);
    }
}
