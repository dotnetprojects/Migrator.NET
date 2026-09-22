using System.Data;
using System;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.OracleProvider.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_TableExistsTests : OracleTransformationProviderTestBase
{
    [Test]
    public void LegacyForeignKeyOverloadHonorsCascadeDelete()
    {
        Provider.AddTable("CascadeParent", new Column("Id",DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + "CascadeParent", "Id"));
        Provider.AddTable("CascadeChild", new Column("ParentId", DbType.Int32));
        Provider.AddForeignKey("CascadeForeignKey", "CascadeChild", new[] { "ParentId" }, "CascadeParent", new[] { "Id" }, ForeignKeyConstraintType.Cascade);
        Provider.Insert("CascadeParent", new[] { "Id" }, new object[] { 1 });
        Provider.Insert("CascadeChild", new[] { "ParentId" }, new object[] { 1 });
        Provider.ExecuteNonQuery("DELETE FROM CascadeParent");
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM CascadeChild")), Is.Zero);
    }

    [Test]
    public void RemovingTableDoesNotGuessOwnershipOfLegacyNamedSequence()
    {
        Provider.AddTable("UnownedSequenceTable", new Column("Id", DbType.Int32));
        Provider.ExecuteNonQuery("CREATE SEQUENCE UnownedSequenceTable_SEQUENCE");
        try
        {
            Provider.RemoveTable("UnownedSequenceTable");
            Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME='UNOWNEDSEQUENCETABLE_SEQUENCE'")), Is.EqualTo(1));
        }
        finally { Provider.ExecuteNonQuery("DROP SEQUENCE UnownedSequenceTable_SEQUENCE"); }
    }

    [Test]
    public void ExplicitLegacyCleanupDropsSequenceAndTableOwnedTrigger()
    {
        Provider.AddTable("LegacyOwned", new Column("Id", DbType.Int32));
        Provider.ExecuteNonQuery("CREATE SEQUENCE LegacyOwned_SEQUENCE");
        Provider.ExecuteNonQuery("CREATE TRIGGER LegacyOwned_TRIGGER BEFORE INSERT ON LegacyOwned FOR EACH ROW BEGIN SELECT LegacyOwned_SEQUENCE.NEXTVAL INTO :new.Id FROM dual; END;");
        ((OracleTransformationProvider)Provider).RemoveTableWithOwnedSequences("LegacyOwned", "LegacyOwned_SEQUENCE");
        Assert.That(Provider.TableExists("LegacyOwned"), Is.False);
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME='LEGACYOWNED_SEQUENCE'")), Is.Zero);
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM USER_TRIGGERS WHERE TRIGGER_NAME='LEGACYOWNED_TRIGGER'")), Is.Zero);
    }

    [Test]
    public void TableExists_TableExists_Returns()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";

        Provider.AddTable(testTableName,
            new Column(propertyName1, DbType.Int32)
        );

        // Act
        var tableExists = Provider.TableExists(testTableName);

        // Assert
        Assert.That(tableExists, Is.True);
    }

    [Test]
    public void TableExists_TableDoesNotExist_ReturnsFalse()
    {
        // Arrange
        const string myTableName = "MyTable";

        // Act
        var tableExists = Provider.TableExists(myTableName);

        // Assert
        Assert.That(tableExists, Is.False);
    }
}
