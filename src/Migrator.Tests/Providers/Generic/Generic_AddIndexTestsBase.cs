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
}