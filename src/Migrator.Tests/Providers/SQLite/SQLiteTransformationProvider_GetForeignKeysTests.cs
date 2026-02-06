using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
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

    [Test]
    public void GetForeignKeyConstraints_WithReservedWordTableName_ReturnsCorrectForeignKeys()
    {
        // Arrange - Testing with SQLite reserved word "group" as parent table name
        const string parentTable = "group";
        const string parentColumn = "Id";
        const string childTable = "Orders";
        const string childColumn = "GroupId";
        const string foreignKeyName = "FK_Orders_Group";

        Provider.AddTable(parentTable, new Column(parentColumn, DbType.Int32, ColumnProperty.PrimaryKey));
        Provider.AddTable(childTable,
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(childColumn, DbType.Int32)
        );

        Provider.AddForeignKey(foreignKeyName, childTable, childColumn, parentTable, parentColumn);

        // Act
        var foreignKeyConstraints = Provider.GetForeignKeyConstraints(childTable);

        // Assert
        Assert.That(foreignKeyConstraints.Length, Is.EqualTo(1));
        var fk = foreignKeyConstraints.Single();
        Assert.That(fk.Name, Is.EqualTo(foreignKeyName));
        Assert.That(fk.ParentTable, Is.EqualTo(parentTable));
        Assert.That(fk.ParentColumns, Is.EqualTo(new[] { parentColumn }));
        Assert.That(fk.ChildTable, Is.EqualTo(childTable));
        Assert.That(fk.ChildColumns, Is.EqualTo(new[] { childColumn }));
    }

    [Test]
    public void GetForeignKeyConstraints_WithReservedWordOrder_ReturnsCorrectForeignKeys()
    {
        // Arrange - Testing with SQLite reserved word "order" as parent table name
        const string parentTable = "order";
        const string parentColumn = "OrderId";
        const string childTable = "LineItems";
        const string childColumn = "OrderRef";
        const string foreignKeyName = "FK_LineItems_Order";

        Provider.AddTable(parentTable, new Column(parentColumn, DbType.Int32, ColumnProperty.PrimaryKey));
        Provider.AddTable(childTable,
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(childColumn, DbType.Int32)
        );

        Provider.AddForeignKey(foreignKeyName, childTable, childColumn, parentTable, parentColumn);

        // Act
        var foreignKeyConstraints = Provider.GetForeignKeyConstraints(childTable);

        // Assert
        Assert.That(foreignKeyConstraints.Length, Is.EqualTo(1));
        var fk = foreignKeyConstraints.Single();
        Assert.That(fk.Name, Is.EqualTo(foreignKeyName));
        Assert.That(fk.ParentTable, Is.EqualTo(parentTable));
        Assert.That(fk.ParentColumns, Is.EqualTo(new[] { parentColumn }));
        Assert.That(fk.ChildTable, Is.EqualTo(childTable));
        Assert.That(fk.ChildColumns, Is.EqualTo(new[] { childColumn }));
    }

    [Test]
    public void GetForeignKeyConstraints_WithReservedWordKey_ReturnsCorrectForeignKeys()
    {
        // Arrange - Testing with SQLite reserved word "key" as parent table name
        const string parentTable = "key";
        const string parentColumn = "KeyId";
        const string childTable = "Locks";
        const string childColumn = "KeyRef";
        const string foreignKeyName = "FK_Locks_Key";

        Provider.AddTable(parentTable, new Column(parentColumn, DbType.Int32, ColumnProperty.PrimaryKey));
        Provider.AddTable(childTable,
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(childColumn, DbType.Int32)
        );

        Provider.AddForeignKey(foreignKeyName, childTable, childColumn, parentTable, parentColumn);

        // Act
        var foreignKeyConstraints = Provider.GetForeignKeyConstraints(childTable);

        // Assert
        Assert.That(foreignKeyConstraints.Length, Is.EqualTo(1));
        var fk = foreignKeyConstraints.Single();
        Assert.That(fk.Name, Is.EqualTo(foreignKeyName));
        Assert.That(fk.ParentTable, Is.EqualTo(parentTable));
        Assert.That(fk.ParentColumns, Is.EqualTo(new[] { parentColumn }));
        Assert.That(fk.ChildTable, Is.EqualTo(childTable));
        Assert.That(fk.ChildColumns, Is.EqualTo(new[] { childColumn }));
    }

    [Test]
    public void GetForeignKeyConstraints_WithMultipleForeignKeysIncludingQuoted_ReturnsAllCorrectly()
    {
        // Arrange - Testing combination of regular and quoted table names
        const string regularParent = "NormalTable";
        const string quotedParent = "order";
        const string regularColumn = "NormalId";
        const string quotedColumn = "OrderId";
        const string childTable = "MixedChild";
        const string childColumnRegular = "NormalRef";
        const string childColumnQuoted = "OrderRef";
        const string fkRegular = "FK_Mixed_Normal";
        const string fkQuoted = "FK_Mixed_Order";

        Provider.AddTable(regularParent, new Column(regularColumn, DbType.Int32, ColumnProperty.PrimaryKey));
        Provider.AddTable(quotedParent, new Column(quotedColumn, DbType.Int32, ColumnProperty.PrimaryKey));
        Provider.AddTable(childTable,
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(childColumnRegular, DbType.Int32),
            new Column(childColumnQuoted, DbType.Int32)
        );

        Provider.AddForeignKey(fkRegular, childTable, childColumnRegular, regularParent, regularColumn);
        Provider.AddForeignKey(fkQuoted, childTable, childColumnQuoted, quotedParent, quotedColumn);

        // Act
        var foreignKeyConstraints = Provider.GetForeignKeyConstraints(childTable);

        // Assert
        Assert.That(foreignKeyConstraints.Length, Is.EqualTo(2));

        var fkReg = foreignKeyConstraints.Single(x => x.Name == fkRegular);
        Assert.That(fkReg.ParentTable, Is.EqualTo(regularParent));
        Assert.That(fkReg.ParentColumns, Is.EqualTo(new[] { regularColumn }));
        Assert.That(fkReg.ChildColumns, Is.EqualTo(new[] { childColumnRegular }));

        var fkQuo = foreignKeyConstraints.Single(x => x.Name == fkQuoted);
        Assert.That(fkQuo.ParentTable, Is.EqualTo(quotedParent));
        Assert.That(fkQuo.ParentColumns, Is.EqualTo(new[] { quotedColumn }));
        Assert.That(fkQuo.ChildColumns, Is.EqualTo(new[] { childColumnQuoted }));
    }
}
