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
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var propertyName3 = "Color3";
        var indexName = "MyIndexName";

        _provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        _provider.AddIndex(indexName, testTableName, [propertyName1]);
        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        _provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        _provider.RemoveColumn(testTableName, propertyName2);
        _provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}) VALUES (2)");

        // Assert
        using var command = _provider.GetCommand();
        using var reader = _provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

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
        var testTableName = "Color";
        var propertyName1 = "Id";
        var propertyName2 = "OtherProperty";
        var childTestTableName = "ChildTable";
        var childTestTableName2 = "ChildTable2";
        var propertyChildTableName1 = "ColorId";

        _provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique)
        );

        _provider.AddTable(childTestTableName, new Column(propertyChildTableName1, DbType.Int32));
        _provider.AddForeignKey("Not used in SQLite", testTableName, propertyName1, childTestTableName, propertyChildTableName1);
        var script = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript(childTestTableName);

        _provider.AddTable(childTestTableName2, new Column(propertyChildTableName1, DbType.Int32));
        _provider.AddForeignKey("Not used in SQLite", testTableName, propertyName2, childTestTableName2, propertyChildTableName1);

        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);
        var tableInfoChildBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(childTestTableName);

        _provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");
        _provider.ExecuteNonQuery($"INSERT INTO {childTestTableName} ({propertyChildTableName1}) VALUES (1)");
        _provider.ExecuteNonQuery($"INSERT INTO {childTestTableName2} ({propertyChildTableName1}) VALUES (2)");

        // Act
        _provider.RemoveColumn(testTableName, propertyName1);

        // Assert
        _provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName2}) VALUES (3)");
        using var command = _provider.GetCommand();
        using var reader = _provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        Assert.That(tableInfoAfter.Columns.FirstOrDefault(x => x.Name == propertyName1), Is.Null);
        Assert.That(tableInfoAfter.ForeignKeys, Is.Empty);

        var valid = ((SQLiteTransformationProvider)_provider).CheckForeignKeyIntegrity();
        Assert.That(valid, Is.True);
    }

    /// <summary>
    /// If there is a composite index (more than one key columns) that contains the target column it should throw.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingIndexWithTwoColumnsOneOfThemTargetColumn_Throws()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var propertyName3 = "Color3";
        var indexName = "MyIndexName";

        _provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        _provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        _provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act/Assert
        var exception = Assert.Throws<Exception>(() => _provider.RemoveColumn(testTableName, propertyName2));

        Assert.That(exception.Message, Does.StartWith("Found composite index"));
    }

    /// <summary>
    /// If there is a composite unique constraint (more than one key columns) that contains the target column it should throw.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingUniqueConstraintWithTwoColumnsOneOfThemTargetColumn_Throws()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var propertyName3 = "Color3";
        var indexName = "MyIndexName";

        _provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        _provider.AddUniqueConstraint("Not used in SQLite", testTableName, [propertyName2, propertyName3]);

        _provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        _provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act/Assert
        var exception = Assert.Throws<Exception>(() => _provider.RemoveColumn(testTableName, propertyName2));

        Assert.That(exception.Message, Does.StartWith("Found composite unique constraint"));
    }

    /// <summary>
    /// If there are multiple single uniques and only single column indexes (or no index) it should succeed.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingMultipleSingleUniques_Succeeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var propertyName3 = "Color3";

        _provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique),
            new Column(propertyName3, DbType.Int32, ColumnProperty.Unique)
        );

        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        // Act
        _provider.RemoveColumn(testTableName, propertyName2);
        var tableInfoAfter = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Uniques.Count, Is.EqualTo(2));
        Assert.That(tableInfoAfter.Uniques.Count, Is.EqualTo(1));
    }
}