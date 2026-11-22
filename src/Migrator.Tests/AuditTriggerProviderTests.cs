using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using Migrator.Tests.Settings;
using Migrator.Tests.Providers.Live;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests;

[TestFixture(ProviderTypes.SQLite, null, Category = "SQLite")]
[TestFixture(ProviderTypes.PostgreSQL, "PostgreSQL", Category = "PostgreSQL")]
[TestFixture(ProviderTypes.SqlServer, "SQLServer", Category = "SQLServer")]
[TestFixture(ProviderTypes.Mysql, "MySQL", Category = "MySQL")]
[TestFixture(ProviderTypes.MariaDB, "MariaDB", Category = "MariaDB")]
[TestFixture(ProviderTypes.Oracle, "Oracle", Category = "Oracle")]
[TestFixture(ProviderTypes.IBM_DB2, "Db2", Category = "Db2")]
public class AuditTriggerProviderTests(ProviderTypes type, string configurationId)
{
    private ITransformationProvider p;
    private IDbConnection connection;
    private AuditFixture fixture;
    private sealed class AuditFixture(string database, ProviderTypes providerType) : LiveProviderFixture(database, providerType)
    {
        public ITransformationProvider Current => Provider;
    }
    private string table, audit;
    private readonly List<string> triggers = new();
    private string T => p.QuoteTableNameIfRequired(table);
    private string A => p.QuoteTableNameIfRequired(audit);
    private string Q(string s) => p.QuoteColumnNameIfRequired(s);
    [SetUp]
    public async Task Setup()
    {
        if (type is ProviderTypes.Oracle or ProviderTypes.IBM_DB2)
        {
            fixture = new AuditFixture(configurationId, type);
            await fixture.SetUp();
            p = fixture.Current;
        }
        else
        {
            var config = configurationId == null ? null : new ConfigurationReader().GetDatabaseConnectionConfigById(configurationId);
            connection = type switch
            {
                ProviderTypes.SQLite => new SqliteConnection("Data Source=:memory:"),
                ProviderTypes.PostgreSQL => new Npgsql.NpgsqlConnection(config.ConnectionString),
                ProviderTypes.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(config.ConnectionString),
                _ => new MySql.Data.MySqlClient.MySqlConnection(config?.ConnectionString ?? "Server=127.0.0.1;Database=testdb;User ID=root;Password=rootpass;Pooling=false")
            };
            connection.Open(); p = ProviderFactory.Create(type, connection, config?.Schema);
        }
        var suffix = Guid.NewGuid().ToString("N")[..10]; table = "audit_item_" + suffix; audit = "audit_log_" + suffix;
        p.AddTable(table, new Column("Id", DbType.String, 50), new Column("Label", DbType.String, 100), new Column("Quantity", DbType.Int32), new Column("Optional", DbType.String, 50), new PrimaryKeyConstraint("pk_" + suffix, "Id"));
        p.AddTable(audit, new Column("OccurredAt", DbType.String, 100), new Column("EntityId", DbType.String, 50), new Column("Operation", DbType.String, 10), new Column("BeforeJson", DbType.String, 4000));
        triggers.Clear();
    }
    [TearDown]
    public void Cleanup()
    {
        try
        {
            if (p == null) return;
            foreach (var name in triggers) p.DropAuditTrigger(name, table);
            p.RemoveTable(table); p.RemoveTable(audit);
        }
        finally
        {
            if (fixture != null) fixture.TearDown();
            else p?.Dispose();
            connection?.Dispose();
            fixture = null; p = null; connection = null;
        }
    }
    private void Create(TriggerEvent operation, SqlExpression condition = null, SqlExpression recordedAt = null)
    {
        var name = "audit_" + Guid.NewGuid().ToString("N")[..10];
        var definition = new AuditTrigger(name, table, operation, audit, operation == TriggerEvent.Delete ? Old("Id") : New("Id"),
            [new("label\\new\\", Old("Label")), new("quantity", Old("Quantity")), new("optional", Old("Optional"))], condition, recordedAt, KeyColumns: ["Id"]);
        var builder = new MigrationBuilder(); builder.Create.AuditTrigger(definition); builder.Apply(p); triggers.Add(name);
    }
    [Test]
    public void CapturesSelectedPreviousValuesAndTransactionalMultirowEvents()
    {
        foreach (var operation in Enum.GetValues<TriggerEvent>()) Create(operation);
        string label = "A\"\\é";
        p.Insert(table, ["Id", "Label", "Quantity", "Optional"], ["account/2026", label, 3, null]);
        p.Insert(table, ["Id", "Label", "Quantity", "Optional"], [Guid.NewGuid().ToString(), "B", 4, null]);
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A} WHERE {Q("BeforeJson")} IS NULL")), Is.EqualTo(2));
        p.ExecuteNonQuery($"UPDATE {T} SET {Q("Quantity")}={Q("Quantity")}+1");
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A}")), Is.EqualTo(4));
        var json = Convert.ToString(p.ExecuteScalar($"SELECT {Q("BeforeJson")} FROM {A} WHERE {Q("EntityId")}='account/2026' AND {Q("Operation")}='UPDATE'"));
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("label\\new\\").GetString(), Is.EqualTo(label));
        Assert.That(doc.RootElement.GetProperty("quantity").GetInt32(), Is.EqualTo(3));
        Assert.That(doc.RootElement.GetProperty("optional").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(doc.RootElement.EnumerateObject().Count(), Is.EqualTo(3));
        p.Commit(); p.BeginTransaction(); p.ExecuteNonQuery($"DELETE FROM {T}");
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A}")), Is.EqualTo(6));
        p.Rollback();
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A}")), Is.EqualTo(4));
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {T}")), Is.EqualTo(2));
        p.ExecuteNonQuery($"DELETE FROM {T}");
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A}")), Is.EqualTo(6));
    }
    [Test]
    public void DefaultClockStoresNativeUtcTimestamp()
    {
        p.RemoveTable(audit);
        p.AddTable(audit, new Column("OccurredAt", DbType.DateTime2), new Column("EntityId", DbType.String, 50), new Column("Operation", DbType.String, 10), new Column("BeforeJson", DbType.String, 4000));
        Create(TriggerEvent.Update);
        p.Insert(table, ["Id", "Label", "Quantity"], ["clock", "A", 3]);
        p.ExecuteNonQuery($"UPDATE {T} SET {Q("Quantity")}=4");
        var timestamp = DateTime.Parse(Convert.ToString(p.ExecuteScalar($"SELECT {Q("OccurredAt")} FROM {A}"), System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.That(timestamp, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(5)));
    }
    [Test]
    public void ChangeOnlyAuditingIncludesNullTransitionsAndSuppliedClock()
    {
        Create(TriggerEvent.Update, Old("Optional").ChangedFrom(New("Optional")), Value("2026-01-02T03:04:05.123456Z"));
        p.Insert(table, ["Id", "Label", "Quantity"], ["key", "A", 3]);
        p.ExecuteNonQuery($"UPDATE {T} SET {Q("Quantity")}=4");
        p.ExecuteNonQuery($"UPDATE {T} SET {Q("Optional")}=NULL");
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A}")), Is.Zero);
        p.ExecuteNonQuery($"UPDATE {T} SET {Q("Optional")}='value'");
        p.ExecuteNonQuery($"UPDATE {T} SET {Q("Optional")}=NULL");
        Assert.That(Convert.ToInt32(p.ExecuteScalar($"SELECT COUNT(*) FROM {A}")), Is.EqualTo(2));
        Assert.That(p.ExecuteStringQuery($"SELECT {Q("OccurredAt")} FROM {A}"), Is.All.EqualTo("2026-01-02T03:04:05.123456Z"));
    }
}

