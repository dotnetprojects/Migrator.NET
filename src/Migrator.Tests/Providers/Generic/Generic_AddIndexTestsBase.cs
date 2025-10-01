using System.Data;
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
    public void AddIndex_AddAlreadyExistingIndex_Throws()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32));
        Provider.AddIndex(tableName, new Index { Name = indexName, KeyColumns = [columnName] });

        // Act/Assert
        // Add already existing index
        Assert.Throws<MigrationException>(() => Provider.AddIndex(tableName, new Index { Name = indexName, KeyColumns = [columnName] }));
    }

    [Test]
    public void AddIndex_IncludeColumnsContainsColumnThatExistInKeyColumns_Throws()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName1, DbType.Int32));

        Assert.Throws<MigrationException>(() => Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName1],
                IncludeColumns = [columnName1]
            }));
    }

    [Test]
    public void AddIndex_ColumnNameUsedInFilterItemDoesNotExistInKeyColumns_Throws()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";
        const string columnName2 = "TestColumn2";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int32),
            new Column(columnName2, DbType.Int32)
        );

        Assert.Throws<MigrationException>(() => Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName1],
                FilterItems = [new FilterItem { Filter = FilterType.GreaterThan, ColumnName = columnName2, Value = 12 }]
            }));
    }

    [Test]
    public void AddIndex_UsingIndexInstanceOverload_NonUnique_ShouldBeReadable()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32));

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

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32));

        // Act
        Provider.AddIndex(indexName, tableName, columnName);

        // Assert
        var indexes = Provider.GetIndexes(tableName);

        var index = indexes.Single();

        Assert.That(index.Name, Is.EqualTo(indexName).IgnoreCase);
        Assert.That(index.KeyColumns.Single(), Is.EqualTo(columnName).IgnoreCase);
    }
}