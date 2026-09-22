using System;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using Oracle.ManagedDataAccess.Client;
using Migrator.Tests.Providers.Live;
using Npgsql;
using NUnit.Framework;

namespace Migrator.Tests.Dialects;

[TestFixture(ProviderTypes.SQLite)]
[TestFixture(ProviderTypes.SqlServer)]
[TestFixture(ProviderTypes.PostgreSQL)]
[TestFixture(ProviderTypes.Oracle)]
[TestFixture(ProviderTypes.Mysql)]
[TestFixture(ProviderTypes.MariaDB)]
[TestFixture(ProviderTypes.Firebird)]
[TestFixture(ProviderTypes.IBM_DB2)]
[TestFixture(ProviderTypes.IBM_Informix)]
[TestFixture(ProviderTypes.Sybase)]
[TestFixture(ProviderTypes.Hana)]
public class DataTypeBoundaryTests(ProviderTypes provider)
{
    [Test]
    public void EveryTypeHasAnExplicitMappingOrRejection([Values] MigratorDbType type)
    {
        var dialect = ProviderFactory.DialectForProvider(provider);
        var column = new Column("payload", type);
        if (type is MigratorDbType.String or MigratorDbType.AnsiString or MigratorDbType.StringFixedLength or MigratorDbType.AnsiStringFixedLength)
            column.Size = 32;
        if (!DataTypeContract.Supports(provider, type))
        {
            Assert.Throws<ArgumentException>(() => dialect.GetAndMapColumnProperties(column));
            return;
        }
        var sql = dialect.GetAndMapColumnProperties(column).ColumnSql;
        Assert.That(sql, Does.Contain("payload").IgnoreCase.And.Not.Contain("$l").And.Not.Contain("{precision}"));
    }

    [TestCase(DbType.String)]
    [TestCase(DbType.AnsiString)]
    public void UnlimitedTextNeverFallsBackToDefaultLength(DbType type)
    {
        var sql = ProviderFactory.DialectForProvider(provider).GetTypeName(type, int.MaxValue).ToUpperInvariant();
        Assert.That(sql.Contains("TEXT") || sql.Contains("CLOB") || sql.Contains("MAX") || sql.Contains("BLOB"), Is.True, sql);
        Assert.That(sql, Does.Not.Contain("255").And.Not.Contain("2147483647"));
    }

    [Test]
    public void DecimalPrecisionAndScaleAreRendered()
    {
        if (provider == ProviderTypes.SQLite) return; // SQLite uses numeric affinity, not a precision constraint.
        var sql = ProviderFactory.DialectForProvider(provider)
            .GetAndMapColumnProperties(new Column("payload", DbType.Decimal) { Precision = 12, Scale = 4 }).ColumnSql;
        Assert.That(sql.Replace(" ", ""), Does.Contain("(12,4)"));
    }
}

public class DialectCapacityRegressionTests
{
    private sealed class OracleParameterProbe()
        : OracleTransformationProvider(new OracleDialect(), (IDbConnection)null, null, "test", null)
    {
        public void Bind(IDbDataParameter parameter, object value) => ConfigureParameterWithValue(parameter, 0, value);
    }

    [Test]
    public void OracleSingleParameterUsesNativeBinaryFloat()
    {
        using var provider = new OracleParameterProbe();
        using var parameter = new OracleParameter();
        provider.Bind(parameter, -12345.125f);
        Assert.That(parameter.OracleDbType, Is.EqualTo(OracleDbType.BinaryFloat));
        Assert.That(parameter.Value, Is.TypeOf<float>().And.EqualTo(-12345.125f));
    }

    private sealed class PostgreSqlParameterProbe()
        : PostgreSQLTransformationProvider(new PostgreSQLDialect(), (IDbConnection)null, "public", "test", "Npgsql")
    {
        public void Bind(IDbDataParameter parameter, object value) => ConfigureParameterWithValue(parameter, 0, value);
    }

