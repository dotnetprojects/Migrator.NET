using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_AddIndexTestsBase : TransformationProviderBase
{
    [Test]
    public void AddIndex_TableDoesNotExist()
    {
        // Act
        Assert.Throws<MigrationException>(() => Provider.AddIndex("NotExistingTable", new Index()));
        Assert.Throws<MigrationException>(() => Provider.AddIndex("NotExistingIndex", "NotExistingTable", "column"));
    }

    [Test]
    public void AddIndex_UsingIndexInstanceOverload_NonUnique_ShouldBeReadable()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32));

        // Act
        Provider.AddIndex(tableName, new Index { Name = indexName, KeyColumns = [columnName] });

        // Assert
        var indexes = Provider.GetIndexes(tableName);

        var index = indexes.Single();

        Assert.That(index.Name, Is.EqualTo(indexName).IgnoreCase);
        Assert.That(index.KeyColumns.Single(), Is.EqualTo(columnName).IgnoreCase);
    }

    [Test]
    public void AddIndex_UsingNonIndexInstanceOverload_NonUnique_ShouldBeReadable()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32));

        // Act
        Provider.AddIndex(indexName, tableName, columnName);

        // Assert
        var indexes = Provider.GetIndexes(tableName);

        var index = indexes.Single();

        Assert.That(index.Name, Is.EqualTo(indexName).IgnoreCase);
        Assert.That(index.KeyColumns.Single(), Is.EqualTo(columnName).IgnoreCase);
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
        Provider.AddIndex(tableName,
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
                    columnName12
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
    }
}