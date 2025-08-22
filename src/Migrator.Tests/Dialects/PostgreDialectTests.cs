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
        _postgreSQLDialect = new PostgreSQLDialect();
    }

    [TestCase(FilterType.EqualTo, "=")]
    [TestCase(FilterType.GreaterThanOrEqualTo, ">=")]
    [TestCase(FilterType.SmallerThanOrEqualTo, "<=")]
    [TestCase(FilterType.SmallerThan, "<")]
    [TestCase(FilterType.GreaterThan, ">")]
    public void GetComparisonStringFilterIndex(FilterType filterType, string expectedString)
    {
        var result = _postgreSQLDialect.GetComparisonStringFilterIndex(filterType);

        Assert.That(result, Is.EqualTo(expectedString));
    }
}