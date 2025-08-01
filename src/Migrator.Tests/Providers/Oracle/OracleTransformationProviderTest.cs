using System;
using System.Configuration;
using System.Data;
using Migrator.Framework;
using Migrator.Providers.Oracle;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProviderTest : TransformationProviderConstraintBase
{
    [SetUp]
    public void SetUp()
    {
        var constr = ConfigurationManager.AppSettings["OracleConnectionString"];
        if (constr == null)
        {
            throw new ArgumentNullException("OracleConnectionString", "No config file");
        }

        Provider = new OracleTransformationProvider(new OracleDialect(), constr, null, "default", null);
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