    [Test]
    public void PostgreSqlDriverAcceptsUInt64WithoutLosingPrecision()
    {
        using var provider = new PostgreSqlParameterProbe();
        foreach (var value in new[] { 0UL, (ulong)long.MaxValue + 1, ulong.MaxValue })
        {
            var parameter = new NpgsqlParameter();
            provider.Bind(parameter, value);
            Assert.That(parameter.NpgsqlDbType, Is.EqualTo(NpgsqlTypes.NpgsqlDbType.Numeric));
            Assert.That(parameter.Value, Is.TypeOf<decimal>().And.EqualTo((decimal)value));
        }
    }

    [TestCase(DateTimeKind.Unspecified, NpgsqlTypes.NpgsqlDbType.Timestamp)]
    [TestCase(DateTimeKind.Local, NpgsqlTypes.NpgsqlDbType.Timestamp)]
    [TestCase(DateTimeKind.Utc, NpgsqlTypes.NpgsqlDbType.TimestampTz)]
    public void PostgreSqlTimestampBindingPreservesDateTimeKind(DateTimeKind kind, NpgsqlTypes.NpgsqlDbType expected)
    {
        using var provider = new PostgreSqlParameterProbe();
        var value = new DateTime(2024, 2, 29, 23, 59, 59, kind);
        var parameter = new NpgsqlParameter();
        provider.Bind(parameter, value);
        Assert.That(parameter.NpgsqlDbType, Is.EqualTo(expected));
        Assert.That(parameter.Value, Is.EqualTo(value));
        Assert.That(((DateTime)parameter.Value).Kind, Is.EqualTo(kind));
    }

    [Test]
    public void OracleSingleUsesIeeeBinaryStorage()
        => Assert.That(ProviderFactory.DialectForProvider(ProviderTypes.Oracle).GetTypeName(DbType.Single), Is.EqualTo("BINARY_FLOAT"));

    [TestCase(ProviderTypes.Mysql, DbType.AnsiString, 256, "VARCHAR(256)")]
    [TestCase(ProviderTypes.MariaDB, DbType.AnsiString, 256, "VARCHAR(256)")]
    [TestCase(ProviderTypes.Firebird, DbType.Binary, 32, "VARCHAR(32) CHARACTER SET OCTETS")]
    [TestCase(ProviderTypes.Firebird, DbType.StringFixedLength, 32, "CHAR(32) CHARACTER SET UTF8")]
    [TestCase(ProviderTypes.Firebird, DbType.AnsiString, 32, "VARCHAR(32)")]
    [TestCase(ProviderTypes.SqlServer, DbType.Binary, 8000, "VARBINARY(8000)")]
    [TestCase(ProviderTypes.SqlServer, DbType.Binary, 8001, "VARBINARY(max)")]
    [TestCase(ProviderTypes.SqlServer, DbType.String, 4000, "NVARCHAR(4000)")]
    [TestCase(ProviderTypes.SqlServer, DbType.String, 4001, "NVARCHAR(max)")]
    [TestCase(ProviderTypes.Hana, DbType.String, 5000, "NVARCHAR(5000)")]
    [TestCase(ProviderTypes.Hana, DbType.String, 5001, "NCLOB")]
    [TestCase(ProviderTypes.IBM_Informix, DbType.String, 32739, "LVARCHAR(32739)")]
    [TestCase(ProviderTypes.IBM_Informix, DbType.String, 32740, "TEXT")]
    [TestCase(ProviderTypes.IBM_DB2, DbType.String, 32672, "VARCHAR(32672)")]
    [TestCase(ProviderTypes.IBM_DB2, DbType.String, 32673, "CLOB")]
    public void CapacityTransitionsHaveExpectedStorage(ProviderTypes provider, DbType type, int size, string expected)
        => Assert.That(ProviderFactory.DialectForProvider(provider).GetTypeName(type, size), Is.EqualTo(expected).IgnoreCase);

    [TestCase(ProviderTypes.Mysql, "DECIMAL(19,4)")]
    [TestCase(ProviderTypes.MariaDB, "DECIMAL(19,4)")]
    [TestCase(ProviderTypes.Oracle, "NUMBER(19,4)")]
    public void CurrencyHasFourFractionalDigits(ProviderTypes provider, string expected)
        => Assert.That(ProviderFactory.DialectForProvider(provider).GetTypeName(DbType.Currency), Is.EqualTo(expected));
}
