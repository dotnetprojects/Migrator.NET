using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
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
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);
        var ex = Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName, columnName2], [1, "Some other string"]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(ex.ErrorCode, Is.EqualTo(19));
    }
}
