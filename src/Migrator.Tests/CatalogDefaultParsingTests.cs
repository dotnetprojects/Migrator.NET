using System;
using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using NUnit.Framework;

namespace Migrator.Tests;

public class CatalogDefaultParsingTests
{
    private static IEnumerable<TestCaseData> Literals()
    {
        foreach (var dialect in new[] { "SqlServer", "Oracle", "PostgreSQL" })
        {
            yield return new TestCaseData(dialect, DbType.String, "'O''Brien (x)'", "O'Brien (x)");
            yield return new TestCaseData(dialect, DbType.Int32, "42", 42L);
            yield return new TestCaseData(dialect, DbType.Int16, "'-42'", -42L);
            yield return new TestCaseData(dialect, DbType.Decimal, "'12.50'", 12.50m);
            yield return new TestCaseData(dialect, DbType.Double, "1.25", 1.25d);
            yield return new TestCaseData(dialect, DbType.Single, "1.25", dialect == "Oracle" ? (object)1.25f : 1.25d);
            yield return new TestCaseData(dialect, DbType.Guid, "'00112233-4455-6677-8899-aabbccddeeff'", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        }
        yield return new TestCaseData("SqlServer", DbType.Int64, "(('42'))", 42L);
        yield return new TestCaseData("SqlServer", DbType.Byte, "((255))", (byte)255);
        yield return new TestCaseData("SqlServer", DbType.UInt64, "((18446744073709551615))", ulong.MaxValue);
        yield return new TestCaseData("SqlServer", DbType.Boolean, "((1))", true);
        yield return new TestCaseData("SqlServer", DbType.Boolean, "('FALSE')", false);
        yield return new TestCaseData("SqlServer", DbType.Binary, "(0x00A1ff)", new byte[] { 0, 161, 255 });
        yield return new TestCaseData("SqlServer", DbType.DateTime, "(CONVERT([datetime],'2000-01-02 03:04:05.123',(121)))", new DateTime(2000, 1, 2, 3, 4, 5, 123, DateTimeKind.Utc));
        yield return new TestCaseData("SqlServer", DbType.Time, "'12:34:56.1234567'", new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234567)));
        yield return new TestCaseData("Oracle", DbType.Binary, "HEXTORAW('00A1ff')", new byte[] { 0, 161, 255 });
        yield return new TestCaseData("Oracle", DbType.Guid, "HEXTORAW('00112233445566778899AABBCCDDEEFF')", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        yield return new TestCaseData("Oracle", DbType.DateTime, "TO_TIMESTAMP('2000-01-02 03:04:05.123','YYYY-MM-DD HH24:MI:SS.FF')", new DateTime(2000, 1, 2, 3, 4, 5, 123, DateTimeKind.Utc));
        yield return new TestCaseData("Oracle", DbType.Boolean, "TRUE", true);
        yield return new TestCaseData("PostgreSQL", DbType.Int32, "'42'::integer", 42L);
        yield return new TestCaseData("PostgreSQL", DbType.String, "'O''Brien'::text", "O'Brien");
        yield return new TestCaseData("PostgreSQL", DbType.Binary, "'\\x00a1ff'::bytea", new byte[] { 0, 161, 255 });
        yield return new TestCaseData("PostgreSQL", DbType.Time, "'12:34:56.1234567'::time without time zone", new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234567)));
        yield return new TestCaseData("PostgreSQL", DbType.DateTime, "'2000-01-02 03:04:05'::timestamp without time zone", new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        yield return new TestCaseData("PostgreSQL", DbType.DateTimeOffset, "'2000-01-02 03:04:05+02'::timestamp with time zone", new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)));
        yield return new TestCaseData("PostgreSQL", DbType.Boolean, "false", false);
    }

    [TestCaseSource(nameof(Literals))]
    [SetCulture("de-DE")]
    public void CatalogLiteralsRetainTheirValuesAndClrTypes(string dialect, DbType type, string sql, object expected)
    {
        var column = new Column("Value", type);
        Apply(dialect, column, sql);
        Assert.That(column.DefaultValue, Is.EqualTo(expected));
        Assert.That(column.DefaultValue.GetType(), Is.EqualTo(expected.GetType()));
    }

    [TestCase("SqlServer", "(newid())")]
    [TestCase("Oracle", "SYS_GUID()")]
    [TestCase("PostgreSQL", "gen_random_uuid()")]
    public void ExpressionsRemainSql(string dialect, string sql)
    {
        var column = new Column("Value", DbType.Guid);
        Apply(dialect, column, sql);
        Assert.That(column.DefaultValue, Is.TypeOf<RawSql>());
        Assert.That(column.DefaultValue.ToString(), Is.EqualTo(sql));
    }

    [TestCase("SqlServer", "(0xABC)")]
    [TestCase("Oracle", "HEXTORAW('ABC')")]
    [TestCase("PostgreSQL", "'\\xabc'::bytea")]
    public void OddLengthBinaryDefaultsAreRejectedInsteadOfTruncated(string dialect, string sql)
    {
        Assert.Throws<FormatException>(() => Apply(dialect, new Column("Value", DbType.Binary), sql));
    }

    [TestCase("schema.sequence.nextval")]
    [TestCase("\"ISEQ$$123\".nextval")]
    [TestCase("NULL")]
    public void OracleIdentityExpressionsAndNullDoNotBecomeDefaults(string sql)
    {
        var column = new Column("Value", DbType.Int64);
        OracleColumnDefault.Apply(column, sql);
        Assert.That(column.DefaultValue, Is.Null);
    }

    private static void Apply(string dialect, Column column, string sql)
    {
        switch (dialect)
        {
            case "SqlServer": SqlServerColumnDefault.Apply(column, sql); break;
            case "Oracle": OracleColumnDefault.Apply(column, sql); break;
            case "PostgreSQL": PostgreSqlColumnDefault.Apply(column, sql); break;
            default: throw new ArgumentOutOfRangeException(nameof(dialect));
        }
    }

    [TestCase("SqlServer", "((NULL))")]
    [TestCase("PostgreSQL", "NULL")]
    public void NumericNullDefaultsRemainNull(string dialect, string sql)
    {
        var column = new Column("Value", DbType.Int32);
        Apply(dialect, column, sql);
        Assert.That(column.DefaultValue, Is.Null);
    }

    [TestCase("-51:02:03.1234567", -1837231234567L)]
    [TestCase("51:02:03.1234567", 1837231234567L)]
    public void PostgreSqlIntervalsRetainSignDaysAndFractionalTicks(string literal, long ticks)
    {
        var column = new Column("Value", MigratorDbType.Interval);
        PostgreSqlColumnDefault.Apply(column, "'" + literal + "'::interval");
        Assert.That(column.DefaultValue, Is.EqualTo(TimeSpan.FromTicks(ticks)));
    }
}
