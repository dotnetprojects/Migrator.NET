using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_GetIndexesTestsBase : TransformationProviderBase
{
    [Test]
    public void AddIndex_FilteredIndexGreaterOrEqualThanNumber_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string columnName3 = "TestColumn3";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName,
            new Column(columnName, DbType.Int32),
            new Column(columnName2, DbType.String),
            new Column(columnName3, DbType.Int32)
        );

        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName, columnName2],
                Unique = true,
                FilterItems = [
                    new() { Filter = FilterType.GreaterThanOrEqualTo, ColumnName = columnName, Value = 100 },
                    new() { Filter = FilterType.EqualTo, ColumnName = columnName2, Value = "Hello" },
                ]
            });

        // Act
        var indexes = Provider.GetIndexes(table: tableName);

        var index = indexes.Single();

        var filterItem1 = index.FilterItems.Single(x => x.ColumnName == columnName);
        var filterItem2 = index.FilterItems.Single(x => x.ColumnName == columnName2);

        Assert.That(filterItem1.Filter, Is.EqualTo(FilterType.GreaterThanOrEqualTo));
        Assert.That((long)filterItem1.Value, Is.EqualTo(100));

        Assert.That(filterItem2.Filter, Is.EqualTo(FilterType.EqualTo));
        Assert.That((string)filterItem2.Value, Is.EqualTo("Hello"));
    }
}