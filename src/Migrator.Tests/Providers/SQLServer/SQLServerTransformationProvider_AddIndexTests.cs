using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_AddIndexTests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
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
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);
        // Unique but no exception is thrown since smaller than 100
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);

        Provider.Insert(tableName, [columnName, columnName2], [100, "Hello"]);
        var sqlException = Assert.Throws<Microsoft.Data.SqlClient.SqlException>(() => Provider.Insert(tableName, [columnName, columnName2], [100, "Hello"]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(sqlException.Number, Is.EqualTo(2601));
    }
}
