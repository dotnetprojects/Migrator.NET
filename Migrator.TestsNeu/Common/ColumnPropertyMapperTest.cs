using System.Data;
using Migrator.Framework;
using Migrator.Providers;
using Migrator.Providers.PostgreSQL;
using Migrator.Providers.SQLite;
using Migrator.Providers.SqlServer;
using NUnit.Framework;

namespace Migrator.TestsNeu
{
	[TestFixture]
	public class ColumnPropertyMapperTest
	{
		[Test]
		public void PostgresIndexSqlIsNoNullWhenIndexed()
		{
			var mapper = new ColumnPropertiesMapper(new PostgreSQLDialect(), "char(1)");
			mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, ColumnProperty.Indexed));
			Assert.IsNotNull(mapper.IndexSql);
		}

		[Test]
		public void PostgresIndexSqlIsNullWhenIndexedFalse()
		{
			var mapper = new ColumnPropertiesMapper(new PostgreSQLDialect(), "char(1)");
			mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, 0));
			Assert.IsNull(mapper.IndexSql);
		}

		[Test]
		public void SqlServerCreatesNotNullSql()
		{
			var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
			mapper.MapColumnProperties(new Column("foo", DbType.String, ColumnProperty.NotNull));
			Assert.AreEqual("[foo] varchar(30) NOT NULL", mapper.ColumnSql);
		}

		[Test]
		public void SqlServerCreatesSqWithBooleanDefault()
		{
			var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "bit");
			mapper.MapColumnProperties(new Column("foo", DbType.Boolean, 0, false));
			Assert.AreEqual("[foo] bit DEFAULT 0", mapper.ColumnSql);

			mapper.MapColumnProperties(new Column("bar", DbType.Boolean, 0, true));
			Assert.AreEqual("[bar] bit DEFAULT 1", mapper.ColumnSql);
		}

		[Test]
		public void SqlServerCreatesSqWithDefault()
		{
			var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
			mapper.MapColumnProperties(new Column("foo", DbType.String, 0, "'NEW'"));
			Assert.AreEqual("[foo] varchar(30) DEFAULT '''NEW'''", mapper.ColumnSql);
		}

		[Test]
		public void SqlServerCreatesSqWithNullDefault()
		{
			var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
			mapper.MapColumnProperties(new Column("foo", DbType.String, 0, "NULL"));
			Assert.AreEqual("[foo] varchar(30) DEFAULT 'NULL'", mapper.ColumnSql);
		}

		[Test]
		public void SqlServerCreatesSql()
		{
			var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
			mapper.MapColumnProperties(new Column("foo", DbType.String, 0));
			Assert.AreEqual("[foo] varchar(30)", mapper.ColumnSql);
		}

		[Test]
		public void SqlServerIndexSqlIsNoNullWhenIndexed()
		{
			var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "char(1)");
			mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, ColumnProperty.Indexed));
			Assert.IsNull(mapper.IndexSql);
		}

        [Test]
        public void SQLiteIndexSqlWithEmptyStringDefault()
        {
            var mapper = new ColumnPropertiesMapper(new SQLiteDialect(), "varchar(30)");
            mapper.MapColumnProperties(new Column("foo", DbType.String, 1, ColumnProperty.NotNull, string.Empty));
            Assert.AreEqual("foo varchar(30) NOT NULL DEFAULT ''", mapper.ColumnSql);
        }
	}
}
