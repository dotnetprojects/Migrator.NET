using System;
using System.Configuration;
using System.Data;
using Migrator.Framework;
using Migrator.Providers.Oracle;
using NUnit.Framework;

namespace Migrator.Tests.Providers
{
    [TestFixture]
    [Category("Oracle")]
    public class OracleTransformationProviderTest : TransformationProviderConstraintBase
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            string constr = ConfigurationManager.AppSettings["OracleConnectionString"];
            if (constr == null)
                throw new ArgumentNullException("OracleConnectionString", "No config file");
            _provider = new OracleTransformationProvider(new OracleDialect(), constr, null, "default", null);
            _provider.BeginTransaction();

            AddDefaultTable();
        }

        #endregion

        [Test]
        public void ChangeColumn_FromNotNullToNotNull()
        {
            _provider.ExecuteNonQuery("DELETE FROM TestTwo");
            _provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
            _provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 3, "Not an Int val." });
            _provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.NotNull));
            _provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.NotNull));
        }
    }
}