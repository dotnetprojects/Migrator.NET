using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
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
[TestFixture(ProviderTypes.Firebird, "Firebird", Category = "Firebird")]
[TestFixture(ProviderTypes.IBM_DB2, "Db2", Category = "Db2")]
[TestFixture(ProviderTypes.IBM_Informix, "Informix", Category = "Informix")]
[TestFixture(ProviderTypes.Sybase, "Sybase", Category = "Sybase")]
[TestFixture(ProviderTypes.Hana, "Hana", Category = "Hana")]
public partial class GuardTriggerProviderTests(ProviderTypes type, string configurationId)
{
    private ITransformationProvider provider;
    private IDbConnection connection;
    private string table, parent;
    private GuardFixture fixture;
    private sealed class GuardFixture(string database, ProviderTypes providerType) : LiveProviderFixture(database, providerType)
    {
        public ITransformationProvider Current => Provider;
    }
    [SetUp]
    public async Task Setup()
    {
        if (type is ProviderTypes.Oracle or ProviderTypes.Firebird or ProviderTypes.IBM_DB2 or ProviderTypes.IBM_Informix or ProviderTypes.Sybase or ProviderTypes.Hana)
        {
            fixture = new GuardFixture(configurationId, type);
            await fixture.SetUp();
            provider = fixture.Current;
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
            connection.Open();
            provider = ProviderFactory.Create(type, connection, config?.Schema);
        }
        var suffix = Guid.NewGuid().ToString("N")[..10];
        table = "guard_child_" + suffix; parent = "guard_parent_" + suffix;
        provider.AddTable(parent, new Column("Id", DbType.Int32), new Column("Finalized", DbType.Int32));
        provider.AddTable(table, new Column("Id", DbType.Int32), new Column("ParentId", DbType.Int32), new Column("Amount", DbType.Int32), new PrimaryKeyConstraint("pk_" + suffix, "Id"));
        provider.Insert(parent, ["Id", "Finalized"], [1, 0]);
        provider.Insert(parent, ["Id", "Finalized"], [2, 1]);
        provider.Insert(table, ["Id", "ParentId", "Amount"], [1, 1, 10]);
        provider.Insert(table, ["Id", "ParentId", "Amount"], [2, 2, 10]);
    }
    [TearDown]
    public void Cleanup()
    {
        try
        {
            // Isolated database/schema fixtures own their cleanup. In particular,
            // Firebird may retain prepared-statement metadata until disconnect.
            if (provider != null && (fixture == null || type == ProviderTypes.Oracle))
            { provider.RemoveTable(table); provider.RemoveTable(parent); }
        }
        finally
        {
            if (fixture != null) fixture.TearDown();
            else provider?.Dispose();
            connection?.Dispose();
            fixture = null; provider = null; connection = null;
        }
    }
    private string T => provider.QuoteTableNameIfRequired(table);
    private string Q(string column) => provider.QuoteColumnNameIfRequired(column);
    private (int Id, int? ParentId, int? Amount)[] ReadRowsThroughApi()
    {
        using var command = provider.CreateCommand();
        using var reader = provider.Select(command, table, ["Id", "ParentId", "Amount"]);
        var rows = new List<(int Id, int? ParentId, int? Amount)>();
        while (reader.Read()) rows.Add((Convert.ToInt32(reader.GetValue(0)), reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetValue(1)), reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2))));
        return rows.OrderBy(row => row.Id).ToArray();
    }
    private void AssertApiRows(params (int Id, int? ParentId, int? Amount)[] rows)
        => Assert.That(ReadRowsThroughApi(), Is.EqualTo(rows));
    private GuardTrigger RelatedApiGuard(TriggerEvent operation)
    {
        SqlExpression Finalized(SqlExpression key) => Exists(parent, "p", Column("Id", "p").EqualTo(key).And(Column("Finalized", "p").EqualTo(Value(1))));
        var condition = operation switch
        {
            TriggerEvent.Insert => Finalized(New("ParentId")),
            TriggerEvent.Delete => Finalized(Old("ParentId")),
            _ => Finalized(Old("ParentId")).Or(Finalized(New("ParentId")))
        };
        return new("api_guard_" + Guid.NewGuid().ToString("N")[..10], table, operation, "API finalized parent", condition, KeyColumns: ["Id"]);
    }
    private void DropGuardThroughApi(GuardTrigger guard)
    {
        var b = new MigrationBuilder(); b.Delete.Trigger(guard.Name, table); b.Apply(provider);
    }
    [TestCase(TriggerEvent.Insert)]
    [TestCase(TriggerEvent.Update)]
    [TestCase(TriggerEvent.Delete)]
    public void RelatedGuardEnforcesDatabaseIndependentWrites(TriggerEvent operation)
    {
        var guard = RelatedApiGuard(operation);
        var b = new MigrationBuilder(); b.Create.GuardTrigger(guard); b.Apply(provider);
        void BlockedWrite()
        {
            switch (operation)
            {
                case TriggerEvent.Insert: provider.Insert(table, ["Id", "ParentId", "Amount"], [4, 2, 20]); break;
                case TriggerEvent.Update: provider.Update(table, ["Amount"], [20]); break;
                case TriggerEvent.Delete: provider.Delete(table); break;
            }
        }
        Assert.That(Assert.Catch(BlockedWrite).ToString(), Does.Contain(guard.Message));
        AssertApiRows((1, 1, 10), (2, 2, 10));
        switch (operation)
        {
            case TriggerEvent.Insert:
                provider.Insert(table, ["Id", "ParentId", "Amount"], [3, 1, 20]);
                AssertApiRows((1, 1, 10), (2, 2, 10), (3, 1, 20)); break;
            case TriggerEvent.Update:
                provider.Update(table, ["Amount"], [20], ["Id"], [1]);
                AssertApiRows((1, 1, 20), (2, 2, 10)); break;
            case TriggerEvent.Delete:
                provider.Delete(table, ["Id"], [1]);
                AssertApiRows((2, 2, 10)); break;
        }
        DropGuardThroughApi(guard);
        BlockedWrite();
        switch (operation)
        {
            case TriggerEvent.Insert: AssertApiRows((1, 1, 10), (2, 2, 10), (3, 1, 20), (4, 2, 20)); break;
            case TriggerEvent.Update: AssertApiRows((1, 1, 20), (2, 2, 20)); break;
            case TriggerEvent.Delete: AssertApiRows(); break;
        }
    }
    [Test]
    public void DatabaseIndependentUpdatesRespectNegatedNullSafeChanges()
    {
        provider.Update(table, ["Amount"], [null], ["Id"], [1]);
        var guard = new GuardTrigger("api_guard_" + Guid.NewGuid().ToString("N")[..10], table, TriggerEvent.Update, "API unchanged", Old("Amount").ChangedFrom(New("Amount")).Not(), KeyColumns: ["Id"]);
        provider.CreateGuardTrigger(guard);
        Assert.That(Assert.Catch(() => provider.Update(table, ["Amount"], [null], ["Id"], [1])).ToString(), Does.Contain(guard.Message));
        AssertApiRows((1, 1, null), (2, 2, 10));
        provider.Update(table, ["Amount"], [5], ["Id"], [1]);
        Assert.That(Assert.Catch(() => provider.Update(table, ["Amount"], [5], ["Id"], [1])).ToString(), Does.Contain(guard.Message));
        AssertApiRows((1, 1, 5), (2, 2, 10));
        provider.Update(table, ["Amount"], [null], ["Id"], [1]);
        AssertApiRows((1, 1, null), (2, 2, 10));
        DropGuardThroughApi(guard);
        provider.Update(table, ["Amount"], [null], ["Id"], [1]);
    }
    [Test]
    public void DatabaseIndependentUpdatesProtectBothReparentingDirections()
    {
        var guard = new GuardTrigger("api_guard_" + Guid.NewGuid().ToString("N")[..10], table, TriggerEvent.Update, "API cannot reparent",
            Old("ParentId").ChangedFrom(New("ParentId")).And(Old("ParentId").EqualTo(Value(2)).Or(New("ParentId").EqualTo(Value(2)))), KeyColumns: ["Id"]);
        provider.CreateGuardTrigger(guard);
        foreach (var change in new (int Id, int? ParentId)[] { (1, 2), (2, 1), (2, null) })
        {
            Assert.That(Assert.Catch(() => provider.Update(table, ["ParentId"], [change.ParentId], ["Id"], [change.Id])).ToString(), Does.Contain(guard.Message));
            AssertApiRows((1, 1, 10), (2, 2, 10));
        }
        provider.Update(table, ["ParentId"], [1], ["Id"], [1]);
        provider.Update(table, ["ParentId"], [null], ["Id"], [1]);
        AssertApiRows((1, null, 10), (2, 2, 10));
        provider.Update(table, ["ParentId"], [1], ["Id"], [1]);
        AssertApiRows((1, 1, 10), (2, 2, 10));
        DropGuardThroughApi(guard);
    }
    [TestCase(TriggerEvent.Insert)]
    [TestCase(TriggerEvent.Update)]
    [TestCase(TriggerEvent.Delete)]
    public void RelatedGuardRejectsEntireMultirowStatement(TriggerEvent operation)
    {
        SqlExpression Finalized(SqlExpression key) => Exists(parent, "p", Column("Id", "p").EqualTo(key).And(Column("Finalized", "p").EqualTo(Value(1))));
        var condition = operation switch
        {
            TriggerEvent.Insert => Finalized(New("ParentId")),
            TriggerEvent.Delete => Finalized(Old("ParentId")),
            _ => Finalized(Old("ParentId")).Or(Finalized(New("ParentId")))
        };
        string name = "guard_" + Guid.NewGuid().ToString("N")[..10];
        var builder = new MigrationBuilder();
        string message = "Finalized parent's record C:\\new\\";
        // These two CI servers use legacy single-byte locales. Their native
        // error messages cannot represent Japanese; guard behavior is identical.
        // Keep the Unicode round-trip assertion on every UTF-8-capable fixture.
        if (type is not (ProviderTypes.IBM_Informix or ProviderTypes.Sybase)) message += " 日本語";
        builder.Create.GuardTrigger(new(name, table, operation, message, condition, KeyColumns: ["Id"]));
        builder.Apply(provider);
        string sql = operation switch
        {
            TriggerEvent.Insert => $"INSERT INTO {T} ({Q("Id")}, {Q("ParentId")}, {Q("Amount")}) SELECT {Q("Id")}+2,{Q("Id")},20 FROM {provider.QuoteTableNameIfRequired(parent)}",
            TriggerEvent.Delete => $"DELETE FROM {T}",
            _ => $"UPDATE {T} SET {Q("Amount")}=20"
        };
        var error = Assert.Catch(() => provider.ExecuteNonQuery(sql));
        Assert.That(error.ToString(), Does.Contain(message));
        Assert.That(Convert.ToInt32(provider.ExecuteScalar($"SELECT SUM({Q("Amount")}) FROM {T}")), Is.EqualTo(20));
        provider.ExecuteNonQuery(operation switch
        {
            TriggerEvent.Insert => $"INSERT INTO {T} VALUES (3,1,20)",
            TriggerEvent.Delete => $"DELETE FROM {T} WHERE {Q("Id")}=1",
            _ => $"UPDATE {T} SET {Q("Amount")}=20 WHERE {Q("Id")}=1"
        });
        provider.DropGuardTrigger(name, table);
        if (operation == TriggerEvent.Insert) provider.ExecuteNonQuery($"DELETE FROM {T} WHERE {Q("Id")}=3");
        provider.ExecuteNonQuery(sql);
        // Recreating also checks that provider-specific helper objects were removed.
        provider.CreateGuardTrigger(new(name, table, operation, message, condition, KeyColumns: ["Id"]));
        provider.DropGuardTrigger(name, table);
    }
    [Test]
    public void NegatedChangeRejectsNullToNullAndSameValueUpdates()
    {
        provider.ExecuteNonQuery($"UPDATE {T} SET {Q("Amount")}=NULL WHERE {Q("Id")}=1");
        string name = "guard_" + Guid.NewGuid().ToString("N")[..10];
        provider.CreateGuardTrigger(new(name, table, TriggerEvent.Update, "Unchanged", Old("Amount").ChangedFrom(New("Amount")).Not(), KeyColumns: ["Id"]));
        Assert.Catch(() => provider.ExecuteNonQuery($"UPDATE {T} SET {Q("Amount")}=NULL WHERE {Q("Id")}=1"));
        provider.ExecuteNonQuery($"UPDATE {T} SET {Q("Amount")}=5 WHERE {Q("Id")}=1");
        Assert.Catch(() => provider.ExecuteNonQuery($"UPDATE {T} SET {Q("Amount")}=5 WHERE {Q("Id")}=1"));
        provider.ExecuteNonQuery($"UPDATE {T} SET {Q("Amount")}=NULL WHERE {Q("Id")}=1");
        provider.DropGuardTrigger(name, table);
    }
    [Test]
    public void ReparentingChecksBothRowsAndNullSafeChanges()
    {
        string name = "guard_" + Guid.NewGuid().ToString("N")[..10];
        var condition = Old("ParentId").ChangedFrom(New("ParentId")).And(Old("ParentId").EqualTo(Value(2)).Or(New("ParentId").EqualTo(Value(2))));
        provider.CreateGuardTrigger(new(name, table, TriggerEvent.Update, "Cannot reparent", condition, KeyColumns: ["Id"]));
        Assert.Catch(() => provider.ExecuteNonQuery($"UPDATE {T} SET {Q("ParentId")}=2 WHERE {Q("Id")}=1"));
        Assert.Catch(() => provider.ExecuteNonQuery($"UPDATE {T} SET {Q("ParentId")}=NULL WHERE {Q("Id")}=2"));
        provider.ExecuteNonQuery($"UPDATE {T} SET {Q("ParentId")}=1 WHERE {Q("Id")}=1");
        provider.DropGuardTrigger(name, table);
    }
}

