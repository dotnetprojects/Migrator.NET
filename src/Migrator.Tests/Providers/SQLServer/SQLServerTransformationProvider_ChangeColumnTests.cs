using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SQLServer")]
public class SQLServerTransformationProvider_ChangeColumnTests : Generic_ChangeColumnTestsBase
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

    [Test]
    public void ChangeColumn_DoesNotRemoveUserOwnedUniqueOrMutateDefinition()
    {
        Provider.AddTable("UserOwned", new Column("Value", DbType.Int32, ColumnProperty.NotNull));
        Provider.AddUniqueConstraint("UX_UserOwned_Value", "UserOwned", "Value");
        var definition = new Column("Value", DbType.Int32, ColumnProperty.NotNull, 3);
        Provider.ChangeColumn("UserOwned", definition);
        Assert.That(Provider.ConstraintExists("UserOwned", "UX_UserOwned_Value"), Is.True);
        Assert.That(definition.DefaultValue, Is.EqualTo(3));
        Assert.That(definition.ColumnProperty, Is.EqualTo(ColumnProperty.NotNull));
    }

    [Test]
    public void ChangeColumn_WithUniqueThenReChangeToNonUnique_UniqueConstraintShouldBeRemoved()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32, ColumnProperty.NotNull));

        // Act
        Provider.ChangeColumn(tableName, new Column(columnName, DbType.Int32, ColumnProperty.NotNull | ColumnProperty.Unique));
        Provider.ChangeColumn(tableName, new Column(columnName, DbType.Int32, ColumnProperty.NotNull));

        // Assert
        var indexes = Provider.GetIndexes(tableName);
        Assert.That(indexes, Is.Empty);
    }
}