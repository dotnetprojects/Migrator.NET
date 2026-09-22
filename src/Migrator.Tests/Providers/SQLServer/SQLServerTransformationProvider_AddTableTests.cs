using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SQLServer")]
public class SQLServerTransformationProvider_AddTableTests : Generic_AddTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }

    [Test]
    public void AddTableDateTime()
    {
        var tableName = "Table1";
        var columnName = "Column1";

        Provider.AddTable(tableName, new Column(columnName,DbType.DateTime){IsNullable = false});
        var column = Provider.ReadLegacyColumn(tableName, columnName);

        Assert.That(column.Type, Is.EqualTo(DbType.DateTime));
    }

    [Test]
    public void AddTableDateTime2()
    {
        var tableName = "Table1";
        var columnName = "Column1";

        Provider.AddTable(tableName, new Column(columnName,DbType.DateTime2){IsNullable = false});
        var column = Provider.ReadLegacyColumn(tableName, columnName);

        Assert.That(column.Type, Is.EqualTo(DbType.DateTime2));
    }
}
