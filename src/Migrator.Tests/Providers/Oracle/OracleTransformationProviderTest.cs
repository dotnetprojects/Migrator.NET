using System.Data;
using Migrator.Framework;
using Migrator.Providers.Oracle;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProviderTest : TransformationProviderConstraintBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.Oracle)
            ?.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new IgnoreException("No Oracle ConnectionString is Set.");
        }

        Provider = new OracleTransformationProvider(new OracleDialect(), connectionString, null, "default", null);
        Provider.BeginTransaction();

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
