using System;
using System.Configuration;
using Migrator.Providers.PostgreSQL;
using NUnit.Framework;

namespace Migrator.Tests.Providers
{
    [TestFixture]
    [Category("Postgre")]
    public class PostgreSQLTransformationProviderTest : TransformationProviderConstraintBase
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            string constr = ConfigurationManager.AppSettings["NpgsqlConnectionString"];
            if (constr == null)
                throw new ArgumentNullException("ConnectionString", "No config file");

            _provider = new PostgreSQLTransformationProvider(new PostgreSQLDialect(), constr, null, "default", null);
            _provider.BeginTransaction();

            AddDefaultTable();
        }

        #endregion
    }
}