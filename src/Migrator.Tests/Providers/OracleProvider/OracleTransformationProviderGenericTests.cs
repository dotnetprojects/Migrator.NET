using System.Data;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProviderGenericTests : TransformationProviderGenericMiscConstraintBase
{
    [Test]
    public void RawSqlDefaultsRoundTripThroughMetadata() => RawDefaultRegression.AssertRoundTrip(Provider);

    [Test]
    public void ForeignKeyMetadataPreservesOrderedPairsAndDeleteAction()
    {
        Provider.AddTable("MetaParents", new Column("FirstId", DbType.Int32), new Column("SecondId", DbType.Int32),
            new PrimaryKeyConstraint("PK_MetaParents", "SecondId", "FirstId"));
        Provider.AddTable("MetaChildren", new Column("LeftId", DbType.Int32), new Column("RightId", DbType.Int32));
        Provider.AddForeignKey("FK_MetaPair", "MetaChildren", ["LeftId", "RightId"], "MetaParents", ["SecondId", "FirstId"], ForeignKeyConstraintType.Cascade);
        var key = System.Linq.Enumerable.Single(Provider.GetForeignKeyConstraints("MetaChildren"));
        Assert.That(key.ChildColumns, Is.EqualTo(new[] { "LEFTID", "RIGHTID" }));
        Assert.That(key.ParentColumns, Is.EqualTo(new[] { "SECONDID", "FIRSTID" }));
        Assert.That(key.OnDelete, Is.EqualTo("CASCADE"));
        Assert.That(key.OnUpdate, Is.EqualTo("NO ACTION"));
    }

    [Test]
    public void UnsupportedIndexOptionsFailExplicitly()
    {
        Assert.Throws<System.NotSupportedException>(() => Provider.AddIndex("TestTwo",
            new DotNetProjects.Migrator.Framework.Index { Name = "IX_Unsupported", KeyColumns = ["Id"], IncludeColumns = ["TestId"] }));
        Assert.That(Provider.IndexExists("TestTwo", "IX_Unsupported"), Is.False);
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();

        AddDefaultTable();
    }

    [Test]
    public void ChangeColumn_FromNotNullToNotNull()
    {
        Provider.ExecuteNonQuery("DELETE FROM TestTwo");
        Provider.ChangeColumn("TestTwo", new Column("TestId",DbType.String,50));
        Provider.Insert("TestTwo", ["Id", "TestId"], [3, "Not an Int val."]);
        Provider.ChangeColumn("TestTwo", new Column("TestId",DbType.String,50){IsNullable = false});
        Provider.ChangeColumn("TestTwo", new Column("TestId",DbType.String,50){IsNullable = false});
    }
}
