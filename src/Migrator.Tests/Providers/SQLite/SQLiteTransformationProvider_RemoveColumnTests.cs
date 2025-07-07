using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_RemoveColumn : SQLiteTransformationProviderTestBase
{
    [Test]
    public void RemoveColumn_HavingNoCompositeIndexAndNoCompositeUniqueConstraint_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";
        const string propertyName3 = "Color3";
        const string indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddIndex(indexName, testTableName, [propertyName1]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        Provider.RemoveColumn(testTableName, propertyName2);
        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}) VALUES (2)");

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName3).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName3).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        var indexAfter = tableInfoAfter.Indexes.Single();
        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1 });
    }

    [Test]
    public void RemoveColumn_HavingASingleForeignKeyPointingToTheTargetColumn_SingleColumnForeignKeyIsRemoved()
    {
        // Arrange
        const string parentTableName = "Parent";
        const string propertyName1 = "Id";
        const string propertyName2 = "OtherProperty";
        const string childTestTableName = "Child";
        const string childTestTableName2 = "ChildTable2";
        const string propertyChildTableName1 = "ColorId";

        Provider.AddTable(parentTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddTable(childTestTableName, new Column(propertyChildTableName1, DbType.Int32));
        Provider.AddForeignKey("Not used in SQLite", childTestTableName, propertyChildTableName1, parentTableName, propertyName1);
        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(childTestTableName);

        Provider.AddTable(childTestTableName2, new Column(propertyChildTableName1, DbType.Int32));
        Provider.AddForeignKey(name: "Not used in SQLite", childTable: childTestTableName2, childColumn: propertyChildTableName1, parentTable: parentTableName, parentColumn: propertyName2);

        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(parentTableName);
        var tableInfoChildBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(childTestTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {parentTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");
        Provider.ExecuteNonQuery($"INSERT INTO {childTestTableName} ({propertyChildTableName1}) VALUES (1)");
        Provider.ExecuteNonQuery($"INSERT INTO {childTestTableName2} ({propertyChildTableName1}) VALUES (2)");

        // Act
        Provider.RemoveColumn(parentTableName, propertyName1);

        // Assert
        Provider.ExecuteNonQuery($"INSERT INTO {parentTableName} ({propertyName2}) VALUES (3)");
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {parentTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(parentTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        Assert.That(tableInfoAfter.Columns.FirstOrDefault(x => x.Name == propertyName1), Is.Null);
        Assert.That(tableInfoAfter.ForeignKeys, Is.Empty);

        var valid = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        Assert.That(valid, Is.True);
    }

    /// <summary>
    /// If there is a composite index (more than one key columns) that contains the target column it should throw.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingIndexWithTwoColumnsOneOfThemIsTheTargetColumn_Throws()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";
        const string propertyName3 = "Color3";
        const string indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act/Assert
        var exception = Assert.Throws<Exception>(() => Provider.RemoveColumn(testTableName, propertyName2));

        Assert.That(exception.Message, Does.StartWith("Found composite index"));
    }

    /// <summary>
    /// If there is a composite unique constraint (more than one key columns) that contains the target column it should throw.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingUniqueConstraintWithTwoColumnsOneOfThemTargetColumn_Throws()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";
        const string propertyName3 = "Color3";
        const string indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddUniqueConstraint("Not used in SQLite", testTableName, [propertyName2, propertyName3]);

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act/Assert
        var exception = Assert.Throws<Exception>(() => Provider.RemoveColumn(testTableName, propertyName2));

        Assert.That(exception.Message, Does.StartWith("Found composite unique constraint"));
    }

    /// <summary>
    /// If there are multiple single uniques and only single column indexes (or no index) it should succeed.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingMultipleSingleUniques_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";
        const string propertyName3 = "Color3";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        // Act
        Provider.RemoveColumn(testTableName, propertyName2);
        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Uniques.Count, Is.EqualTo(2));
        Assert.That(tableInfoAfter.Uniques.Count, Is.EqualTo(1));
    }

    [Test]
    public void RemoveColumn_HavingAForeignKeyPointingFromTableToParentAndForeignKeyPointingToTable_SingleColumnForeignKeyIsRemoved()
    {
        // Arrange
        const string tableNameLevel1 = "Level1";
        const string tableNameLevel2 = "Level2";
        const string tableNameLevel3 = "Level3";
        const string propertyId = "Id";
        const string propertyLevel1Id = "Level1Id";
        const string propertyLevel2Id = "Level2Id";

        Provider.AddTable(tableNameLevel1, new Column(propertyId, DbType.Int32, ColumnProperty.PrimaryKey));

        Provider.AddTable(tableNameLevel2,
            new Column(propertyId, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyLevel1Id, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddTable(tableNameLevel3,
            new Column(propertyId, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyLevel2Id, DbType.Int32)
        );

        Provider.AddForeignKey("Level2ToLevel1", tableNameLevel2, propertyLevel1Id, tableNameLevel1, propertyId);
        Provider.AddForeignKey("Level3ToLevel2", tableNameLevel3, propertyLevel2Id, tableNameLevel2, propertyId);

        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableNameLevel2);

        var tableInfoLevel2Before = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel2);
        var tableInfoLevel3Before = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel3);

        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel1} ({propertyId}) VALUES (1)");
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel1} ({propertyId}) VALUES (2)");
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel2} ({propertyId}, {propertyLevel1Id}) VALUES (1, 1)");
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel3} ({propertyId}, {propertyLevel2Id}) VALUES (1, 1)");

        // Act
        Provider.RemoveColumn(tableNameLevel2, propertyLevel1Id);

        // Assert
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel2} ({propertyId}) VALUES (2)");
        using var command = Provider.GetCommand();

        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {tableNameLevel2}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoLevel2After = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel2);

        Assert.That(tableInfoLevel2Before.Columns.Single(x => x.Name == propertyId).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoLevel2Before.Columns.Single(x => x.Name == propertyLevel1Id).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);
        Assert.That(tableInfoLevel2Before.ForeignKeys.Single().ChildColumns.Single(), Is.EqualTo(propertyLevel1Id));

        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyId), Is.Not.Null);
        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyLevel1Id), Is.Null);
        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyId), Is.Not.Null);
        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyLevel1Id), Is.Null);
        Assert.That(tableInfoLevel2After.ForeignKeys, Is.Empty);

        var valid = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        Assert.That(valid, Is.True);
    }
}