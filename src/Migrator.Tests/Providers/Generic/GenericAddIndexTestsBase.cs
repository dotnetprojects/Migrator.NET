using System;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.SchemaBuilder;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.Generic;

public abstract class GenericAddIndexTestsBase : TransformationProviderBase
{
    [Test]
    public void AddIndex_TableDoesNotExist()
    {
        // Act
        Assert.Throws<MigrationException>(() => Provider.AddIndex("NotExistingTable", new Index()));
        Assert.Throws<MigrationException>(() => Provider.AddIndex("NotExistingIndex", "NotExistingTable", "column"));
    }

    [Test]
    public void AddIndex_UsingIndexInstanceOverload_ShouldBeReadable()
    {
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32));

        // Arrange
        Provider.AddIndex(tableName, new Index { Name = indexName, KeyColumns = [columnName] });

        // Act
        var indexes = Provider.GetIndexes(tableName);

        var index = indexes.Single();

        Assert.That(index.Name, Is.EqualTo(indexName).IgnoreCase);
        Assert.That(index.KeyColumns.Single(), Is.EqualTo(columnName).IgnoreCase);
    }

    [Test]
    public void AddIndex_Unique_ShouldThrowOnSecondInsert()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32));

        // Act
        Provider.AddIndex(tableName, new Index { Name = indexName, KeyColumns = [columnName], Unique = true });

        // Assert
        Provider.Insert(tableName, [columnName], [1]);
        var oracleException = Assert.Throws<OracleException>(() => Provider.Insert(tableName, [columnName], [1]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(oracleException.Number, Is.EqualTo(1));
    }

    [Test]
    public void AddIndex_UsingNonIndexInstanceOverload_ShouldBeReadable()
    {
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32));

        // Arrange
        Provider.AddIndex(tableName, indexName, columnName);

        // Act
        var indexes = Provider.GetIndexes(tableName);

        var index = indexes.Single();

        Assert.That(index.Name, Is.EqualTo(indexName).IgnoreCase);
        Assert.That(index.KeyColumns.Single(), Is.EqualTo(columnName).IgnoreCase);
    }

    [Test]
    public void AddIndex_FilteredIndexGreaterOrEqualThanNumber_Success()
    {

    }
}