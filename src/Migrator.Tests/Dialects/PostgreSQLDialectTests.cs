using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using NUnit.Framework;

namespace Migrator.Tests.Dialects;

[TestFixture]
[Category("Postgre")]
public class PostgreDialectTests
{
    private PostgreSQLDialect _postgreSQLDialect;

    [SetUp]
    public void SetUp()
    {
        // Since Dialect is abstract we use PostgreSQLDialect
        _postgreSQLDialect = new PostgreSQLDialect();
    }

    [TestCase(FilterType.EqualTo, "=")]
    [TestCase(FilterType.GreaterThanOrEqualTo, ">=")]
    [TestCase(FilterType.SmallerThanOrEqualTo, "<=")]
    [TestCase(FilterType.SmallerThan, "<")]
    [TestCase(FilterType.GreaterThan, ">")]
    [TestCase(FilterType.NotEqualTo, "<>")]
    public void GetComparisonStringByFilterType_Success(FilterType filterType, string expectedString)
    {
        var result = _postgreSQLDialect.GetComparisonStringByFilterType(filterType);

        Assert.That(result, Is.EqualTo(expectedString));
    }

    [TestCase("=", FilterType.EqualTo)]
    [TestCase(">=", FilterType.GreaterThanOrEqualTo)]
    [TestCase("<=", FilterType.SmallerThanOrEqualTo)]
    [TestCase("<", FilterType.SmallerThan)]
    [TestCase(">", FilterType.GreaterThan)]
    [TestCase("<>", FilterType.NotEqualTo)]
    public void GetFilterTypeByComparisonString_Success(string comparisonString, FilterType expectedFilterType)
    {
        var result = _postgreSQLDialect.GetFilterTypeByComparisonString(comparisonString);

        Assert.That(result, Is.EqualTo(expectedFilterType));
    }
}