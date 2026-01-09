using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddColumnTests : SQLiteTransformationProviderTestBase
{
    /// <summary>
    /// We use a NULL column as new column here. NOT NULL will fail as expected. The user should handle that on his own.
    /// </summary>
    [Test]
    public void AddColumn_HavingColumnPropertyUniqueAndIndex_RebuildSucceeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";
        const string newColumn = "NewColumn";
        const string indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        Provider.AddColumn(table: testTableName, new Column(newColumn, DbType.String, ColumnProperty.Null));
        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}, {newColumn}) VALUES (2, 3, 'Hello')");

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        var indexAfter = tableInfoAfter.Indexes.Single();
        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1, propertyName2 });
    }

    [Test]
    public void AddColumn_HavingNullInPrimaryKey_Succeds()
    {
        // Arrange/Act
        Provider.ExecuteNonQuery("CREATE TABLE Common_Language (LanguageID TEXT PRIMARY KEY)");

        Provider.AddColumn("Common_Language", "Enabled", DbType.Boolean);

        var tableInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo("Common_Language");
        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Language");

        var columnProperty = tableInfo.Columns.Single(x => x.Name == "LanguageID").ColumnProperty;
        var hasNull = columnProperty.IsSet(ColumnProperty.Null);

        // Assert        
        Assert.That(hasNull, Is.False);
    }
}
