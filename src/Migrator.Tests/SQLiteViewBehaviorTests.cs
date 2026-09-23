using System;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteViewBehaviorTests
{
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        provider.ExecuteNonQuery("""
            CREATE TABLE Customers (Id INTEGER, Name TEXT, RegionId INTEGER);
            CREATE TABLE Regions (Id INTEGER, Label TEXT);
            CREATE TABLE Orders (OrderId INTEGER, CustomerId INTEGER);
            INSERT INTO Customers VALUES (1, 'Ada', 10);
            INSERT INTO Regions VALUES (10, 'West');
            INSERT INTO Orders VALUES (101, 1), (102, NULL);
            """);
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [TestCase(JoinType.Join, false, 1)]
    [TestCase(JoinType.LeftJoin, false, 2)]
    [TestCase(JoinType.Join, true, 1)]
    [TestCase(JoinType.LeftJoin, true, 2)]
    public void FluentViewHonorsJoinKindAndCanReplaceExistingDefinition(JoinType kind, bool alias, int count)
    {
        var prefix = alias ? "c" : "Customers";
        var builder = new MigrationBuilder();
        builder.Create.View("OrderDetails").FromTable("Orders").WithElements(
            new ViewColumn("Orders", "OrderId"), new ViewColumn(prefix, "Name"),
            alias ? new ViewJoin("Customers", "c", "Id", "Orders", "CustomerId", kind)
                : new ViewJoin("Customers", "Id", "Orders", "CustomerId", kind));
        builder.Apply(provider);
        builder.Apply(provider);
        var schema = new SchemaInspector(provider);
        Assert.That(schema.ViewExists("OrderDetails"), Is.True);
        Assert.That(schema.ViewExists("Missing"), Is.False);
        Assert.That(schema.Scalar("SELECT COUNT(*) FROM OrderDetails"), Is.EqualTo((long)count));
        Assert.That(schema.Strings($"SELECT {prefix}Name FROM OrderDetails ORDER BY OrdersOrderId"),
            Is.EqualTo(count == 1 ? new[] { "Ada" } : new string[] { "Ada", null }));
    }

    [Test]
    public void ChainedViewJoinsUseTheParentAlias()
    {
        var builder = new MigrationBuilder();
        builder.Create.View("OrderRegions").FromTable("Orders").WithElements(
            new ViewColumn("Orders", "OrderId"), new ViewColumn("Regions", "Label"),
            new ViewJoin("Customers", "c", "Id", "Orders", "CustomerId", JoinType.Join),
            new ViewJoin(JoinType.Join, "Regions", "Id", "Customers", "c", "RegionId"));
        builder.Apply(provider);
        Assert.That(provider.ExecuteStringQuery("SELECT RegionsLabel FROM OrderRegions"), Is.EqualTo(new[] { "West" }));
    }

    [Test]
    public void FieldViewsGroupColumnsByRelationshipAndAssignDistinctJoinAliases()
    {
        var name = new ViewField("Name", "Customers", "Id", "Orders", "CustomerId");
        var builder = new MigrationBuilder();
        builder.Create.View("OrderDetails").FromTable("Orders").WithFields(
            new ViewField("OrderId"), name,
            new ViewField("RegionId", "Customers", "Id", "Orders", "CustomerId"));
        name.ColumnName = "Missing";
        builder.Apply(provider);
        Assert.That(provider.ExecuteStringQuery("SELECT Name || ':' || RegionId FROM OrderDetails"), Is.EqualTo(new[] { "Ada:10" }));

        provider.ExecuteNonQuery("CREATE TABLE Assignments (Id INTEGER, CustomerId INTEGER, RegionId INTEGER); INSERT INTO Assignments VALUES (1, 1, 10)");
        provider.AddView("AssignmentDetails", "Assignments", new IViewField[]
        {
            new ViewField("Id", "Assignments", null, null, null),
            new ViewField("Name", "Customers", "Id", "Assignments", "CustomerId"),
            new ViewField("Label", "Regions", "Id", "Assignments", "RegionId")
        });
        Assert.That(provider.ExecuteStringQuery("SELECT Name || ':' || Label FROM AssignmentDetails"), Is.EqualTo(new[] { "Ada:West" }));
    }

    [Test]
    public void FieldViewsCanJoinTheSameTableThroughDifferentKeys()
    {
        provider.ExecuteNonQuery("CREATE TABLE Transfers (SenderId INTEGER, RecipientId INTEGER); INSERT INTO Customers VALUES (2, 'Grace', 10); INSERT INTO Transfers VALUES (1, 2)");
        provider.AddView("TransferNames", "Transfers", new IViewField[]
        {
            new ViewField("Name", "Customers", "Id", "Transfers", "SenderId"),
            new ViewField("Name", "Customers", "Id", "Transfers", "RecipientId")
        });
        using var command = provider.CreateCommand();
        using var reader = provider.ExecuteQuery(command, "SELECT * FROM TransferNames");
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo("Ada"));
        Assert.That(reader.GetString(1), Is.EqualTo("Grace"));
        Assert.That(reader.Read(), Is.False);
    }
}
