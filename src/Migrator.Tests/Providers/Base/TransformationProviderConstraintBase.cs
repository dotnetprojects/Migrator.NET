using System;
using System.Data;
using Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

/// <summary>
/// Base class for Provider tests for all tests including constraint oriented tests.
/// </summary>
public abstract class TransformationProviderConstraintBase : TransformationProviderBase
{
    public void AddForeignKey()
    {
        AddTableWithPrimaryKey();
        Provider.AddForeignKey("FK_Test_TestTwo", "TestTwo", "TestId", "Test", "Id");
    }

    public void AddPrimaryKey()
    {
        AddTable();
        Provider.AddPrimaryKey("PK_Test", "Test", "Id");
    }

    public void AddUniqueConstraint()
    {
        Provider.AddUniqueConstraint("UN_Test_TestTwo", "TestTwo", "TestId");
    }

    public void AddMultipleUniqueConstraint()
    {
        Provider.AddUniqueConstraint("UN_Test_TestTwo", "TestTwo", "Id", "TestId");
    }

    public void AddCheckConstraint()
    {
        Provider.AddCheckConstraint("CK_TestTwo_TestId", "TestTwo", "TestId>5");
    }

    [Test]
    public void CanAddPrimaryKey()
    {
        AddPrimaryKey();
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True);
    }

    [Test]
    public void AddIndexedColumn()
    {
        Provider.AddColumn("TestTwo", "Test", DbType.String, 50, ColumnProperty.Indexed);
    }

    [Test]
    public void AddUniqueColumn()
    {
        Provider.AddColumn("TestTwo", "Test", DbType.String, 50, ColumnProperty.Unique);
    }

    [Test]
    public void CanAddForeignKey()
    {
        AddForeignKey();
        Assert.That(Provider.ConstraintExists("TestTwo", "FK_Test_TestTwo"), Is.True);
    }

    [Test]
    public virtual void CanAddUniqueConstraint()
    {
        AddUniqueConstraint();
        Assert.That(Provider.ConstraintExists("TestTwo", "UN_Test_TestTwo"), Is.True);
    }

    [Test]
    public virtual void CanAddMultipleUniqueConstraint()
    {
        AddMultipleUniqueConstraint();
        Assert.That(Provider.ConstraintExists("TestTwo", "UN_Test_TestTwo"), Is.True);
    }

    [Test]
    public virtual void CanAddCheckConstraint()
    {
        AddCheckConstraint();
        Assert.That(Provider.ConstraintExists("TestTwo", "CK_TestTwo_TestId"), Is.True);
    }

    [Test]
    public virtual void RemoveForeignKey()
    {
        Console.WriteLine($"Test running in class: {TestContext.CurrentContext.Test.ClassName}");
        AddForeignKey();
        Provider.RemoveForeignKey("TestTwo", "FK_Test_TestTwo");
        Assert.That(Provider.ConstraintExists("TestTwo", "FK_Test_TestTwo"), Is.False);
    }

    [Test]
    public void RemoveUniqueConstraint()
    {
        AddUniqueConstraint();
        Provider.RemoveConstraint("TestTwo", "UN_Test_TestTwo");
        Assert.That(Provider.ConstraintExists("TestTwo", "UN_Test_TestTwo"), Is.False);
    }

    [Test]
    public virtual void RemoveCheckConstraint()
    {
        AddCheckConstraint();
        Provider.RemoveConstraint("TestTwo", "CK_TestTwo_TestId");
        Assert.That(Provider.ConstraintExists("TestTwo", "CK_TestTwo_TestId"), Is.False);
    }

    [Test]
    public void RemoveUnexistingForeignKey()
    {
        AddForeignKey();
        Provider.RemoveForeignKey("abc", "FK_Test_TestTwo");
        Provider.RemoveForeignKey("abc", "abc");
        Provider.RemoveForeignKey("Test", "abc");
    }

    [Test]
    public void ConstraintExist()
    {
        AddForeignKey();
        Assert.That(Provider.ConstraintExists("TestTwo", "FK_Test_TestTwo"), Is.True);
        Assert.That(Provider.ConstraintExists("abc", "abc"), Is.False);
    }

    [Test]
    public void AddTableWithCompoundPrimaryKey()
    {
        Provider.AddTable("Test",
                           new Column("PersonId", DbType.Int32, ColumnProperty.PrimaryKey),
                           new Column("AddressId", DbType.Int32, ColumnProperty.PrimaryKey)
        );

        Assert.That(Provider.TableExists("Test"), Is.True, "Table doesn't exist");
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True, "Constraint doesn't exist");
    }

    [Test]
    public void AddTableWithCompoundPrimaryKeyShouldKeepNullForOtherProperties()
    {
        Provider.AddTable("Test",
                           new Column("PersonId", DbType.Int32, ColumnProperty.PrimaryKey),
                           new Column("AddressId", DbType.Int32, ColumnProperty.PrimaryKey),
                           new Column("Name", DbType.String, 30, ColumnProperty.Null)
            );
        Assert.That(Provider.TableExists("Test"), Is.True, "Table doesn't exist");
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True, "Constraint doesn't exist");

        var column = Provider.GetColumnByName("Test", "Name");
        Assert.That(column, Is.Not.Null);
        Assert.That((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null, Is.True);
    }
}