[Category("SQLite")]
public class SQLiteAuditNamespaceTests
{
    [TestCase("rowid", false)]
    [TestCase("OID", false)]
    [TestCase("_RoWiD_", false)]
    [TestCase("rowid", true)]
    [TestCase("OID", true)]
    [TestCase("_RoWiD_", true)]
    public void UpdateOfAuditsImplicitRowidAliasesOnlyWhereTheyExist(string alias, bool withoutRowid)
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p.ExecuteNonQuery("CREATE TABLE Item(Value INTEGER PRIMARY KEY)" + (withoutRowid ? " WITHOUT ROWID" : "") + "; CREATE TABLE Audit(OccurredAt TEXT,EntityId INTEGER,Operation TEXT,BeforeJson TEXT)");
        var definition = new AuditTrigger("audit", "Item", TriggerEvent.Update, "Audit", Old("Value"), [], UpdateColumns: [alias]);
        if (withoutRowid)
        {
            Assert.Throws<ArgumentException>(() => p.CreateAuditTrigger(definition));
            Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM sqlite_master WHERE type='trigger'"), Is.EqualTo(0L));
            return;
        }
        p.CreateAuditTrigger(definition);
        p.Insert("Item", ["Value"], [1]);
        p.Update("Item", [alias], [2], ["Value"], [1]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(1L));
        p.DropAuditTrigger("audit", "Item");
        p.Update("Item", [alias], [3], ["Value"], [2]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(1L));
    }
    [Test]
    public void TemporaryAuditPredicateUsesOrdinaryRelatedTableLookup()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p.ExecuteNonQuery("CREATE TABLE main.Parent(Id INTEGER); INSERT INTO main.Parent VALUES(1); CREATE TEMP TABLE Item(Id INTEGER); CREATE TEMP TABLE Audit(OccurredAt TEXT,EntityId INTEGER,Operation TEXT,BeforeJson TEXT)");
        p.CreateAuditTrigger(new("audit", "temp.Item", TriggerEvent.Insert, "temp.Audit", New("Id"), [], Exists("Parent", "p", Column("Id", "p").EqualTo(New("Id")))));
        p.Insert("temp.Item", ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM temp.Audit"), Is.EqualTo(1L));
        p.DropAuditTrigger("audit", "temp.Item");
    }
    [Test]
    public void UpdateOfRejectsMissingNamesAndValidatesAfterQueuedTablesExist()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var b = new MigrationBuilder();
        b.Create.Table("Item").WithColumn("Id").AsInt32().WithColumn("CurrentStatus").AsInt32();
        b.Create.Table("Audit").WithColumn("OccurredAt").AsString(100).WithColumn("EntityId").AsInt32().WithColumn("Operation").AsString(10).WithColumn("BeforeJson").AsString(100);
        var definition = new AuditTrigger("audit", "Item", TriggerEvent.Update, "Audit", Old("Id"), [new("status", Old("CurrentStatus"))], UpdateColumns: ["currentstatus"]);
        b.Create.AuditTrigger(definition); b.Apply(p);
        p.DropAuditTrigger("audit", "Item");
        Assert.Throws<ArgumentException>(() => p.CreateAuditTrigger(definition with { UpdateColumns = ["PreviousStatus"] }));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM sqlite_master WHERE type='trigger'"), Is.EqualTo(0L));
        p.CreateAuditTrigger(definition);
        p.Insert("Item", ["Id", "CurrentStatus"], [1, 0]);
        p.Update("Item", ["Id"], [2], ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(0L));
        p.Update("Item", ["CurrentStatus"], [1], ["Id"], [2]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(1L));
    }
    [Test]
    public void ExplicitAttachedSubjectBindsPredicateAndAuditTargetLocally()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p.ExecuteNonQuery("ATTACH DATABASE ':memory:' AS aux; CREATE TABLE main.Parent(Id INTEGER,Finalized INTEGER); INSERT INTO main.Parent VALUES(1,0); CREATE TABLE aux.Parent(Id INTEGER,Finalized INTEGER); INSERT INTO aux.Parent VALUES(1,1); CREATE TABLE aux.Item(Id INTEGER); CREATE TABLE aux.Audit(OccurredAt TEXT,EntityId INTEGER,Operation TEXT,BeforeJson TEXT); CREATE TABLE main.Audit(OccurredAt TEXT,EntityId INTEGER,Operation TEXT,BeforeJson TEXT)");
        p.CreateAuditTrigger(new("audit", "aux.Item", TriggerEvent.Insert, "Audit", New("Id"), [], Exists("Parent", "p", Column("Id", "p").EqualTo(New("Id")).And(Column("Finalized", "p").EqualTo(Value(1))))));
        p.Insert("aux.Item", ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM aux.Audit"), Is.EqualTo(1L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM main.Audit"), Is.EqualTo(0L));
        p.DropAuditTrigger("audit", "aux.Item");
    }
    [Test]
    public void MainAuditAndDropIgnoreTempNamesakes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p.ExecuteNonQuery("CREATE TABLE main.Item(Id INTEGER); CREATE TEMP TABLE Item(Id INTEGER); CREATE TABLE main.Audit(OccurredAt TEXT,EntityId INTEGER,Operation TEXT,BeforeJson TEXT); CREATE TEMP TRIGGER audit AFTER INSERT ON temp.Item BEGIN SELECT 1; END");
        p.CreateAuditTrigger(new("audit", "Item", TriggerEvent.Insert, "Audit", New("Id"), []));
        p.ExecuteNonQuery("INSERT INTO main.Item VALUES(1)");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM main.Audit"), Is.EqualTo(1L));
        p.DropAuditTrigger("audit", "Item");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM temp.sqlite_master WHERE type='trigger' AND name='audit'"), Is.EqualTo(1L));
    }
    [Test]
    public void IgnoredAuditConflictAbortsTheSourceWrite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p.ExecuteNonQuery("CREATE TABLE Item(Id INTEGER); CREATE TABLE Audit(OccurredAt TEXT,EntityId INTEGER UNIQUE,Operation TEXT,BeforeJson TEXT); INSERT INTO Audit(EntityId) VALUES(1)");
        p.CreateAuditTrigger(new("audit", "Item", TriggerEvent.Insert, "Audit", New("Id"), []));
        Assert.Catch(() => p.ExecuteNonQuery("INSERT OR IGNORE INTO Item VALUES(1)"));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Item"), Is.EqualTo(0L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(1L));
        p.ExecuteNonQuery("INSERT OR IGNORE INTO Item VALUES(2)");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(2L));
    }
    [Test]
    public void QualifiedAuditTargetUsesTheConfiguredAttachment()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, "aux");
        p.ExecuteNonQuery("ATTACH DATABASE ':memory:' AS aux; CREATE TABLE aux.Item(Id INTEGER, Label TEXT); CREATE TABLE aux.Audit(OccurredAt TEXT,EntityId INTEGER,Operation TEXT,BeforeJson TEXT); INSERT INTO aux.Item VALUES(1,'old')");
        var a = new AuditTrigger("audit", "Item", TriggerEvent.Update, "aux.Audit", Old("Id"), [new("label", Old("Label"))]);
        p.CreateAuditTrigger(a); p.ExecuteNonQuery("UPDATE aux.Item SET Label='new'");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM aux.Audit"), Is.EqualTo(1L));
        p.DropAuditTrigger("audit", "Item");
        Assert.Throws<NotSupportedException>(() => p.CreateAuditTrigger(a with { AuditTable = "main.Audit" }));
        p["PostgreSQL"].CreateAuditTrigger(a); p["PostgreSQL"].DropAuditTrigger("audit", "Item");
    }
}
