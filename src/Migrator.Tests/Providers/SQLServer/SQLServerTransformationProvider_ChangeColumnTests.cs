
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SQLServerTransformationProvider_ChangeColumnTests : Generic_AddIndexTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }

    [Test]
    public void ChangeColumn_DateTimeToDateTime2_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";

        Provider.AddTable(tableName, new Column(columnName, DbType.DateTime, ColumnProperty.NotNull));
        var columnBefore = Provider.GetColumnByName(tableName, columnName);

        // Act
        Provider.ChangeColumn(tableName, new Column(columnName, DbType.DateTime2, ColumnProperty.NotNull));

        // Assert
        var columnAfter = Provider.GetColumnByName(tableName, columnName);

        Assert.That(columnBefore.Type == DbType.DateTime);
        Assert.That(columnAfter.Type == DbType.DateTime2);
    }
}