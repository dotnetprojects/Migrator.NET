using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests;

[TestFixture(ProviderTypes.SQLite)]
[TestFixture(ProviderTypes.MonoSQLite)]
[Category("SQLite")]
public class SQLiteNamespaceTests(ProviderTypes type)
{
    [TestCase(false)]
    [TestCase(true)]
    public void AttachedDatabaseRebuildPreservesDataIndexesTriggersAndIdentity(bool useDefault)
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(type, connection, useDefault ? "\"sales.data\"" : null);
        provider.ExecuteNonQuery("""
            ATTACH ':memory:' AS "sales.data";
            CREATE TABLE main.items(id INTEGER PRIMARY KEY, wrong TEXT);
            INSERT INTO main.items VALUES(99,'untouched');
            CREATE TABLE "sales.data".audit(value TEXT);
            """);
        var table = useDefault ? "items" : "\"sales.data\".items";
        provider.AddTable(table, new Column("id", DbType.Int64) { IsIdentity = true },
            new Column("value", DbType.String, 20), new PrimaryKeyConstraint("pk_items", "id"));
        provider.Insert(table, new[] { "value" }, new object[] { "keep" });
        provider.Insert(table, new[] { "id", "value" }, new object[] { 100, "delete" });
        provider.ExecuteNonQuery("DELETE FROM \"sales.data\".items WHERE id=100");
        provider.AddIndex(table, new Index { Name = "ix_value", KeyColumns = new[] { "value" } });
        provider.ExecuteNonQuery("CREATE TRIGGER \"sales.data\".audit_items AFTER INSERT ON items BEGIN INSERT INTO audit VALUES(NEW.value); END");
        provider.ChangeColumn(table, new Column("value", DbType.String, 80));
        Assert.That(provider.IndexExists(table, "ix_value"), Is.True);
        provider.Insert(table, new[] { "value" }, new object[] { "after" });
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT MAX(id) FROM \"sales.data\".items")), Is.EqualTo(101));
        Assert.That(provider.ExecuteScalar("SELECT value FROM \"sales.data\".audit"), Is.EqualTo("after"));
        Assert.That(provider.ExecuteScalar("SELECT wrong FROM main.items"), Is.EqualTo("untouched"));
        Assert.That(provider.GetColumns("\"sales.data\"", "items"), Is.EqualTo(new[] { "id", "value" }));
        provider.RemoveIndex(table, "ix_value");
        provider.RenameTable(table, "renamed");
        Assert.That(provider.TableExists("\"sales.data\".renamed"), Is.True);
        Assert.That(provider.TableExists("main.items"), Is.True);
        provider.RemoveTable("\"sales.data\".renamed");
        Assert.That(provider.TableExists("main.items"), Is.True);
    }

    [Test]
    public void ForeignKeysUseTheirOwnDatabaseAndRejectCrossDatabaseReferencesBeforeDdl()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(type, connection, null);
        provider.ExecuteNonQuery("ATTACH ':memory:' AS aux; CREATE TABLE main.parent(id INTEGER PRIMARY KEY); CREATE TABLE aux.parent(id INTEGER PRIMARY KEY)");
        provider.AddTable("aux.child", new Column("pid", DbType.Int32),
            new DotNetProjects.Migrator.Framework.ForeignKeyConstraint("fk_parent", "aux.parent", new[] { "id" }, "aux.child", new[] { "pid" }));
        Assert.That(provider.GetForeignKeyConstraints("aux.child").Single().ParentTable, Is.EqualTo("parent"));
        Assert.Throws<NotSupportedException>(() => provider.AddTable("aux.invalid", new Column("pid", DbType.Int32),
            new DotNetProjects.Migrator.Framework.ForeignKeyConstraint("fk_cross", "main.parent", new[] { "id" }, "aux.invalid", new[] { "pid" })));
        Assert.That(provider.TableExists("aux.invalid"), Is.False);
    }
}
