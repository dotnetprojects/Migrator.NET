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
        const string testTableName = "Color";
        const string propertyName1 = "Id";
        const string propertyName2 = "OtherProperty";
        const string childTestTableName = "ChildTable";
        const string childTestTableName2 = "ChildTable2";
        const string propertyChildTableName1 = "ColorId";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddTable(childTestTableName, new Column(propertyChildTableName1, DbType.Int32));
        Provider.AddForeignKey("Not used in SQLite", childTestTableName, propertyChildTableName1, testTableName, propertyName1);
        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(childTestTableName);

        Provider.AddTable(childTestTableName2, new Column(propertyChildTableName1, DbType.Int32));
        Provider.AddForeignKey(name: "Not used in SQLite", childTable: childTestTableName2, childColumn: propertyChildTableName1, parentTable: testTableName, parentColumn: propertyName2);

        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);
        var tableInfoChildBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(childTestTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");
        Provider.ExecuteNonQuery($"INSERT INTO {childTestTableName} ({propertyChildTableName1}) VALUES (1)");
        Provider.ExecuteNonQuery($"INSERT INTO {childTestTableName2} ({propertyChildTableName1}) VALUES (2)");

        // Act
        Provider.RemoveColumn(testTableName, propertyName1);

        // Assert
        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName2}) VALUES (3)");
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

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
}