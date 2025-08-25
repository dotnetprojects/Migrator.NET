using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddIndexTests : Generic_AddIndexTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
    }

    [Test]
    public void AddIndex_Unique_Success()
    {
        // Arrange
        const string columnName1 = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string indexName = "TestIndexName";
        const string tableName = "TestTable";

        Provider.AddTable(tableName, new Column(columnName1, DbType.Int32), new Column(columnName2, DbType.String));

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                KeyColumns = [columnName1],
                Name = indexName,
                Unique = true,
            });

        // Assert
        Provider.Insert(tableName, [columnName1, columnName2], [1, "Hello"]);
        var ex = Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [1, "Some other string"]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);

        // Unique violation
        Assert.That(ex.ErrorCode, Is.EqualTo(19));
    }

    [Test]
    public void AddIndex_FilteredIndexGreaterOrEqualThanNumber_Success()
    {
        // Arrange
        const string columnName1 = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string columnName3 = "TestColumn3";
        const string columnName4 = "TestColumn4";
        const string indexName = "TestIndexName";
        const string tableName = "TestTable";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int32),
            new Column(columnName2, DbType.String),
            new Column(columnName3, DbType.Boolean),
            new Column(columnName4, DbType.Int32)
        );

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName1, columnName2, columnName3],
                Unique = true,
                FilterItems = [
                    new() { Filter = FilterType.GreaterThanOrEqualTo, ColumnName = columnName1, Value = 100 },
                    new() { Filter = FilterType.EqualTo, ColumnName = columnName2, Value = "Hello" },
                    new() { Filter = FilterType.EqualTo, ColumnName = columnName3, Value = true },
                ]
            });

        // We remove column to invoke a recreation of the table. 
        Provider.RemoveColumn(tableName, columnName4);

        // Assert
        Provider.Insert(tableName, [columnName1, columnName2, columnName3], [1, "Hello", true]);
        // Unique but no exception should be thrown since the integer value is smaller than 100 - not within the filter restriction.
        Provider.Insert(tableName, [columnName1, columnName2, columnName3], [1, "Hello", true]);

        Provider.Insert(tableName, [columnName1, columnName2, columnName3], [100, "Hello", true]);
        var sqliteException = Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2, columnName3], [100, "Hello", true]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        // Unique violation
        Assert.That(sqliteException.ErrorCode, Is.EqualTo(19));

        var indexScriptFromDatabase = GetCreateIndexSqlString(indexName);

        Assert.That(indexScriptFromDatabase, Is.EqualTo("CREATE UNIQUE INDEX TestIndexName ON TestTable (TestColumn, TestColumn2, TestColumn3) WHERE TestColumn >= 100 AND TestColumn2 = 'Hello' AND TestColumn3 = 1"));
    }

    private string GetCreateIndexSqlString(string indexName)
    {
        using var cmd = Provider.CreateCommand();
        using var reader = Provider.ExecuteQuery(cmd, $"SELECT sql FROM sqlite_master WHERE type='index' AND lower(name)=lower('{indexName}')");
        reader.Read();

        return reader.IsDBNull(0) ? null : (string)reader[0];
    }
}
