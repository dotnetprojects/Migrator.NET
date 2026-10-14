using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
using DtoColumn = System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;

namespace Migrator.Tests;

[Category("SQLite")]
public class DtoFilterTests
{
    public enum Status { New = 1, Archived = 2 }
    public class Row
    {
        [DtoColumn("select")] public int Id { get; set; }
        public bool Active { get; set; }
        public int? Optional { get; set; }
        public Status State { get; set; }
        public string Name { get; set; }
        [NotMapped] public string Display { get; set; }
    }
    public class BaseRow { [DtoColumn("select")] public int Id { get; set; } }
    public class DerivedRow : BaseRow { public string Name { get; set; } }
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        provider.ExecuteNonQuery("CREATE TABLE \"filter rows\" (\"select\" INTEGER, Active INTEGER, Optional INTEGER, State INTEGER, Name TEXT)");
        var columns = new[] { "select", "Active", "Optional", "State", "Name" };
        provider.Insert("filter rows", columns, new object[] { 1, true, null, 1, "O'Brien" });
        provider.Insert("filter rows", columns, new object[] { 2, false, 7, 2, "other" });
        provider.Insert("filter rows", columns, new object[] { 3, true, 9, 1, "third" });
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [TestCase("equal", 2)]
    [TestCase("not-equal", 1, 3)]
    [TestCase("less", 1)]
    [TestCase("less-equal", 1, 2)]
    [TestCase("greater", 3)]
    [TestCase("greater-equal", 2, 3)]
    [TestCase("null", 1)]
    [TestCase("not-null", 2, 3)]
    [TestCase("nullable-equal", 2)]
    [TestCase("nullable-not-equal", 3)]
    [TestCase("equal-null", 1)]
    [TestCase("not-equal-null", 2, 3)]
    [TestCase("enum", 2)]
    [TestCase("boolean", 1, 3)]
    [TestCase("reference-null")]
    [TestCase("reference-not-null", 1, 2, 3)]
    public void FiltersMappedPropertiesWithAllSupportedOperations(string operation, params int[] expected)
    {
        var filter = operation switch
        {
            "equal" => Filter<Row>.Equal(x => x.Id, 2),
            "not-equal" => Filter<Row>.NotEqual(x => x.Id, 2),
            "less" => Filter<Row>.LessThan(x => x.Id, 2),
            "less-equal" => Filter<Row>.LessThanOrEqual(x => x.Id, 2),
            "greater" => Filter<Row>.GreaterThan(x => x.Id, 2),
            "greater-equal" => Filter<Row>.GreaterThanOrEqual(x => x.Id, 2),
            "null" => Filter<Row>.IsNull(x => x.Optional),
            "not-null" => Filter<Row>.IsNotNull(x => x.Optional),
            "nullable-equal" => Filter<Row>.Equal(x => x.Optional, 7),
            "nullable-not-equal" => Filter<Row>.NotEqual(x => x.Optional, 7),
            "equal-null" => Filter<Row>.Equal(x => x.Optional, null),
            "not-equal-null" => Filter<Row>.NotEqual(x => x.Optional, null),
            "enum" => Filter<Row>.Equal(x => x.State, Status.Archived),
            "reference-null" => Filter<Row>.IsNull(x => x.Name),
            "reference-not-null" => Filter<Row>.IsNotNull(x => x.Name),
            _ => Filter<Row>.Equal(x => x.Active, true)
        };
        ITransformationProvider database = provider;
        Assert.That(database.Select("filter rows", filter).Select(x => x.Id), Is.EquivalentTo(expected));
    }

