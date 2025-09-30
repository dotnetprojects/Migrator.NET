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
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
        Provider.Insert("TestTwo", ["Id", "TestId"], [3, "Not an Int val."]);
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.NotNull));
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.NotNull));
    }
}
