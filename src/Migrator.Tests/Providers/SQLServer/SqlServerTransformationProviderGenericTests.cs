using System.Data;
using DotNetProjects.Migrator.Framework;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SQLServer")]
public class SqlServerTransformationProviderGenericTests : TransformationProviderGenericMiscConstraintBase
{
    [Test]
    public void RawSqlDefaultsRoundTripThroughMetadata() => RawDefaultRegression.AssertRoundTrip(Provider);

    [Test]
    public void NonClusteredPrimaryKeyRoundTripsAsConstraint()
    {
        Provider.AddTable("NonClusteredKey", new Column("Id", DbType.Int32),
            new PrimaryKeyConstraint("PK_NonClusteredKey", "Id") { NonClustered = true });
        var key = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<PrimaryKeyConstraint>(Provider.GetTableConstraints("NonClusteredKey")));
        Assert.That(key.NonClustered, Is.True);
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();

        AddDefaultTable();
    }

    [Test]
    public void SemanticCollationAndRawDefaultsExecuteInBothApis()
    {
        Provider.AddTable("SemanticNames", new Column("Name", DbType.String, 40) { Collation = Collation.CaseInsensitive },
            new Column("Token", DbType.Guid) { DefaultValue = RawSql.Insert("NEWID()") });
        Provider.Insert("SemanticNames", ["Name"], ["é"]);
        Assert.That(System.Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM SemanticNames WHERE Name=N'É'")), Is.EqualTo(1));
        Assert.That(System.Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM SemanticNames WHERE Name=N'e'")), Is.EqualTo(0));
        Assert.That(Provider.ExecuteScalar("SELECT Token FROM SemanticNames"), Is.TypeOf<System.Guid>());
        var builder = new DotNetProjects.Migrator.Framework.Fluent.MigrationBuilder();
        builder.Create.Table("SemanticNamesFluent").WithColumn("Name").AsString(40).WithCollation(Collation.CaseSensitive)
            .WithColumn("Token").AsGuid().WithDefaultValue(RawSql.Insert("NEWID()"));
        builder.Apply(Provider);
        Provider.Insert("SemanticNamesFluent", ["Name"], ["é"]);
        Assert.That(System.Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM SemanticNamesFluent WHERE Name=N'É'")), Is.EqualTo(0));
        Assert.That(Provider.ExecuteScalar("SELECT Token FROM SemanticNamesFluent"), Is.TypeOf<System.Guid>());
    }

    [Test]
    public void ByteColumnWillBeCreatedAsBlob()
    {
        Provider.AddColumn("TestTwo", "BlobColumn", DbType.Byte);
        Assert.That(Provider.ColumnExists("TestTwo", "BlobColumn"), Is.True);
    }

    [Test]
    public void InstanceForProvider()
    {
        var localProv = Provider["sqlserver"];
        Assert.That(localProv is SqlServerTransformationProvider, Is.True);

        var localProv2 = Provider["foo"];
        Assert.That(localProv2 is NoOpTransformationProvider, Is.True);
    }

    [Test]
    public void QuoteCreatesProperFormat()
    {
        var dialect = new SqlServerDialect();

        Assert.That("[foo]", Is.EqualTo(dialect.Quote("foo")));
    }

    [Test]
    public void TableExistsShouldWorkWithBracketsAndSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("[dbo].[TestTwo]"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("dbo.TestTwo"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithTableNamesWithBracket()
    {
        Assert.That(Provider.TableExists("[TestTwo]"), Is.True);
    }
}
