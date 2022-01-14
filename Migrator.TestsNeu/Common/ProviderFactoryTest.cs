using System;
using System.Linq;
using Migrator.Framework;
using Migrator.Providers;
using Npgsql;
using NUnit.Framework;

namespace Migrator.TestsNeu
{
	[TestFixture]
	public class ProviderFactoryTest
	{
		[Test]
		public void CanGetDialectsForProvider()
		{
			foreach (ProviderTypes provider in Enum.GetValues(typeof(ProviderTypes)).Cast<ProviderTypes>().Where(x=>x!=ProviderTypes.none))
            {
                Assert.IsNotNull(ProviderFactory.DialectForProvider(provider));
            }
			Assert.IsNull(ProviderFactory.DialectForProvider(ProviderTypes.none));			
		}

		[Test]
		[Category("Postgre")]
		public void CanLoad_PostgreSQLProvider()
		{
			DbProviderFactories.RegisterFactory("Npgsql", () => NpgsqlFactory.Instance);
			ITransformationProvider provider = ProviderFactory.Create(ProviderTypes.PostgreSQL,Helper.GetConnectionStringPostgreSQL.ConnectionString, null);
			Assert.IsNotNull(provider);
		}

		[Test]
		[Category("SQLite")]
		public void CanLoad_SQLiteProvider()
		{
            ITransformationProvider provider = ProviderFactory.Create(ProviderTypes.SQLite,Helper.GetConnectionStringSQLite.ConnectionString, null);
			Assert.IsNotNull(provider);
		}

		[Test]
		[Category("SqlServer")]
		public void CanLoad_SqlServerProvider()
		{
            ITransformationProvider provider = ProviderFactory.Create(ProviderTypes.SqlServer,Helper.GetConnectionStringSQLServer.ConnectionString, null);
			Assert.IsNotNull(provider);
		}
	}
}