[Category("Unit")]
public class TriggerDefinitionTests
{
    [TestCase("rowid", false)]
    [TestCase("OID", false)]
    [TestCase("_RoWiD_", false)]
    [TestCase("rowid", true)]
    [TestCase("OID", true)]
    [TestCase("_RoWiD_", true)]
    [Category("SQLite")]
    public void UpdateOfAcceptsImplicitAliasesOnlyOnRowidTables(string alias, bool withoutRowid)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, c, null);
        p.ExecuteNonQuery("CREATE TABLE Child(Value INTEGER PRIMARY KEY)" + (withoutRowid ? " WITHOUT ROWID" : ""));
        var guard = new GuardTrigger("guard", "Child", TriggerEvent.Update, "Rowid guarded", UpdateColumns: [alias]);
        if (withoutRowid)
        {
            Assert.Throws<ArgumentException>(() => p.CreateGuardTrigger(guard));
            Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM sqlite_master WHERE type='trigger'"), Is.EqualTo(0L));
            return;
        }
        p.CreateGuardTrigger(guard);
        p.Insert("Child", ["Value"], [1]);
        Assert.That(Assert.Catch(() => p.Update("Child", [alias], [2], ["Value"], [1])).ToString(), Does.Contain("Rowid guarded"));
        p.DropGuardTrigger("guard", "Child");
        Assert.That(p.Update("Child", [alias], [2], ["Value"], [1]), Is.EqualTo(1));
    }
    [TestCase("main")]
    [TestCase("aux")]
    [TestCase("temp")]
    [Category("SQLite")]
    public void TemporaryGuardUsesOrdinaryRelatedTableLookup(string schema)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, c, null);
        p.ExecuteNonQuery($"ATTACH DATABASE ':memory:' AS aux; CREATE TABLE {schema}.Parent(Id INTEGER); INSERT INTO {schema}.Parent VALUES(1); CREATE TEMP TABLE Child(ParentId INTEGER)");
        p.CreateGuardTrigger(new("guard", "temp.Child", TriggerEvent.Insert, "Related row found", Exists("Parent", "p", Column("Id", "p").EqualTo(New("ParentId")))));
        Assert.That(Assert.Catch(() => p.Insert("temp.Child", ["ParentId"], [1])).ToString(), Does.Contain("Related row found"));
        p.DropGuardTrigger("guard", "temp.Child");
        p.Insert("temp.Child", ["ParentId"], [1]);
    }
    [TestCase("CurrentStatus", "PreviousStatus")]
    [TestCase("Σ", "σ")]
    [Category("SQLite")]
    public void InvalidUpdateOfColumnFailsBeforeTriggerCreation(string actual, string missing)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, c, null);
        p.AddTable("Child", new Column("Id", DbType.Int32), new Column(actual, DbType.Int32));
        Assert.Throws<ArgumentException>(() => p.CreateGuardTrigger(new("guard", "Child", TriggerEvent.Update, "Denied", UpdateColumns: [missing])));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM main.sqlite_master WHERE type='trigger'"), Is.EqualTo(0L));
    }
    [Test, Category("SQLite")]
    public void UpdateOfMetadataIsReadAfterQueuedTableCreation()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, c, null);
        var b = new MigrationBuilder();
        b.Create.Table("Child").WithColumn("Id").AsInt32().WithColumn("CurrentStatus").AsInt32();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Update, "Status guarded", UpdateColumns: ["currentstatus"]));
        b.Apply(p);
        p.Insert("Child", ["Id", "CurrentStatus"], [1, 0]);
        p.Update("Child", ["Id"], [2], ["Id"], [1]);
        Assert.That(Assert.Catch(() => p.Update("Child", ["CurrentStatus"], [1], ["Id"], [2])).ToString(), Does.Contain("Status guarded"));
    }
    [Test, Category("SQLite")]
    public void BulkDeleteUsesConfiguredNamespaceAndQuotesTheTable()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, c, "aux");
        p.ExecuteNonQuery("ATTACH DATABASE ':memory:' AS aux; CREATE TABLE main.\"odd table\"(Id INTEGER); CREATE TABLE aux.\"odd table\"(Id INTEGER); INSERT INTO main.\"odd table\" VALUES(1); INSERT INTO aux.\"odd table\" VALUES(2)");
        p.Delete("odd table");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM aux.\"odd table\""), Is.EqualTo(0L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM main.\"odd table\""), Is.EqualTo(1L));
    }
    [Test, Category("SQLite")]
    public void ExplicitAttachedSubjectBindsUnqualifiedRelatedTablesLocally()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, c, null);
        p.ExecuteNonQuery("ATTACH DATABASE ':memory:' AS aux; CREATE TABLE main.Parent(Id INTEGER, Finalized INTEGER); INSERT INTO main.Parent VALUES(1,0); CREATE TABLE aux.Parent(Id INTEGER,Finalized INTEGER); INSERT INTO aux.Parent VALUES(1,1); CREATE TABLE aux.Child(ParentId INTEGER)");
        p.CreateGuardTrigger(new("g", "aux.Child", TriggerEvent.Insert, "Attached finalized", Exists("Parent", "p", Column("Id", "p").EqualTo(New("ParentId")).And(Column("Finalized", "p").EqualTo(Value(1))))));
        Assert.That(Assert.Catch(() => p.Insert("aux.Child", ["ParentId"], [1])).ToString(), Does.Contain("Attached finalized"));
        p.DropGuardTrigger("g", "aux.Child");
        p.Insert("aux.Child", ["ParentId"], [1]);
    }
    [Test, Category("SQLite")]
    public void MainGuardAndDropIgnoreTempNamesakes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p.ExecuteNonQuery("CREATE TABLE main.Child(Id INTEGER); CREATE TEMP TABLE Child(Id INTEGER); CREATE TEMP TRIGGER guard BEFORE INSERT ON temp.Child BEGIN SELECT RAISE(ABORT,'temp guard'); END");
        p.CreateGuardTrigger(new("guard", "Child", TriggerEvent.Insert, "main guard"));
        Assert.That(Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO main.Child VALUES(1)")).ToString(), Does.Contain("main guard"));
        p.DropGuardTrigger("guard", "Child");
        p.ExecuteNonQuery("INSERT INTO main.Child VALUES(1)");
        Assert.That(Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO temp.Child VALUES(1)")).ToString(), Does.Contain("temp guard"));
    }
    [TestCase(ProviderTypes.SQLite, "NEW")]
    [TestCase(ProviderTypes.PostgreSQL, "old")]
    [TestCase(ProviderTypes.Mysql, "new")]
    [TestCase(ProviderTypes.Oracle, "NEW")]
    [TestCase(ProviderTypes.Firebird, "OLD")]
    [TestCase(ProviderTypes.IBM_DB2, "newrow")]
    [TestCase(ProviderTypes.IBM_Informix, "oldrow")]
    [TestCase(ProviderTypes.Hana, "newrow")]
    public void CorrelatedAliasesCannotShadowTriggerRows(ProviderTypes type, string alias)
    {
        var b = new MigrationBuilder();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Insert, "Denied", Exists("Parent", alias, Column("Id", alias).EqualTo(New("Id"))).Not()));
        Assert.Throws<ArgumentException>(() => b.Preview(new SqlGenerationContext(type)));
    }
    [Test]
    public void SqlServerPreviewIsolatesTriggerBatchAndRejectsShadowedAliases()
    {
        var b = new MigrationBuilder();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Insert, "日本語"));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.SqlServer))[0], Does.StartWith("EXEC(N'CREATE TRIGGER"));
        b = new MigrationBuilder();
        b.Create.GuardTrigger(new("guard", "Child", TriggerEvent.Insert, "Denied", Exists("Parent", "i", Column("Id", "i").EqualTo(New("Id")))));
        Assert.Throws<ArgumentException>(() => b.Preview(new SqlGenerationContext(ProviderTypes.SqlServer)));
    }
    [Test]
    public void UnquotedPostgreSqlSchemaKeepsCaseFolding()
    {
        var b = new MigrationBuilder(); b.Create.GuardTrigger(new("guard", "Sales.Child", TriggerEvent.Insert, "Denied"));
        Assert.That(b.Preview(new SqlGenerationContext(ProviderTypes.PostgreSQL))[0], Does.Contain("FUNCTION Sales.\"guard_fn\""));
    }
    [Test, Category("SQLite")]
    public void InactiveProviderSkipsTriggerOperations()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        p["PostgreSQL"].CreateGuardTrigger(new("guard", "Missing", TriggerEvent.Insert, "Denied"));
        p["PostgreSQL"].DropGuardTrigger("guard", "Missing");
        var b = new MigrationBuilder(); b.Create.GuardTrigger(new("guard", "Missing", TriggerEvent.Insert, "Denied"));
        b.Delete.Trigger("guard", "Missing"); b.Apply(p["PostgreSQL"]);
    }
    [Test, Category("SQLite")]
    public void ConfiguredNamespaceAppliesToGuardAndRelatedTable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = ProviderFactory.Create(ProviderTypes.SQLite, connection, "aux");
        p.ExecuteNonQuery("ATTACH DATABASE ':memory:' AS aux; CREATE TABLE main.Parent (Id INTEGER, Finalized INTEGER); INSERT INTO main.Parent VALUES (1,0); CREATE TABLE aux.Parent (Id INTEGER, Finalized INTEGER); INSERT INTO aux.Parent VALUES (1,1); CREATE TABLE aux.Child (ParentId INTEGER)");
        p.CreateGuardTrigger(new("guard", "Child", TriggerEvent.Insert, "Finalized", Exists("Parent", "p", Column("Id", "p").EqualTo(New("ParentId")).And(Column("Finalized", "p").EqualTo(Value(1))))));
        Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO aux.Child VALUES (1)"));
        p.DropGuardTrigger("guard", "Child");
        p.ExecuteNonQuery("INSERT INTO aux.Child VALUES (1)");
    }
    [TestCase(ProviderTypes.Ingres)]
    public void UnsupportedProvidersFailBeforeExecution(ProviderTypes type)
    {
        var builder = new MigrationBuilder();
        builder.Create.GuardTrigger(new("guard", "child", TriggerEvent.Insert, "Denied"));
        Assert.Throws<NotSupportedException>(() => builder.Preview(new SqlGenerationContext(type)));
    }
    [Test]
    public void InvalidBindingsAndMissingSqlServerKeysFailBeforeExecution()
    {
        var builder = new MigrationBuilder();
        builder.Create.GuardTrigger(new("guard", "child", TriggerEvent.Insert, "Denied", Old("Id").IsNull()));
        Assert.Throws<ArgumentException>(() => builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
        builder = new MigrationBuilder();
        builder.Create.GuardTrigger(new("guard", "child", TriggerEvent.Update, "Denied"));
        Assert.Throws<ArgumentException>(() => builder.Preview(new SqlGenerationContext(ProviderTypes.SqlServer)));
    }
}
