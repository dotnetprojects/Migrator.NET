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
            new Column(propertyName1,DbType.Int32){IsNullable = false},
            new Column(propertyName2,DbType.Int32),new PrimaryKeyConstraint("PK_" + testTableName, propertyName1),new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_" + testTableName + "_" + propertyName2, propertyName2)        );

        Provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        Provider.AddColumn(table: testTableName, new Column(newColumn,DbType.String));
        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}, {newColumn}) VALUES (2, 3, 'Hello')");

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Assert.That((tableInfoBefore.PrimaryKey?.KeyColumns.Contains(propertyName1) == true), Is.True);
        Assert.That(tableInfoBefore.Uniques.Any(u => u.KeyColumns.Length == 1 && u.KeyColumns[0] == propertyName2), Is.True);

        Assert.That((tableInfoAfter.PrimaryKey?.KeyColumns.Contains(propertyName1) == true), Is.True);
        Assert.That(tableInfoAfter.Uniques.Any(u => u.KeyColumns.Length == 1 && u.KeyColumns[0] == propertyName2), Is.True);

        var indexAfter = tableInfoAfter.Indexes.Single();
        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1, propertyName2 });
    }

    /// <summary>
    /// NOT NULL is implicitly set by the migrator for non-composite primary key
    /// </summary>
    [Test]
    public void AddColumn_HavingNullInPrimaryKey_HasNotNullAfterAddAnotherColumn()
    {
        // Arrange/Act
        Provider.ExecuteNonQuery("CREATE TABLE Common_Language (LanguageID TEXT PRIMARY KEY)");

        Provider.AddColumn("Common_Language", "Enabled", DbType.Boolean);

        var tableInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo("Common_Language");
        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Language");

        var column = tableInfo.Columns.Single(x => x.Name == "LanguageID");

        // Assert        
        Assert.That(column.IsNullable, Is.False);
        Assert.That(tableInfo.PrimaryKey.KeyColumns, Is.EqualTo(new[] { "LanguageID" }));
    }

    [Test]
    public void AddColumn_HavingNullInPrimaryKey_HasNOTNULLAfterAddAnotherColumn()
    {
        // Arrange/Act
        Provider.ExecuteNonQuery("CREATE TABLE Common_Language (LanguageID TEXT NOT NULL PRIMARY KEY)");

        Provider.AddColumn("Common_Language", "Enabled", DbType.Boolean);

        var tableInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo("Common_Language");
        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Language");

        var column = tableInfo.Columns.Single(x => x.Name == "LanguageID");

        // Assert        
        Assert.That(column.IsNullable, Is.False);
        Assert.That(tableInfo.PrimaryKey.KeyColumns, Is.EqualTo(new[] { "LanguageID" }));
    }

    [Test]
    public void AddColumn_HavingNotNullInPrimaryKey_Succeds()
    {
        // Arrange/Act
        Provider.ExecuteNonQuery("CREATE TABLE Common_Language (LanguageID INT NOT NULL PRIMARY KEY)");

        Provider.AddColumn("Common_Language", "Enabled", DbType.Boolean);

        var tableInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo("Common_Language");
        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Language");

        var column = tableInfo.Columns.Single(x => x.Name == "LanguageID");
        var hasNull = column.IsNullable;

        // Assert  
        Assert.That(column.IsNullable, Is.False);
        Assert.That(tableInfo.PrimaryKey.KeyColumns, Is.EqualTo(new[] { "LanguageID" }));
        Assert.That(hasNull, Is.False);
    }
}