    [Test]
    public void ComposedFiltersMatchLambdaPredicatesAndPreserveGrouping()
    {
        var filter = Filter<Row>.Equal(x => x.Active, false).Not()
            .And(Filter<Row>.IsNull(x => x.Optional).Or(Filter<Row>.GreaterThan(x => x.Id, 2)));
        var result = provider.Select("filter rows", filter);
        var lambda = provider.Select<Row>("filter rows", x => !(!x.Active) && (x.Optional == null || x.Id > 2));
        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(new[] { 1, 3 }));
        Assert.That(result.Select(x => x.Id), Is.EquivalentTo(lambda.Select(x => x.Id)));
    }

    [Test]
    public void FiltersInheritedMappedProperties()
    {
        var filter = Filter<DerivedRow>.GreaterThan(x => x.Id, 2);
        var row = provider.Select("filter rows", filter).Single();
        Assert.That(row.Id, Is.EqualTo(3));
        Assert.That(row.Name, Is.EqualTo("third"));
    }

    [Test]
    public void FiltersAreImmutableAndCaptureValuesAtConstruction()
    {
        var id = 1;
        var original = Filter<Row>.Equal(x => x.Id, id);
        id = 2;
        var combined = original.Or(Filter<Row>.Equal(x => x.Id, id));
        Assert.That(provider.Select("filter rows", original).Single().Id, Is.EqualTo(1));
        Assert.That(provider.Select("filter rows", combined).Select(x => x.Id), Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(provider.Select("filter rows", original).Single().Id, Is.EqualTo(1));
    }

    [Test]
    public void FilterValuesAreParameterizedAndCanBeReusedInATransaction()
    {
        var filter = Filter<Row>.Equal(x => x.Name, "'; DROP TABLE anything; --");
        Assert.That(provider.Select("filter rows", filter), Is.Empty);
        provider.BeginTransaction();
        provider.Update("filter rows", new[] { "Name" }, new object[] { "changed" }, new[] { "select" }, new object[] { 1 });
        var changed = Filter<Row>.Equal(x => x.Name, "changed");
        Assert.That(provider.Select("filter rows", changed).Single().Id, Is.EqualTo(1));
        provider.Rollback();
        Assert.That(provider.Select("filter rows", changed), Is.Empty);
        Assert.That(provider.Select<Row>("filter rows").Count, Is.EqualTo(3));
    }

    [TestCase("calculation")]
    [TestCase("nested")]
    [TestCase("method")]
    [TestCase("conversion")]
    [TestCase("constant")]
    public void RejectsSelectorsThatAreNotDirectProperties(string kind)
    {
        Expression<Func<Row, int>> column = kind switch
        {
            "calculation" => x => x.Id + 1,
            "nested" => x => x.Name.Length,
            "method" => x => x.Name.GetHashCode(),
            "conversion" => x => (int)x.State,
            _ => x => 7
        };
        Assert.Throws<ArgumentException>(() => Filter<Row>.Equal(column, 1));
    }

    [Test]
    public void RejectsUnmappedAndWidenedSelectorsAndUnsupportedOperators()
    {
        Assert.Throws<NotSupportedException>(() => Filter<Row>.Equal(x => x.Display, "unused"));
        Assert.Throws<ArgumentException>(() => Filter<Row>.Equal<object>(x => x.Id, "wrong type"));
        Assert.Throws<ArgumentException>(() => Filter<Row>.Equal<object>(x => x.Name, "name"));
        Assert.Throws<ArgumentException>(() => Filter<Row>.Equal(x => (long)x.Id, 1L));
        Assert.Throws<NotSupportedException>(() => Filter<Row>.GreaterThan(x => x.Active, false));
        Assert.Throws<NotSupportedException>(() => Filter<Row>.GreaterThan(x => x.State, Status.New));
        Assert.Throws<NotSupportedException>(() => Filter<Row>.GreaterThan(x => x.Name, "a"));
        Assert.Throws<NotSupportedException>(() => Filter<Row>.LessThan(x => x.Optional, null));
        Assert.Throws<ArgumentException>(() => Filter<Row>.IsNull(x => x.Id));
    }

    [Test]
    public void RejectsNullArgumentsRatherThanReadingAllRows()
    {
        var filter = Filter<Row>.Equal(x => x.Id, 1);
        Assert.Throws<ArgumentNullException>(() => Filter<Row>.Equal<int>(null, 1));
        Assert.Throws<ArgumentNullException>(() => filter.And(null));
        Assert.Throws<ArgumentNullException>(() => filter.Or(null));
        var database = Substitute.For<ITransformationProvider>();
        Assert.Throws<ArgumentNullException>(() => database.Select<Row>("filter rows", filter: null));
        database.DidNotReceive().CreateCommand();
    }
}
