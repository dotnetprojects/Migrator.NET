using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

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

    [Test]
    public void AddIndex_FilteredIndexSingle_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";

        const string indexName = "TestIndexName";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int16)
        );

        List<FilterItem> filterItems = [
            new() { Filter = FilterType.EqualTo, ColumnName = columnName1, Value = 1 },
        ];

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName1],
                Unique = true,
                FilterItems = filterItems
            });

        // Assert

        var indexesFromDatabase = Provider.GetIndexes(table: tableName);
        var filteredItemsFromDatabase = indexesFromDatabase.Single().FilterItems;

        // We cannot find out the exact DbType so we compare strings.
        foreach (var filteredItemFromDatabase in filteredItemsFromDatabase)
        {
            var expected = filterItems.Single(x => x.ColumnName.Equals(filteredItemFromDatabase.ColumnName, StringComparison.OrdinalIgnoreCase));
            Assert.That(filteredItemFromDatabase.Filter, Is.EqualTo(expected.Filter));
            Assert.That(Convert.ToString(filteredItemFromDatabase.Value, CultureInfo.InvariantCulture), Is.EqualTo(Convert.ToString(expected.Value, CultureInfo.InvariantCulture)));
        }

        Assert.That(
            filteredItemsFromDatabase.Select(x => x.ColumnName.ToLowerInvariant()),
            Is.EquivalentTo(filterItems.Select(x => x.ColumnName.ToLowerInvariant()))
        );
    }

    /// <summary>
    /// This test is located in the dedicated database type folder not in the base class since partial indexes (Oracle) are not supported in the migrator at this point in time.
    /// </summary>
    [Test]
    public void AddIndex_FilteredIndexMiscellaneousFilterTypesAndDataTypes_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";
        const string columnName2 = "TestColumn2";
        const string columnName3 = "TestColumn3";
        const string columnName4 = "TestColumn4";
        const string columnName5 = "TestColumn5";
        const string columnName6 = "TestColumn6";
        const string columnName7 = "TestColumn7";
        const string columnName8 = "TestColumn8";
        const string columnName9 = "TestColumn9";
        const string columnName10 = "TestColumn10";
        const string columnName11 = "TestColumn11";
        const string columnName12 = "TestColumn12";
        const string columnName13 = "TestColumn13";

        const string indexName = "TestIndexName";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int16),
            new Column(columnName2, DbType.Int32),
            new Column(columnName3, DbType.Int64),
            new Column(columnName4, DbType.UInt16),
            new Column(columnName5, DbType.UInt32),
            new Column(columnName6, DbType.UInt64),
            new Column(columnName7, DbType.String),
            new Column(columnName8, DbType.Int32),
            new Column(columnName9, DbType.Int32),
            new Column(columnName10, DbType.Int32),
            new Column(columnName11, DbType.Int32),
            new Column(columnName12, DbType.Int32),
            new Column(columnName13, DbType.Int32)
        );

        List<FilterItem> filterItems = [
            new() { Filter = FilterType.EqualTo, ColumnName = columnName1, Value = 1 },
            new() { Filter = FilterType.GreaterThan, ColumnName = columnName2, Value = 2 },
            new() { Filter = FilterType.GreaterThanOrEqualTo, ColumnName = columnName3, Value = 2323 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName4, Value = 3434 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName5, Value = -3434 },
            new() { Filter = FilterType.SmallerThan, ColumnName = columnName6, Value = 3434345345 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName7, Value = "asdf" },
            new() { Filter = FilterType.EqualTo, ColumnName = columnName8, Value = 11 },
            new() { Filter = FilterType.GreaterThan, ColumnName = columnName9, Value = 22 },
            new() { Filter = FilterType.GreaterThanOrEqualTo, ColumnName = columnName10, Value = 33 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName11, Value = 44 },
            new() { Filter = FilterType.SmallerThan, ColumnName = columnName12, Value = 55 },
            new() { Filter = FilterType.SmallerThanOrEqualTo, ColumnName = columnName13, Value = 66 }
        ];

        // Act
        var addIndexSql = Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [
                    columnName1,
                    columnName2,
                    columnName3,
                    columnName4,
                    columnName5,
                    columnName6,
                    columnName7,
                    columnName8,
                    columnName9,
                    columnName10,
                    columnName11,
                    columnName12,
                    columnName13
                ],
                Unique = true,
                FilterItems = filterItems
            });

        // Assert

        var indexesFromDatabase = Provider.GetIndexes(table: tableName);
        var filteredItemsFromDatabase = indexesFromDatabase.Single().FilterItems;

        // We cannot find out the exact DbType so we compare strings.
        foreach (var filteredItemFromDatabase in filteredItemsFromDatabase)
        {
            var expected = filterItems.Single(x => x.ColumnName.Equals(filteredItemFromDatabase.ColumnName, StringComparison.OrdinalIgnoreCase));
            Assert.That(filteredItemFromDatabase.Filter, Is.EqualTo(expected.Filter));
            Assert.That(Convert.ToString(filteredItemFromDatabase.Value, CultureInfo.InvariantCulture), Is.EqualTo(Convert.ToString(expected.Value, CultureInfo.InvariantCulture)));
        }

        Assert.That(
            filteredItemsFromDatabase.Select(x => x.ColumnName.ToLowerInvariant()),
            Is.EquivalentTo(filterItems.Select(x => x.ColumnName.ToLowerInvariant()))
        );

        var expectedSql = "CREATE UNIQUE INDEX TestIndexName ON TestTable (TestColumn1, TestColumn2, TestColumn3, TestColumn4, TestColumn5, TestColumn6, TestColumn7, TestColumn8, TestColumn9, TestColumn10, TestColumn11, TestColumn12, TestColumn13) WHERE TestColumn1 = 1 AND TestColumn2 > 2 AND TestColumn3 >= 2323 AND TestColumn4 <> 3434 AND TestColumn5 <> -3434 AND TestColumn6 < 3434345345 AND TestColumn7 <> 'asdf' AND TestColumn8 = 11 AND TestColumn9 > 22 AND TestColumn10 >= 33 AND TestColumn11 <> 44 AND TestColumn12 < 55 AND TestColumn13 <= 66";

        Assert.That(addIndexSql, Is.EqualTo(expectedSql));
    }

    private string GetCreateIndexSqlString(string indexName)
    {
        using var cmd = Provider.CreateCommand();
        using var reader = Provider.ExecuteQuery(cmd, $"SELECT sql FROM sqlite_master WHERE type='index' AND lower(name)=lower('{indexName}')");
        reader.Read();

        return reader.IsDBNull(0) ? null : (string)reader[0];
    }
}
