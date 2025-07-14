using System.Data;
using System.Linq;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetForeignKeysTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void RenameColumn_HavingASingleForeignKeyPointingToTheTargetColumn_SingleColumnForeignKeyIsRemoved()
    {
        // Arrange
        const string parentA = "TableA";
        const string parentAProperty1 = "ParentBProperty1";
        const string parentB = "TableB";
        const string parentBProperty1 = "ParentBProperty1";
        const string parentBProperty2 = "ParentBProperty2";
        const string child = "TableChild";
        const string childColumnFKToParentAProperty1 = "ChildColumnFKToParentAProperty1";
        const string childColumnFKToParentBProperty1 = "ChildColumnFKToParentBProperty1";
        const string childColumnFKToParentBProperty2 = "ChildColumnFKToParentBProperty2";
        const string foreignKeyStringA = "ForeignKeyStringA";
        const string foreignKeyStringB = "ForeignKeyStringB";

        Provider.AddTable(parentA, new Column(parentAProperty1, DbType.Int32, ColumnProperty.PrimaryKey));

        Provider.AddTable(parentB,
            new Column(parentBProperty1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(parentBProperty2, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddTable(child,
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(childColumnFKToParentAProperty1, DbType.Int32, ColumnProperty.Unique),
            new Column(childColumnFKToParentBProperty1, DbType.Int32),
            new Column(childColumnFKToParentBProperty2, DbType.Int32)
        );

        Provider.AddForeignKey(foreignKeyStringA, child, childColumnFKToParentAProperty1, parentA, parentAProperty1);
        Provider.AddForeignKey(foreignKeyStringB, child, [childColumnFKToParentBProperty1, childColumnFKToParentBProperty2], parentB, [parentBProperty1, parentBProperty2]);

        // Act
        var foreignKeyConstraints = Provider.GetForeignKeyConstraints(child);

        // Assert
        Assert.That(foreignKeyConstraints.Single(x => x.Name == foreignKeyStringA).ChildColumns, Is.EqualTo([childColumnFKToParentAProperty1]));
        Assert.That(foreignKeyConstraints.Single(x => x.Name == foreignKeyStringA).ParentColumns, Is.EqualTo([parentAProperty1]));

        Assert.That(foreignKeyConstraints.Single(x => x.Name == foreignKeyStringB).ChildColumns, Is.EqualTo([childColumnFKToParentBProperty1, childColumnFKToParentBProperty2]));
        Assert.That(foreignKeyConstraints.Single(x => x.Name == foreignKeyStringB).ParentColumns, Is.EqualTo([parentBProperty1, parentBProperty2]));
    }
}
