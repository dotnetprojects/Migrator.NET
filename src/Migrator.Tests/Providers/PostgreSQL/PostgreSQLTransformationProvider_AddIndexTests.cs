using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Generic;
using Npgsql;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_AddIndexTests : Generic_AddIndexTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }

    [Test]
    public void AddTableWithCompoundPrimaryKey()
    {
        Provider.AddTable("Test",
            new Column("PersonId", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column("AddressId", DbType.Int32, ColumnProperty.PrimaryKey)
        );

        Assert.That(Provider.TableExists("Test"), Is.True, "Table doesn't exist");
        Assert.That(Provider.PrimaryKeyExists("Test", "PK_Test"), Is.True, "Constraint doesn't exist");
    }

    [Test]
    public void AddIndex_Unique_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32), new Column(columnName2, DbType.String));

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName],
                Unique = true,
            });

        // Assert
        var indexes = Provider.GetIndexes(tableName);
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);
        var ex = Assert.Throws<PostgresException>(() => Provider.Insert(tableName, [columnName, columnName2], [1, "Some other string"]));
        var index = indexes.Single();

        Assert.That(index.Unique, Is.True);
        // Need to compare message string since ErrorNumber does not hold a positive number.
        Assert.That(ex.Message, Does.StartWith("23505: duplicate key value violates unique constraint"));
        Assert.That(ex.SqlState, Is.EqualTo("23505"));
    }

    [Test]
    public void AddIndex_FilteredIndexGreaterOrEqualThanNumber_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32), new Column(columnName2, DbType.String));

        // Act
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

        // Assert
        var index = Provider.GetIndexes(tableName).Single();
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);
        // Unique but no exception is thrown since smaller than 100
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);

        Provider.Insert(tableName, [columnName, columnName2], [100, "Hello"]);
        var ex = Assert.Throws<PostgresException>(() => Provider.Insert(tableName, [columnName, columnName2], [100, "Hello"]));

        Assert.That(index.Unique, Is.True);
        Assert.That(ex.SqlState, Is.EqualTo("23505"));
    }

    [Test]
    public void AddIndex_IncludeColumnsSingle_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32), new Column(columnName2, DbType.String));

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName],
                Unique = true,
                IncludeColumns = [columnName2]
            });

        // Assert
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(index.KeyColumns.Single, Is.EqualTo(columnName).IgnoreCase);
        Assert.That(index.IncludeColumns.Single, Is.EqualTo(columnName2).IgnoreCase);
    }

    [Test]
    public void AddIndex_IncludeColumnsMultiple_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string columnName3 = "TestColumn3";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32), new Column(columnName2, DbType.String), new Column(columnName3, DbType.Boolean));

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName],
                Unique = true,
                IncludeColumns = [columnName2, columnName3]
            });

        // Assert
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(index.KeyColumns.Single, Is.EqualTo(columnName).IgnoreCase);
        Assert.That(index.IncludeColumns, Is.EquivalentTo([columnName2, columnName3])
            .Using<string>((x, y) => string.Compare(x, y, ignoreCase: true)));
    }
}
