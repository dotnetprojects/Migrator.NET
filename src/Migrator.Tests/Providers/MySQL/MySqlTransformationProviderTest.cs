using System.Data;
using Migrator.Framework;
using Migrator.Providers.Mysql;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers.MySQL;

[TestFixture]
[Category("MySql")]
public class MySqlTransformationProviderTest : TransformationProviderConstraintBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.MySQL)
            ?.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new IgnoreException("No MySQL ConnectionString is Set.");
        }

        Provider = new MySqlTransformationProvider(new MysqlDialect(), connectionString, "default", null);
        // _provider.Logger = new Logger(true, new ConsoleWriter());

        AddDefaultTable();
    }

    [TearDown]
    public override void TearDown()
    {
        DropTestTables();
    }

    // [Test,Ignore("MySql doesn't support check constraints")]
    public override void CanAddCheckConstraint()
    {
    }

    [Test]
    public void AddTableWithMyISAMEngine()
    {
        Provider.AddTable("Test", "MyISAM",
                           new Column("Id", DbType.Int32, ColumnProperty.NotNull),
                           new Column("name", DbType.String, 50)
            );
    }
}
