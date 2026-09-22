using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

[TestFixture]
public abstract class Generic_ConstraintExistsBase : TransformationProviderBase
{
    [Test]
    public void QuotedConstraintNamesCanBeInspectedAndRemovedFromOnlyTheirTable()
    {
        const string name = "UQ ' dotted.name";
        Provider.AddTable("NamedConstraints", new Column("Id", DbType.Int32),
            new DotNetProjects.Migrator.Framework.UniqueConstraint(name, "Id"));
        Provider.AddTable("OtherConstraints", new Column("Id", DbType.Int32));
        Assert.That(Provider.ConstraintExists("NamedConstraints", name), Is.True);
        Assert.That(Provider.ConstraintExists("OtherConstraints", name), Is.False);
        Provider.RemoveConstraint("NamedConstraints", name);
        Assert.That(Provider.ConstraintExists("NamedConstraints", name), Is.False);
        Provider.Insert("NamedConstraints", ["Id"], [1]);
        Provider.Insert("NamedConstraints", ["Id"], [1]);
    }

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
           new Column(name: "Id",type: DbType.Int32){IsNullable = false},
           new Column(name: "TaskGroupId",type: DbType.Int32),new PrimaryKeyConstraint("PK_" + "Task", "Id")        );

        Provider.AddTable("TaskGroup",
             new Column(name: "Id",type: DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + "TaskGroup", "Id")         );

        Provider.AddForeignKey(name: fkName, childTable: tableName, childColumn: "TaskGroupId", parentTable: "TaskGroup", parentColumn: "Id");

        // Act
        var result = Provider.ConstraintExists(table: tableName, name: fkName);

        // Assert
        Assert.That(result, Is.True);
    }
}
