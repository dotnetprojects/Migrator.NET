using System.Data;
using System.Data.Common;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

public class SchemaCatalogContractTests
{
    [Test]
    public void SchemaColumnEnumerationReturnsColumnNamesAndScopesTheRequest()
    {
        using var table = new DataTable();
        table.Columns.Add("TABLE_NAME"); table.Columns.Add("COLUMN_NAME");
        table.Rows.Add("Orders", "Id"); table.Rows.Add("Orders", "Total");
        var connection = Substitute.For<DbConnection>();
        connection.GetSchema("Columns", Arg.Any<string[]>()).Returns(table);
        using var provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        Assert.That(provider.GetColumns("sales", "Orders").ToArray(), Is.EqualTo(new[] { "Id", "Total" }));
        connection.Received(1).GetSchema("Columns", Arg.Is<string[]>(x => x.Length == 4 && x[0] == null && x[1] == "sales" && x[2] == "Orders" && x[3] == null));
    }

    [Test]
    public void SchemaTableEnumerationPreservesReturnedNamesAndSchemaRestriction()
    {
        using var table = new DataTable(); table.Columns.Add("TABLE_NAME");
        table.Rows.Add("Orders"); table.Rows.Add("Order Details");
        var connection = Substitute.For<DbConnection>();
        connection.GetSchema("Tables", Arg.Any<string[]>()).Returns(table);
        using var provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        Assert.That(provider.GetTables("sales").ToArray(), Is.EqualTo(new[] { "Orders", "Order Details" }));
        connection.Received(1).GetSchema("Tables", Arg.Is<string[]>(x => x.Length == 4 && x[1] == "sales" && x[2] == null));
    }
}
