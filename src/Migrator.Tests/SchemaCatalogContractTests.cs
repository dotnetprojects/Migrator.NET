using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

public class SchemaCatalogContractTests
{
    [Test]
    public void ExplicitAndDefaultNamespacesEnumerateOnlyTheirOwnObjects()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, "sales");
        provider.ExecuteNonQuery("ATTACH ':memory:' AS sales; CREATE TABLE main.Orders(Wrong INTEGER); CREATE TABLE sales.Orders(Id INTEGER, Total INTEGER); CREATE TABLE sales.[Order Details](Id INTEGER)");
        Assert.That(provider.GetTables("sales"), Is.EquivalentTo(new[] { "Orders", "Order Details" }));
        Assert.That(provider.GetTables(), Is.EquivalentTo(new[] { "Orders", "Order Details" }));
        Assert.That(provider.GetColumns("sales", "Orders"), Is.EqualTo(new[] { "Id", "Total" }));
        Assert.That(provider.GetColumns("main", "Orders"), Is.EqualTo(new[] { "Wrong" }));
    }
}
