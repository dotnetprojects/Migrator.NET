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
    public void ChangeColumnPreservesExplicitUniqueFromTableOrColumnCreation(bool addColumn)
    {
        var definition = new Column("Value", DbType.Int32) { IsNullable = false };
        if (addColumn)
        {
            Provider.AddTable("CreatedUnique", new Column("Id", DbType.Int32));
            Provider.AddColumn("CreatedUnique", definition);
            Provider.AddUniqueConstraint("UQ_Created", "CreatedUnique", "Value");
        }
        else Provider.AddTable("CreatedUnique", definition,
            new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_Created", "Value"));
        Provider.ChangeColumn("CreatedUnique", new Column("Value", DbType.Int32) { IsNullable = false });
        Assert.That(Provider.ConstraintExists("CreatedUnique", "UQ_Created"), Is.True);
        Assert.That(definition.IsNullable, Is.False);
        Provider.Insert("CreatedUnique", new[] { "Value" }, new object[] { 1 });
        Assert.Catch(() => Provider.Insert("CreatedUnique", new[] { "Value" }, new object[] { 1 }));
    }

    [Test]
    public void ExplicitUniqueRemovalAllowsDuplicates()
    {
        Provider.AddTable("LegacyUnique", new Column("Value", DbType.Int32));
        Provider.AddUniqueConstraint("LegacyUniqueConstraint", "LegacyUnique", "Value");
        Provider.ChangeColumn("LegacyUnique", new Column("Value", DbType.Int32));
        Assert.That(Provider.ConstraintExists("LegacyUnique", "LegacyUniqueConstraint"), Is.True);
        Provider.RemoveConstraint("LegacyUnique", "LegacyUniqueConstraint");
        Provider.ExecuteNonQuery("INSERT INTO LegacyUnique VALUES (1), (1)");
        Assert.That(System.Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM LegacyUnique")), Is.EqualTo(2));
    }

    [Test]
    public void ChangeColumn_DateTimeToDateTime2_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";

        Provider.AddTable(tableName, new Column(columnName,DbType.DateTime){IsNullable = false});
        var columnBefore = Provider.GetColumnByName(tableName, columnName);

        // Act
        Provider.ChangeColumn(tableName, new Column(columnName,DbType.DateTime2){IsNullable = false});

        // Assert
        var columnAfter = Provider.GetColumnByName(tableName, columnName);

        Assert.That(columnBefore.Type == DbType.DateTime);
        Assert.That(columnAfter.Type == DbType.DateTime2);
    }

    [Test]
    public void ChangeColumn_DoesNotRemoveUserOwnedUniqueOrMutateDefinition()
    {
        Provider.AddTable("UserOwned", new Column("Value",DbType.Int32){IsNullable = false});
        Provider.AddUniqueConstraint("UX_UserOwned_Value", "UserOwned", "Value");
        var definition = new Column("Value",DbType.Int32){IsNullable = false, DefaultValue = 3};
        Provider.ChangeColumn("UserOwned", definition);
        Assert.That(Provider.ConstraintExists("UserOwned", "UX_UserOwned_Value"), Is.True);
        Assert.That(definition.DefaultValue, Is.EqualTo(3));
        Assert.That(definition.IsNullable, Is.False);
    }

    [Test]
    public void ChangeColumn_WithUniqueThenReChangeToNonUnique_UniqueConstraintShouldBeRemoved()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";

        Provider.AddTable(tableName, new Column(columnName,DbType.Int32){IsNullable = false});

        // Act
        Provider.ChangeColumn(tableName, new Column(columnName,DbType.Int32){IsNullable = false});
        Provider.ChangeColumn(tableName, new Column(columnName,DbType.Int32){IsNullable = false});

        // Assert
        var indexes = Provider.GetIndexes(tableName);
        Assert.That(indexes, Is.Empty);
    }
}