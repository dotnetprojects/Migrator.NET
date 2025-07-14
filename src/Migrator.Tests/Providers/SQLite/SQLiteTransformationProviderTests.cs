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
public class SQLiteTransformationProviderTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void GetTables()
    {
        var tables = Provider.GetTables();

        Assert.That("TestTwo", Is.EqualTo(tables.Single()));
    }

    [Test]
    public void CanParseColumnDefForNotNull()
    {
        const string nullString = "bar TEXT";
        const string notNullString = "baz INTEGER NOT NULL";

        Assert.That(((SQLiteTransformationProvider)Provider).IsNullable(nullString), Is.True);
        Assert.That(((SQLiteTransformationProvider)Provider).IsNullable(notNullString), Is.False);
    }

    [Test]
    public void RemoveDefaultValue_Succeeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var columnName = "Bla";

        Provider.AddTable(testTableName, new Column(columnName, DbType.Int32, (object)55));
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);
        var createScriptBefore = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(testTableName);

        // Act
        Provider.RemoveColumnDefaultValue(testTableName, columnName);

        // Assert
        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);
        var createScriptAfter = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(testTableName);
        var tableNames = ((SQLiteTransformationProvider)Provider).GetTables();

        Assert.That(tableInfoBefore.Columns.Single().DefaultValue, Is.EqualTo(55));
        Assert.That(tableInfoAfter.Columns.Single().DefaultValue, Is.Null);
        Assert.That(createScriptBefore, Does.Contain("DEFAULT 55"));
        Assert.That(createScriptAfter, Does.Not.Contain("DEFAULT"));

        // Check for intermediate table residues.
        Assert.That(tableNames.Where(x => x.Contains(testTableName)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddPrimaryKey_CompositePrimaryKey_Succeeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";

        Provider.AddTable(testTableName,
            new Column("Id", DbType.Int32),
            new Column("Color", DbType.Int32),
            new Column("NotAPrimaryKey", DbType.Int32)
        );

        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        // Act
        Provider.AddPrimaryKey("MyPrimaryKeyName", testTableName, "Id", "Color");

        // Assert
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "Id").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "Color").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "NotAPrimaryKey").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);
        var tableNames = ((SQLiteTransformationProvider)Provider).GetTables();

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "Id").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "Color").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "NotAPrimaryKey").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        // Check for intermediate table residues.
        Assert.That(tableNames.Where(x => x.Contains(testTableName)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddPrimaryKey_HavingColumnPropertyUniqueAndIndex_RebuildSucceeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.Unique | ColumnProperty.NotNull),
            new Column(propertyName2, DbType.Int32)
        );

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        ((SQLiteTransformationProvider)Provider).AddPrimaryKey("MyPrimaryKeyName", testTableName, [propertyName1]);

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(1));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        var indexAfter = tableInfoAfter.Indexes.Single();
        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1, propertyName2 });
    }

    [Test]
    public void RemovePrimaryKey_HavingColumnPropertyUniqueAndIndex_RebuildSucceeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        ((SQLiteTransformationProvider)Provider).RemovePrimaryKey(tableName: testTableName);

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(1));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        var indexAfter = tableInfoAfter.Indexes.Single();
        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1, propertyName2 });
    }

    [Test]
    public void RemoveAllIndexes_HavingIndexAndUnique_RebuildSucceeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32)
        );

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        Provider.AddUniqueConstraint("MyConstraint", testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.AddUniqueConstraint("MyUniqueConstraintName", testTableName, [propertyName1, propertyName2]);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        ((SQLiteTransformationProvider)Provider).RemoveAllIndexes(tableName: testTableName);

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(1));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);

        Assert.That(tableInfoBefore.Uniques, Is.Not.Empty);
        Assert.That(tableInfoBefore.Indexes, Is.Not.Empty);
        Assert.That(tableInfoAfter.Uniques, Is.Empty);
        Assert.That(tableInfoAfter.Indexes, Is.Empty);
    }
}
