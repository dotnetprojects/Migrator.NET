using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
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

        // var tableInfoLevel2Before = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel2);
        // var tableInfoLevel3Before = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel3);

        // Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel1} ({propertyId}) VALUES (1)");
        // Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel1} ({propertyId}) VALUES (2)");
        // Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel2} ({propertyId}, {propertyLevel1Id}) VALUES (1, 1)");
        // Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel3} ({propertyId}, {propertyLevel2Id}) VALUES (1, 1)");

        // // Act
        // Provider.RenameColumn(tableNameLevel2, propertyId, propertyIdRenamed);
        // Provider.RenameColumn(tableNameLevel2, propertyLevel1Id, propertyLevel1IdRenamed);

        // // Assert
        // Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel2} ({propertyIdRenamed}, {propertyLevel1IdRenamed}) VALUES (2,2)");
        // using var command = Provider.GetCommand();

        // using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {tableNameLevel2}");
        // reader.Read();
        // var count = reader.GetInt32(reader.GetOrdinal("Count"));
        // Assert.That(count, Is.EqualTo(2));

        // var tableInfoLevel2After = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel2);

        // Assert.That(tableInfoLevel2Before.Columns.Single(x => x.Name == propertyId).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        // Assert.That(tableInfoLevel2Before.Columns.Single(x => x.Name == propertyLevel1Id).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);
        // Assert.That(tableInfoLevel2Before.ForeignKeys.Single().ChildColumns.Single(), Is.EqualTo(propertyLevel1Id));

        // Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyId), Is.Null);
        // Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyLevel1Id), Is.Null);
        // Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyIdRenamed), Is.Not.Null);
        // Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyLevel1IdRenamed), Is.Not.Null);
        // Assert.That(tableInfoLevel2After.ForeignKeys.Single().ChildColumns.Single(), Is.EqualTo(propertyLevel1IdRenamed));

        // var valid = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        // Assert.That(valid, Is.True);
    }
}
