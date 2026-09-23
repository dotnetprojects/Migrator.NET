using System;
using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;

namespace Migrator.Tests;

public class SqlPreviewBoundaryTests
{
    private static IEnumerable<TestCaseData> Literals()
    {
        yield return new TestCaseData(null, "NULL");
        yield return new TestCaseData(DBNull.Value, "NULL");
        yield return new TestCaseData("O'Brien", "'O''Brien'");
        yield return new TestCaseData("", "''");
        yield return new TestCaseData((byte)255, "255");
        yield return new TestCaseData((sbyte)-128, "-128");
        yield return new TestCaseData(short.MinValue, "-32768");
        yield return new TestCaseData(ushort.MaxValue, "65535");
        yield return new TestCaseData(int.MinValue, "-2147483648");
        yield return new TestCaseData(uint.MaxValue, "4294967295");
        yield return new TestCaseData(long.MinValue, "-9223372036854775808");
        yield return new TestCaseData(ulong.MaxValue, "18446744073709551615");
        yield return new TestCaseData(12.5m, "12.5");
        yield return new TestCaseData(12.5f, "12.5");
        yield return new TestCaseData(12.5d, "12.5");
        yield return new TestCaseData(new DateTime(2024, 2, 29, 12, 34, 56).AddTicks(1234567), "'2024-02-29 12:34:56.1234567'");
        yield return new TestCaseData(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"), "'00112233-4455-6677-8899-aabbccddeeff'");
    }

    [TestCaseSource(nameof(Literals)), SetCulture("de-DE")]
    public void LiteralsPreserveValuesWithoutUsingCurrentCulture(object value, string expected)
        => Assert.That(new SqlGenerationContext(ProviderTypes.SQLite).Literal(value), Is.EqualTo(expected));

    [TestCase(ProviderTypes.SQLite, "1", "0")]
    [TestCase(ProviderTypes.SqlServer, "1", "0")]
    [TestCase(ProviderTypes.PostgreSQL, "TRUE", "FALSE")]
    [TestCase(ProviderTypes.PostgreSQL82, "TRUE", "FALSE")]
    public void BooleanLiteralsUseTheTargetDialect(ProviderTypes provider, string yes, string no)
    {
        var context = new SqlGenerationContext(provider);
        Assert.That(context.Literal(true), Is.EqualTo(yes));
        Assert.That(context.Literal(false), Is.EqualTo(no));
    }

    [Test]
    public void UnsupportedValuesCannotMasqueradeAsSqlLiterals()
    {
        var context = new SqlGenerationContext(ProviderTypes.SQLite);
        foreach (var value in new object[] { DayOfWeek.Monday, RawSql.Insert("DROP TABLE Items"), new Version(1, 2), new byte[] { 1 }, TimeSpan.FromDays(1) })
            Assert.Throws<NotSupportedException>(() => context.Literal(value));
    }

    [TestCase(ProviderTypes.SQLite, "\"two words\"", "\"a.b\"")]
    [TestCase(ProviderTypes.PostgreSQL, "\"two words\"", "\"a.b\"")]
    [TestCase(ProviderTypes.SqlServer, "[two words]", "[a.b]")]
    public void IdentifierAtomsAreQuotedWithoutSplittingDots(ProviderTypes type, string spaced, string dotted)
    {
        var context = new SqlGenerationContext(type);
        Assert.That(context.Quote("two words"), Is.EqualTo(spaced));
        Assert.That(context.Quote("a.b"), Is.EqualTo(dotted));
    }

    [TestCase(ProviderTypes.SQLite, "\"sales.region\".\"Order Lines\"", "\"sales.region\".\"Order Lines\"")]
    [TestCase(ProviderTypes.PostgreSQL, "\"sales.region\".\"Order Lines\"", "\"sales.region\".\"Order Lines\"")]
    [TestCase(ProviderTypes.SqlServer, "[sales.region].[Order]]Lines]", "[sales.region].[Order]]Lines]")]
    public void QualifiedTableNamesPreserveQuotedComponents(ProviderTypes type, string name, string expected)
        => Assert.That(new SqlGenerationContext(type).Table(name), Is.EqualTo(expected));

    [Test, Category("SQLite")]
    public void PreviewWithSpacesAndDottedColumnNamesExecutesLikeTheMigration()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("Order Details").WithColumn("Id").AsInt32().WithColumn("Line.Item").AsString();
        builder.Insert.IntoTable("Order Details").Row(new[] { "Id", "Line.Item" }, new object[] { 1, "O'Brien" });
        builder.Create.Index("Index with spaces").OnTable("Order Details").WithColumns("Line.Item");
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        foreach (var sql in builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite))) provider.ExecuteNonQuery(sql);
        Assert.That(provider.ExecuteScalar("SELECT \"Line.Item\" FROM \"Order Details\" WHERE Id=1"), Is.EqualTo("O'Brien"));
        Assert.That(provider.IndexExists("Order Details", "Index with spaces"), Is.True);
    }
}
