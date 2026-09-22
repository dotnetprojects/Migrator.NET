using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class ColumnPropertyMapperTest
{
    [Test]
    public void OracleCreatesNotNullSql()
    {
        var mapper = new ColumnPropertiesMapper(new OracleDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo",DbType.String){IsNullable = false});
        Assert.That("foo varchar(30) NOT NULL", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void OracleCreatesSql()
    {
        var mapper = new ColumnPropertiesMapper(new OracleDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0));
        Assert.That("foo varchar(30) NULL", Is.EqualTo(mapper.ColumnSql));
    }





    [Test]
    public void SqlServerCreatesNotNullSql()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo",DbType.String){IsNullable = false});
        Assert.That("[foo] varchar(30) NOT NULL", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSqWithBooleanDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "bit");
        mapper.MapColumnProperties(new Column("foo", DbType.Boolean, 0, false));
        Assert.That("[foo] bit NULL DEFAULT 0", Is.EqualTo(mapper.ColumnSql));

        mapper.MapColumnProperties(new Column("bar", DbType.Boolean, 0, true));
        Assert.That("[bar] bit NULL DEFAULT 1", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSqWithDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0, "'NEW'"));
        Assert.That("[foo] varchar(30) NULL DEFAULT '''NEW'''", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSqWithNullDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0, "NULL"));
        Assert.That("[foo] varchar(30) NULL DEFAULT 'NULL'", Is.EqualTo(mapper.ColumnSql));
    }

    [Test]
    public void SqlServerCreatesSql()
    {
        var mapper = new ColumnPropertiesMapper(new SqlServerDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo", DbType.String, 0));
        Assert.That("[foo] varchar(30) NULL", Is.EqualTo(mapper.ColumnSql));
    }


    [Test]
    public void SQLiteIndexSqlWithEmptyStringDefault()
    {
        var mapper = new ColumnPropertiesMapper(new SQLiteDialect(), "varchar(30)");
        mapper.MapColumnProperties(new Column("foo",DbType.String,1,string.Empty){IsNullable = false});
        Assert.That("foo varchar(30) NOT NULL DEFAULT ''", Is.EqualTo(mapper.ColumnSql));
    }
}