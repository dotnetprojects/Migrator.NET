using System.Data;
using Migrator.Framework;
using Migrator.Providers;
using Migrator.Providers.Oracle;
using Migrator.Providers.PostgreSQL;
using Migrator.Providers.SQLite;
using Migrator.Providers.SqlServer;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class ColumnPropertyMapperTest
{
    [Test]
    public void OracleCreatesNotNullSql()
    {
        var mapper = new ColumnPropertiesMapper(new OracleDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, ColumnProperty.NotNull));
        Assert.That("foo varchar(30) NOT NULL", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void OracleCreatesSql()
    {
        var mapper = new ColumnPropertiesMapper(new OracleDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0));
        Assert.That("foo varchar(30)", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void OracleIndexSqlIsNoNullWhenIndexed()
    {
        var mapper = new ColumnPropertiesMapper(new OracleDialect(), "char(1)");
        mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, ColumnProperty.Indexed));
        Assert.That(mapper.IndexSql, Is.Not.Null);
    }

    [Test]
    public void OracleIndexSqlIsNullWhenIndexedFalse()
    {
        var mapper = new ColumnPropertiesMapper(new OracleDialect(), "char(1)");
        mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, 0));
        Assert.That(mapper.IndexSql, Is.Null);
    }

    [Test]
    public void PostgresIndexSqlIsNoNullWhenIndexed()
    {
        var mapper = new ColumnPropertiesMapper(new PostgreSQLDialect(), "char(1)");
        mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, ColumnProperty.Indexed));
        Assert.That(mapper.IndexSql, Is.Not.Null);
    }

    [Test]
    public void PostgresIndexSqlIsNullWhenIndexedFalse()
    {
        var mapper = new ColumnPropertiesMapper(new PostgreSQLDialect(), "char(1)");
        mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, 0));
        Assert.That(mapper.IndexSql, Is.Null);
    }

    [Test]
    public void SqlServerCreatesNotNullSql()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, ColumnProperty.NotNull));
        Assert.That("[foo] varchar(30) NOT NULL", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSqWithBooleanDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "bit");
        mapper.MapColumnProperties(new Column("foo", DbType.Boolean, 0, false));
        Assert.That("[foo] bit DEFAULT 0", Is.EqualTo(mapper.ColumnSql));

        mapper.MapColumnProperties(new Column("bar", DbType.Boolean, 0, true));
        Assert.That("[bar] bit DEFAULT 1", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSqWithDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0, "'NEW'"));
        Assert.That("[foo] varchar(30) DEFAULT '''NEW'''", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSqWithNullDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0, "NULL"));
        Assert.That("[foo] varchar(30) DEFAULT 'NULL'", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSql()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0));
        Assert.That("[foo] varchar(30)", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerIndexSqlIsNoNullWhenIndexed()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "char(1)");
        mapper.MapColumnProperties(new Column("foo", DbType.StringFixedLength, 1, ColumnProperty.Indexed));
        Assert.That(mapper.IndexSql, Is.Null);
    }

    [Test]
    public void SQLiteIndexSqlWithEmptyStringDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SQLiteDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 1, ColumnProperty.NotNull, string.Empty));
        Assert.That("foo varchar(30) NOT NULL DEFAULT ''", Is.EqualTo(mapper.ColumnSql));
    }
}