using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_AddIndex_Tests : GenericAddIndexTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
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
        var ex = Assert.Throws<OracleException>(() => Provider.Insert(tableName, [columnName, columnName2], [1, "Some other string"]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(ex.Number, Is.EqualTo(1));
    }
}