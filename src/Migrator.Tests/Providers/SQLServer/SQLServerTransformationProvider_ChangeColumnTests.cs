using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;

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

    [TestCase(false), TestCase(true)]
    public void ChangeColumnRemovesOwnedUniqueFromTableOrColumnCreation(bool addColumn)
    {
        var definition = new Column("Value", DbType.Int32, ColumnProperty.NotNull | ColumnProperty.Unique);
        if (addColumn)
        {
            Provider.AddTable("CreatedUnique", new Column("Id", DbType.Int32));
            Provider.AddColumn("CreatedUnique", definition);
        }
        else Provider.AddTable("CreatedUnique", definition);
        Provider.ChangeColumn("CreatedUnique", new Column("Value", DbType.Int32, ColumnProperty.NotNull));
        Provider.Insert("CreatedUnique", new[] { "Value" }, new object[] { 1 });
        Provider.Insert("CreatedUnique", new[] { "Value" }, new object[] { 1 });
        Assert.That(definition.ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);
        Assert.That(Provider.GetIndexes("CreatedUnique"), Is.Empty);
    }

    [Test]
    public void OwnershipAdoptionRejectsCompositeConstraints()
    {
        Provider.AddTable("CompositeOwned", new Column("FirstId", DbType.Int32), new Column("SecondId", DbType.Int32));
        Provider.AddUniqueConstraint("UserComposite", "CompositeOwned", "FirstId", "SecondId");
        Assert.Throws<MigrationException>(() => ((SqlServerTransformationProvider)Provider).AdoptColumnUniqueConstraint("CompositeOwned", "FirstId", "UserComposite"));
        Assert.That(Provider.ConstraintExists("CompositeOwned", "UserComposite"), Is.True);
    }

    [Test]
    public void ExplicitOwnershipAdoptionAllowsLegacyUniqueRemoval()
    {
        Provider.AddTable("LegacyUnique", new Column("Value", DbType.Int32));
        Provider.AddUniqueConstraint("LegacyUniqueConstraint", "LegacyUnique", "Value");
        var sqlServer = (SqlServerTransformationProvider)Provider;
        sqlServer.AdoptColumnUniqueConstraint("LegacyUnique", "Value", "LegacyUniqueConstraint");
        sqlServer.AdoptColumnUniqueConstraint("LegacyUnique", "Value", "LegacyUniqueConstraint");
        Provider.ChangeColumn("LegacyUnique", new Column("Value", DbType.Int32, ColumnProperty.Null));
        Assert.That(Provider.ConstraintExists("LegacyUnique", "LegacyUniqueConstraint"), Is.False);
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