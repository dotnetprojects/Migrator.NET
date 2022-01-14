using System;
using System.Configuration;
using Migrator.Providers;
using Migrator.Providers.PostgreSQL;
using Migrator.TestsNeu;
using Npgsql;
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
			string constr = Helper.GetConnectionStringPostgreSQL.ConnectionString;
			if (constr == null)
				throw new ArgumentNullException("SqlServerConnectionString", "No config file");

			DbProviderFactories.RegisterFactory("Npgsql", () => NpgsqlFactory.Instance);
			_provider = ProviderFactory.Create(ProviderTypes.PostgreSQL, constr, "default");
			_provider.BeginTransaction();
			AddDefaultTable();
		}

		#endregion

		[Test]
		public void QuoteCreatesProperFormat()
		{
			Dialect dialect = new PostgreSQLDialect();
			var res = dialect.Quote("foo");
			Assert.AreEqual("[foo]", dialect.Quote("foo"));
		}
	}
}
