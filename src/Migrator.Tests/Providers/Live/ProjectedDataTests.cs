using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests.Providers.Live;

[TestFixture("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
[TestFixture("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
[TestFixture("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
public class TypedProjectionValueTests(string database, ProviderTypes type) : LiveProviderFixture(database, type)
{
    [TestCase(false)]
    [TestCase(true)]
    public void DecimalAndTimestampMarkersWorkWithoutAnInferredType(bool inCase)
    {
        const decimal amount = 123456.123456m;
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234000);
        string table = "typed_projection", t = Provider.QuoteTableNameIfRequired(table);
        string Q(string name) => Provider.QuoteColumnNameIfRequired(name);
        var timestampType = Provider.Dialect is DotNetProjects.Migrator.Providers.Impl.DB2.DB2Dialect ? "TIMESTAMP(7)" : Provider.Dialect.GetTypeName(DbType.DateTime);
        Provider.ExecuteNonQuery($"CREATE TABLE {t} ({Q("Amount")} DECIMAL(18,6), {Q("RecordedAt")} {timestampType})");
        var price = inCase ? Case(Value(1).EqualTo(Value(1)), Parameter(amount), Parameter(0.000001m)) : Parameter(amount);
        var stamp = inCase ? Case(Value(1).EqualTo(Value(1)), Parameter(timestamp), Parameter(timestamp.AddDays(1))) : Parameter(timestamp);
        Provider.InsertFromQuery(table, ["Amount", "RecordedAt"], new SelectQuery(null, null, [new(price, "Price"), new(stamp, "Stamp")]));
        using var command = Provider.CreateCommand(); using var reader = Provider.Select(command, table, ["Amount", "RecordedAt"]);
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetDecimal(0), Is.EqualTo(amount));
        Assert.That(reader.GetDateTime(1).Ticks, Is.EqualTo(timestamp.Ticks));
        Assert.That(reader.Read(), Is.False);
    }
}

[Category("Db2")]
public class Db2ProjectionPrecisionTests() : LiveProviderFixture("Db2", ProviderTypes.IBM_DB2)
{
    [TestCase(false)]
    [TestCase(true)]
    public void MarkersPreserveFullDecimalScaleAndTimestampTicks(bool inCase)
    {
        const decimal amount = 123456789.12345678901234567890m;
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
        string table = "precise_projection", t = Provider.QuoteTableNameIfRequired(table);
        string Q(string name) => Provider.QuoteColumnNameIfRequired(name);
        Provider.ExecuteNonQuery($"CREATE TABLE {t} ({Q("Amount")} DECIMAL(29,20), {Q("RecordedAt")} TIMESTAMP(7))");
        var price = inCase ? Case(Value(1).EqualTo(Value(1)), Parameter(amount), Parameter(0.00000000000000000001m)) : Parameter(amount);
        var stamp = inCase ? Case(Value(1).EqualTo(Value(1)), Parameter(timestamp), Parameter(timestamp.AddDays(1))) : Parameter(timestamp);
        Provider.InsertFromQuery(table, ["Amount", "RecordedAt"], new SelectQuery(null, null, [new(price, "Price"), new(stamp, "Stamp")]));
        using var command = Provider.CreateCommand(); using var reader = Provider.Select(command, table, ["Amount", "RecordedAt"]);
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetDecimal(0), Is.EqualTo(amount));
        Assert.That(reader.GetDateTime(1).Ticks, Is.EqualTo(timestamp.Ticks));
        Assert.That(reader.Read(), Is.False);
    }
}

[Category("Db2")]
public class Db2ProjectionStringTests() : LiveProviderFixture("Db2", ProviderTypes.IBM_DB2)
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void UnicodeMarkerCapacityMatchesUtf8Octets(bool inCase, bool large)
    {
        var text = large ? string.Concat(Enumerable.Repeat("日本語", 4000)) : "日本語 😀 e\u0301";
        Provider.AddTable("unicode_projection", new Column("Value", DbType.String, int.MaxValue));
        var value = inCase ? Case(Value(1).EqualTo(Value(1)), Parameter(text), Value("other")) : Parameter(text);
        Provider.InsertFromQuery("unicode_projection", ["Value"], new SelectQuery(null, null, [new(value, "Text")]));
        using var command = Provider.CreateCommand();
        using var reader = Provider.Select(command, "unicode_projection", ["Value"]);
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetString(0), Is.EqualTo(text));
        Assert.That(reader.Read(), Is.False);
    }
}

[Category("Firebird")]
public class FirebirdProjectionBoundaryTests() : LiveProviderFixture("Firebird", ProviderTypes.Firebird)
{
    [TestCase(4001)]
    [TestCase(8192)]
    [TestCase(20000)]
    public void LongCaseParametersPreserveTheWholeValue(int length)
    {
        var text = new string('é', length);
        Provider.AddTable("long_projection", new Column("Value", DbType.String, int.MaxValue));
        var query = new SelectQuery(null, null, [new(Case(Value(1).EqualTo(Value(1)), Parameter(text), Value("other")), "Text")]);
        Provider.InsertFromQuery("long_projection", ["Value"], query);
        Assert.That(Provider.ExecuteScalar("SELECT " + Provider.QuoteColumnNameIfRequired("Value") + " FROM long_projection"), Is.EqualTo(text));
    }
}

[Category("Unit")]
public class SelectQuerySourceCompatibilityTests
{
    [Test]
    public void OriginalExplicitNullPredicateCallRemainsUnambiguous()
    {
        var query = new SelectQuery("Source", "s", new[] { new SelectItem(Column("Id", "s"), "Id") }, null);
        Assert.That(query.Predicate, Is.Null);
        Assert.That(query.ToSql(DotNetProjects.Migrator.ProviderFactory.DialectForProvider(ProviderTypes.SQLite)), Does.Not.Contain("WHERE"));
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
public class ProjectedDataTests(string database, ProviderTypes type) : LiveProviderFixture(database, type)
{
    [TestCase(false)]
    [TestCase(true)]
    public void ProjectionsPreserveValuesAndCardinality(bool window) => Verify(Provider, window);
    public static void Verify(ITransformationProvider p, bool window)
    {
        var sequence = window ? RowNumber([Column("Account", "s")], new(Column("Created", "s")), new(Column("Id", "s"))) : Value(99);
        if (window && p.Dialect is DotNetProjects.Migrator.Providers.Impl.Sybase.SybaseDialect)
        { Assert.Throws<NotSupportedException>(() => sequence.ToSql(new SqlExpressionContext(p.Dialect))); return; }
        string suffix = Guid.NewGuid().ToString("N")[..8], source = "proj_src_" + suffix, target = "proj_dst_" + suffix;
        p.AddTable(source, new Column("Id", DbType.Int32), new Column("Account", DbType.Int32), new Column("Created", DbType.Int32), new Column("Payload", DbType.Binary, 100), new Column("Eligible", DbType.Int32));
        p.AddTable(target, new Column("LegacyId", DbType.Int32), new Column("Sequence", DbType.Int64), new Column("select", DbType.String, 100), new Column("Extra", DbType.Int32), new Column("BlobCopy", DbType.Binary, 100));
        for (int id = 1; id <= 5; id++) p.Insert(source, ["Id", "Account", "Created", "Payload", "Eligible"], [id, id % 2, 1, new byte[] { (byte)id, 0, 255 }, id == 5 ? 0 : 1]);
        string label = "O'Brien {json} --";
        var query = new SelectQuery(source, "s", [new(Column("Id", "s"), "Id"), new(sequence, "Sequence"),
            new(Case(Column("Eligible", "s").EqualTo(Value(1)), Parameter(label), Value("other")), "Label"),
            new(Value(null), "Extra"), new(Column("Payload", "s"), "Bytes")], Column("Eligible", "s").EqualTo(Parameter(1)));
        var builder = new MigrationBuilder(); builder.InsertFromQuery(target, ["LegacyId", "Sequence", "select", "Extra", "BlobCopy"], query); builder.Apply(p);
        string t = p.QuoteTableNameIfRequired(target); string Q(string s) => p.QuoteColumnNameIfRequired(s);
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {t}")), Is.EqualTo(4));
        Assert.That(p.ExecuteStringQuery($"SELECT {Q("select")} FROM {t}"), Is.All.EqualTo(label));
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {t} WHERE {Q("Extra")} IS NULL")), Is.EqualTo(4));
        using var command = p.CreateCommand(); using var reader = p.ExecuteQuery(command, $"SELECT {Q("LegacyId")},{Q("Sequence")},{Q("BlobCopy")} FROM {t} ORDER BY {Q("LegacyId")}");
        int count = 0;
        while (reader.Read())
        {
            int id = Convert.ToInt32(reader.GetValue(0));
            Assert.That(Convert.ToInt32(reader.GetValue(1)), Is.EqualTo(window ? (id + 1) / 2 : 99));
            Assert.That((byte[])reader.GetValue(2), Is.EqualTo(new byte[] { (byte)id, 0, 255 })); count++;
        }
        Assert.That(count, Is.EqualTo(4));
    }
}

[Category("SQLite")]
public class SQLiteProjectedDataTests
{
    [Test]
    public void ProjectionUsesConfiguredNamespaceAndInactiveProviderIsSkipped()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = DotNetProjects.Migrator.ProviderFactory.Create(ProviderTypes.SQLite, c, "aux");
        p.ExecuteNonQuery("ATTACH DATABASE ':memory:' AS aux; CREATE TABLE main.Source(Id INTEGER); INSERT INTO main.Source VALUES(9); CREATE TABLE aux.Source(Id INTEGER); INSERT INTO aux.Source VALUES(1); CREATE TABLE aux.Target(Id INTEGER)");
        var query = new SelectQuery("Source", "s", [new(Column("Id", "s"), "Id")]);
        p.InsertFromQuery("Target", ["Id"], query);
        Assert.That(p.ExecuteScalar("SELECT Id FROM aux.Target"), Is.EqualTo(1L));
        Assert.That(p["PostgreSQL"].InsertFromQuery("Missing", ["Id"], query), Is.Zero);
    }
    [Test, NonParallelizable]
    public void ConsoleLoggingPreservesLiteralJsonAndTraceSql()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        var original = Console.Out; using var output = new System.IO.StringWriter();
        try
        {
            Console.SetOut(output);
            p.Logger = new DotNetProjects.Migrator.Framework.Loggers.Logger(true, new DotNetProjects.Migrator.Framework.Loggers.ConsoleWriter());
            p.ExecuteNonQuery("CREATE TABLE Data (Json TEXT); INSERT INTO Data VALUES('{json}')");
            p.InsertFromQuery("Data", ["Json"], new SelectQuery(null, null, [new(Value("{json}"), "Json")]));
            Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Data"), Is.EqualTo(2L));
            Assert.That(output.ToString(), Does.Contain("{json}"));
        }
        finally { Console.SetOut(original); }
    }
    [TestCase(false)]
    [TestCase(true)]
    public void Projections(bool window)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        ProjectedDataTests.Verify(p, window);
    }
    [Test]
    public void OrderedAntiJoinSelectsOneSeedAndRollbackRemovesIt()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Candidates (Id INTEGER); INSERT INTO Candidates VALUES (1),(2),(3); CREATE TABLE Used (Id INTEGER UNIQUE); INSERT INTO Used VALUES (1)");
        var query = SelectQuery.Ordered("Candidates", "c", [new(Column("Id", "c"), "Id")], predicate: Exists("Used", "u", Column("Id", "u").EqualTo(Column("Id", "c"))).Not(), orderBy: [new(Column("Id", "c"))], limit: 1);
        p.BeginTransaction(); Assert.That(p.InsertFromQuery("Used", ["Id"], query), Is.EqualTo(1));
        Assert.That(p.ExecuteScalar("SELECT MAX(Id) FROM Used"), Is.EqualTo(2L)); p.Rollback();
        Assert.That(p.ExecuteScalar("SELECT MAX(Id) FROM Used"), Is.EqualTo(1L));
        Assert.Throws<ArgumentException>(() => p.InsertFromQuery("Used", ["Id", "Extra"], query));
    }
    [Test]
    public void ScalarAggregateAndConstantOnlySelectDoNotRequireClientIteration()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Source (Number INTEGER); CREATE TABLE Sequence (Next INTEGER)");
        p.InsertFromQuery("Sequence", ["Next"], new SelectQuery("Source", "s", [new(Coalesce(Column("Number", "s").Max(), Parameter(100)), "Next")]));
        Assert.That(p.ExecuteScalar("SELECT Next FROM Sequence"), Is.EqualTo(100L));
        p.InsertFromQuery("Source", ["Number"], new SelectQuery(null, null, [new(Parameter(123), "Number")]));
        Assert.That(p.ExecuteScalar("SELECT Number FROM Source"), Is.EqualTo(123L));
    }
}
