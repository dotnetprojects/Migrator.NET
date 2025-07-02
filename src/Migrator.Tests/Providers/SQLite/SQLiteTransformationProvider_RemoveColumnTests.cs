using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
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
    /// <summary>
    /// We use a NULL column as new column here. NOT NULL will fail as expected. The user should handle that on his own.
    /// </summary>
    [Test]
    public void RemoveColumn_HavingColumnPropertyUniqueAndIndex_RebuildSucceeds()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var propertyName1 = "Color1";
        var propertyName2 = "Color2";
        var indexName = "MyIndexName";

        _provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyName2, DbType.Int32, ColumnProperty.Unique)
        );

        _provider.AddIndex(indexName, testTableName, [propertyName1, propertyName2]);
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

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName1).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == propertyName2).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);

        var indexAfter = tableInfoAfter.Indexes.Single();
        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1 });
    }
}