using System.Data;
using Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers
{
    /// <summary>
    /// Base class for Provider tests for all tests including constraint oriented tests.
    /// </summary>
    public class TransformationProviderConstraintBase : TransformationProviderBase
    {
        public void AddForeignKey()
        {
            AddTableWithPrimaryKey();
            _provider.AddForeignKey("FK_Test_TestTwo", "TestTwo", "TestId", "Test", "Id");
        }

        public void AddPrimaryKey()
        {
            AddTable();
            _provider.AddPrimaryKey("PK_Test", "Test", "Id");
        }

        public void AddUniqueConstraint()
        {
            _provider.AddUniqueConstraint("UN_Test_TestTwo", "TestTwo", "TestId");
        }

        public void AddMultipleUniqueConstraint()
        {
            _provider.AddUniqueConstraint("UN_Test_TestTwo", "TestTwo", "Id", "TestId");
        }

        public void AddCheckConstraint()
        {
            _provider.AddCheckConstraint("CK_TestTwo_TestId", "TestTwo", "TestId>5");
        }

        [Test]
        public void CanAddPrimaryKey()
        {
            AddPrimaryKey();
            Assert.That(_provider.PrimaryKeyExists("Test", "PK_Test"), Is.True);
        }

        [Test]
        public void AddIndexedColumn()
        {
            _provider.AddColumn("TestTwo", "Test", DbType.String, 50, ColumnProperty.Indexed);
        }

        [Test]
        public void AddUniqueColumn()
        {
            _provider.AddColumn("TestTwo", "Test", DbType.String, 50, ColumnProperty.Unique);
        }

        [Test]
        public void CanAddForeignKey()
        {
            AddForeignKey();
            Assert.That(_provider.ConstraintExists("TestTwo", "FK_Test_TestTwo"), Is.True);
        }

        [Test]
        public virtual void CanAddUniqueConstraint()
        {
            AddUniqueConstraint();
            Assert.That(_provider.ConstraintExists("TestTwo", "UN_Test_TestTwo"), Is.True);
        }

        [Test]
        public virtual void CanAddMultipleUniqueConstraint()
        {
            AddMultipleUniqueConstraint();
            Assert.That(_provider.ConstraintExists("TestTwo", "UN_Test_TestTwo"), Is.True);
        }

        [Test]
        public virtual void CanAddCheckConstraint()
        {
            AddCheckConstraint();
            Assert.That(_provider.ConstraintExists("TestTwo", "CK_TestTwo_TestId"), Is.True);
        }

        [Test]
        public void RemoveForeignKey()
        {
            AddForeignKey();
            _provider.RemoveForeignKey("TestTwo", "FK_Test_TestTwo");
            Assert.That(_provider.ConstraintExists("TestTwo", "FK_Test_TestTwo"), Is.False);
        }

        [Test]
        public void RemoveUniqueConstraint()
        {
            AddUniqueConstraint();
            _provider.RemoveConstraint("TestTwo", "UN_Test_TestTwo");
            Assert.That(_provider.ConstraintExists("TestTwo", "UN_Test_TestTwo"), Is.False);
        }

        [Test]
        public virtual void RemoveCheckConstraint()
        {
            AddCheckConstraint();
            _provider.RemoveConstraint("TestTwo", "CK_TestTwo_TestId");
            Assert.That(_provider.ConstraintExists("TestTwo", "CK_TestTwo_TestId"), Is.False);
        }

        [Test]
        public void RemoveUnexistingForeignKey()
        {
            AddForeignKey();
            _provider.RemoveForeignKey("abc", "FK_Test_TestTwo");
            _provider.RemoveForeignKey("abc", "abc");
            _provider.RemoveForeignKey("Test", "abc");
        }

        [Test]
        public void ConstraintExist()
        {
            AddForeignKey();
            Assert.That(_provider.ConstraintExists("TestTwo", "FK_Test_TestTwo"), Is.True);
            Assert.That(_provider.ConstraintExists("abc", "abc"), Is.False);
        }

        [Test]
        public void AddTableWithCompoundPrimaryKey()
        {
            _provider.AddTable("Test",
                               new Column("PersonId", DbType.Int32, ColumnProperty.PrimaryKey),
                               new Column("AddressId", DbType.Int32, ColumnProperty.PrimaryKey)
            );

            Assert.That(_provider.TableExists("Test"), Is.True, "Table doesn't exist");
            Assert.That(_provider.PrimaryKeyExists("Test", "PK_Test"), Is.True, "Constraint doesn't exist");
        }

        [Test]
        public void AddTableWithCompoundPrimaryKeyShouldKeepNullForOtherProperties()
        {
            _provider.AddTable("Test",
                               new Column("PersonId", DbType.Int32, ColumnProperty.PrimaryKey),
                               new Column("AddressId", DbType.Int32, ColumnProperty.PrimaryKey),
                               new Column("Name", DbType.String, 30, ColumnProperty.Null)
                );
            Assert.That(_provider.TableExists("Test"), Is.True, "Table doesn't exist");
            Assert.That(_provider.PrimaryKeyExists("Test", "PK_Test"), Is.True, "Constraint doesn't exist");

            Column column = _provider.GetColumnByName("Test", "Name");
            Assert.That(column, Is.Not.Null);
            Assert.That((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null, Is.True);
        }
    }
}