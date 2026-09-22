using System;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;
namespace Migrator.Tests.Framework.ColumnProperties;
public class ColumnModelTests
{
    [Test]
    public void ColumnsDoNotExposeConstraintFlags()
    {
        Assert.That(typeof(Column).Assembly.GetType("DotNetProjects.Migrator.Framework.ColumnProperty"), Is.Null);
        Assert.That(typeof(Column).GetProperties().Select(p => p.Name), Does.Not.Contain("IsPrimaryKey"));
        Assert.That(typeof(Column).GetProperties().Select(p => p.Name), Does.Not.Contain("ColumnProperty"));
        Assert.That(Enum.GetNames<ColumnAttribute>(), Is.EquivalentTo(new[] { "Null", "NotNull", "Identity", "Unsigned" }));
    }
    [Test]
    public void ColumnAttributesAreIndependent()
    {
        var column = new Column("Id") { IsIdentity = true, IsNullable = true, IsUnsigned = true };
        Assert.That(column.IsNullable, Is.True);
        Assert.That(column.IsIdentity, Is.True);
        column.IsNullable = false;
        Assert.That(column.IsIdentity, Is.True);
        Assert.That(column.IsUnsigned, Is.True);
    }
    [Test]
    public void KeyDefinitionsCopyOrderedCallerArrays()
    {
        var columns = new[] { "Second", "First" };
        var key = new PrimaryKeyConstraint("PK", columns);
        columns[0] = "Changed";
        Assert.That(key.KeyColumns, Is.EqualTo(new[] { "Second", "First" }));
    }
}
