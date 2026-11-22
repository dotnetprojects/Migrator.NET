using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests.Providers.Live;

[Category("SQLServer")]
public class SqlServerQueryViewPreviewTests() : LiveProviderFixture("SQLServer", ProviderTypes.SqlServer)
{
    [Test]
    public void TableAndViewPreviewExecuteAsOneBatch()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8], table = "preview_source_" + suffix, view = "preview_view_" + suffix;
        var b = new MigrationBuilder(); b.Create.Table(table).WithColumn("Id").AsInt32();
        b.Create.View(view, new SelectQuery(table, "s", [new(Column("Id", "s"), "Id")]));
        try
        {
            Provider.ExecuteNonQuery(string.Join("\n", b.Preview(new SqlGenerationContext(ProviderTypes.SqlServer))));
            Assert.That(Provider.ViewExists(view), Is.True);
        }
        finally { Provider.DropView(view, true); }
    }
}

[TestFixture("SQLServer", ProviderTypes.SqlServer, Category = "SQLServer")]
[TestFixture("PostgreSQL", ProviderTypes.PostgreSQL, Category = "PostgreSQL")]
[TestFixture("Oracle", ProviderTypes.Oracle, Category = "Oracle")]
[TestFixture("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
[TestFixture("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
[TestFixture("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
[TestFixture("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
[TestFixture("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
[TestFixture("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
[TestFixture("Hana", ProviderTypes.Hana, Category = "Hana")]
public class QueryViewTests(string database, ProviderTypes type) : LiveProviderFixture(database, type)
{
    [Test]
    public void FilteredAliasedViewReflectsCancellationAndCanBeReplaced() => VerifyFilteredView(Provider);
    public static void VerifyFilteredView(ITransformationProvider Provider)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string invoices = "q_invoice_" + suffix, links = "q_link_" + suffix, cancellations = "q_cancel_" + suffix, view = "q_active_" + suffix;
        Provider.AddTable(invoices, new Column("Id", DbType.Int32), new Column("Number", DbType.Int32));
        Provider.AddTable(links, new Column("InvoiceId", DbType.Int32), new Column("AdvanceId", DbType.Int32));
        Provider.AddTable(cancellations, new Column("OwnerId", DbType.Int32));
        foreach (var row in new object[][] { [1, 100], [2, null], [3, 300], [4, 400] }) Provider.Insert(invoices, ["Id", "Number"], row);
        foreach (var row in new object[][] { [1, 10], [2, 20], [3, 30] }) Provider.Insert(links, ["InvoiceId", "AdvanceId"], row);
        Provider.Insert(cancellations, ["OwnerId"], [3]);
        var filter = Column("Number", "i").IsNotNull().And(Exists(cancellations, "c", Column("OwnerId", "c").EqualTo(Column("Id", "i"))).Not());
        SelectQuery Query(SqlExpression predicate) => new(links, "l", [new(Column("InvoiceId", "l"), "Owner"), new(Column("AdvanceId", "l"), "Advance")], predicate,
            [new(invoices, "i", Column("Id", "i").EqualTo(Column("InvoiceId", "l")))]);
        var builder = new MigrationBuilder(); builder.Create.View(view, Query(filter)); builder.Apply(Provider);
        var quoted = Provider.QuoteTableNameIfRequired(view);
        var owner = Provider.Dialect.QuoteIdentifier("Owner");
        try
        {
            Assert.That(Convert.ToInt32(Provider.ExecuteScalar($"SELECT {owner} FROM {quoted}")), Is.EqualTo(1));
            Provider.Insert(cancellations, ["OwnerId"], [1]);
            Assert.That(Convert.ToInt32(Provider.ExecuteScalar($"SELECT COUNT(*) FROM {quoted}")), Is.Zero);
            Provider.ReplaceView(view, Query(Column("Number", "i").IsNotNull()));
            Assert.That(Convert.ToInt32(Provider.ExecuteScalar($"SELECT COUNT(*) FROM {quoted}")), Is.EqualTo(2));
            using var command = Provider.CreateCommand(); using var reader = Provider.ExecuteQuery(command, $"SELECT * FROM {quoted}");
            Assert.That(reader.GetName(0), Is.EqualTo("Owner")); Assert.That(reader.GetName(1), Is.EqualTo("Advance"));
        }
        finally { Provider.DropView(view); }
        Provider.DropView(view, true);
    }
    [Test]
    public void LeftJoinKeepsUnmatchedRowsAndSqlNull() => VerifyLeftJoin(Provider);
    public static void VerifyLeftJoin(ITransformationProvider Provider)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string a = "q_a_" + suffix, b = "q_b_" + suffix, v = "q_v_" + suffix;
        Provider.AddTable(a, new Column("Id", DbType.Int32)); Provider.AddTable(b, new Column("Id", DbType.Int32));
        Provider.Insert(a, ["Id"], [1]);
        var query = new SelectQuery(a, "a", [new(Column("Id", "a"), "LeftId"), new(Column("Id", "b"), "RightId")],
            joins: [new(b, "b", Column("Id", "a").EqualTo(Column("Id", "b")), true)]);
        Provider.CreateView(v, query);
        try
        {
            Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM " + Provider.QuoteTableNameIfRequired(v))), Is.EqualTo(1));
            Assert.That(Provider.ExecuteScalar("SELECT " + Provider.Dialect.QuoteIdentifier("RightId") + " FROM " + Provider.QuoteTableNameIfRequired(v)), Is.EqualTo(DBNull.Value));
        }
        finally { Provider.DropView(v); }
    }
}
