using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

[TestFixture]
public abstract class Generic_ConstraintExistsBase : TransformationProviderBase
{
    /// <summary>
    /// Should return true if foreign key exists.
    /// </summary>
    [Test]
    public void ConstraintExists_ForeignKeyExists_ReturnsTrue()
    {
        // Arrange
        var tableName = "Task";
        var fkName = "FK_Task_TaskGroup";

        Provider.AddTable("Task",
           new Column(name: "Id", type: DbType.Int32, property: ColumnProperty.PrimaryKey),
           new Column(name: "TaskGroupId", type: DbType.Int32, property: ColumnProperty.Null)
        );

        Provider.AddTable("TaskGroup",
             new Column(name: "Id", type: DbType.Int32, property: ColumnProperty.PrimaryKey)
         );

        Provider.AddForeignKey(name: fkName, childTable: tableName, childColumn: "TaskGroupId", parentTable: "TaskGroup", parentColumn: "Id");

        // Act
        var result = Provider.ConstraintExists(table: tableName, name: fkName);

        // Assert
        Assert.That(result, Is.True);
    }
}
