using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using NUnit.Framework;

namespace Migrator.Tests;

public class IdentifierAndTimeRegressionTests
{
    [TestCase(-51)]
    [TestCase(51)]
    public void SQLiteIntervalsKeepTheirSignAndDaysSeparateFromTimeOfDay(int hours)
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        var duration = TimeSpan.FromHours(hours).Add(TimeSpan.FromTicks(1234567));
        provider.AddTable("Durations", new Column("Id", DbType.Int32), new Column("Elapsed", MigratorDbType.Interval, duration));
        provider.Insert("Durations", ["Id"], [1]);
        provider.Insert("Durations", ["Id", "Elapsed"], [2, duration]);
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Elapsed FROM Durations WHERE Id=1")), Is.EqualTo(duration.Ticks));
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Elapsed FROM Durations WHERE Id=2")), Is.EqualTo(duration.Ticks));
        Assert.Throws<ArgumentException>(() => provider.AddTable("WrongTime", new Column("Moment", DbType.Time, duration)));
        Assert.That(provider.TableExists("WrongTime"), Is.False);
    }

    [TestCase("sales.Orders", "[sales].[Orders]")]
    [TestCase("[sales.region].[Order]]Lines]", "[sales.region].[Order]]Lines]")]
    [TestCase("[sales].[O'Brien]", "[sales].[O'Brien]")]
    public void QualifiedSqlServerNamesEscapeEachComponentExactlyOnce(string input, string expected)
    {
        var dialect = new SqlServerDialect();
        Assert.That(dialect.Quote(input), Is.EqualTo(expected));
        Assert.That(dialect.QuoteColumnNameIfRequired("value.part]"), Is.EqualTo("[value.part]]]"));
    }

    [Test]
    public void OracleAndPostgreSqlQualifyReservedComponentsIndependently()
    {
        foreach (var dialect in new Dialect[] { new OracleDialect(), new PostgreSQLDialect() })
        {
            Assert.That(dialect.QuoteTableNameIfRequired("sales.select"), Is.EqualTo("sales.\"select\""));
            Assert.That(dialect.QuoteTableNameIfRequired("\"sales.region\".\"O'Brien\""), Is.EqualTo("\"sales.region\".\"O'Brien\""));
        }
    }

    [Test]
    public void TimeDefaultsAreQuotedAndPreserveSubMillisecondPrecision()
    {
        var time = new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234560));
        foreach (var dialect in new Dialect[] { new SQLiteDialect(), new MysqlDialect(), new PostgreSQLDialect(), new SqlServerDialect() })
            Assert.That(dialect.Default(time), Is.EqualTo("DEFAULT '12:34:56.123456'"));
    }

    [Test, Category("SQLite")]
    public void TimeDefaultsAndValuesSurviveSQLiteReconstruction()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        var time = new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234560));
        provider.AddTable("Times", new Column("Id", DbType.Int32), new Column("Value", DbType.Time) { DefaultValue = time });
        var column = provider.GetColumns("Times").Single(c => c.Name == "Value");
        Assert.That(column.Type, Is.EqualTo(DbType.Time));
        Assert.That(column.DefaultValue, Is.EqualTo(time));
        provider.ChangeColumn("Times", new Column("Id", DbType.Int64));
        provider.Insert("Times", ["Id"], [1]);
        Assert.That(TimeOnly.Parse(Convert.ToString(provider.ExecuteScalar("SELECT CAST(Value AS TEXT) FROM Times"))), Is.EqualTo(time));
        provider.Insert("Times", ["Id", "Value"], [2, time]);
        Assert.That(TimeOnly.Parse(Convert.ToString(provider.ExecuteScalar("SELECT CAST(Value AS TEXT) FROM Times WHERE Id=2"))), Is.EqualTo(time));
    }
}
