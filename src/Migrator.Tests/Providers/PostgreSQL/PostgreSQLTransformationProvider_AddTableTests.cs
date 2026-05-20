using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_AddTableTests : Generic_AddTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }

    [Test]
    public void AddTableTime()
    {
        var tableName = "Table1";
        var columnName = "Column1";

        Provider.AddTable(tableName, new Column(columnName, DbType.Time, ColumnProperty.NotNull));
        var column = Provider.GetColumnByName(tableName, columnName);

        Assert.That(column.Type, Is.EqualTo(DbType.Time));
    }
}
