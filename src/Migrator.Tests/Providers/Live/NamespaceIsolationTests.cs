using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;
using Sap.Data.Hana;

namespace Migrator.Tests.Providers.Live;

[TestFixture("SQLite", ProviderTypes.SQLite, Category = "SQLite")]
[TestFixture("SQLServer", ProviderTypes.SqlServer, Category = "SQLServer")]
[TestFixture("PostgreSQL", ProviderTypes.PostgreSQL, Category = "PostgreSQL")]
[TestFixture("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
[TestFixture("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
[TestFixture("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
[TestFixture("Hana", ProviderTypes.Hana, Category = "Hana")]
[NonParallelizable]
public class NamespaceIsolationTests(string database, ProviderTypes providerType) : LiveProviderFixture(database, providerType)
{
    [Test]
    public void SameNamedObjectsRemainIsolatedWhenDefaultPointsElsewhere()
    {
        var provider = (TransformationProvider)Provider;
        if (database == "SQLite") Provider.Rollback();
        var first = "NS_" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var second = first + "B";
        string Qualified(string schema, string table) => Provider.Dialect.QuoteIdentifier(schema) + "." + table;
        void Create(string schema)
        {
            var quoted = Provider.Dialect.QuoteIdentifier(schema);
            Provider.ExecuteNonQuery(database switch {
                "SQLite" => "ATTACH ':memory:' AS " + quoted,
                "MySQL" or "MariaDB" => "CREATE DATABASE " + quoted,
                _ => "CREATE SCHEMA " + quoted });
        }
        var created = new List<string>();
        try
        {
            Create(first); created.Add(first);
            Create(second); created.Add(second);
            provider.SetDefaultSchema(Provider.Dialect.QuoteIdentifier(second));
            foreach (var schema in created)
            {
                var table = Qualified(schema, "ns_items");
                Provider.AddTable(table, new Column("id", DbType.Int32) { IsNullable = false }, new Column("payload", DbType.String, 30),
                    new PrimaryKeyConstraint("pk_same", "id"));
                Provider.AddIndex(table, new DotNetProjects.Migrator.Framework.Index { Name = "ix_same", KeyColumns = new[] { "payload" } });
                Provider.Insert(table, new[] { "id", "payload" }, new object[] { 1, schema });
            }
            var target = Qualified(first, "ns_items");
            Assert.That(Provider.TableExists(target), Is.True);
            Assert.That(Provider.GetTables(Provider.Dialect.QuoteIdentifier(first)).Select(n => n.ToLowerInvariant()), Is.EqualTo(new[] { "ns_items" }));
            Assert.That(Convert.ToString(Provider.ExecuteScalar("SELECT " + Provider.QuoteColumnNameIfRequired("payload") + " FROM " + Provider.QuoteTableNameIfRequired("ns_items"))), Is.EqualTo(second));
            Provider.ChangeColumn(target, new Column("payload", DbType.String, 60));
            Provider.AddColumn(target, new Column("only_first", DbType.Int32));
            Assert.That(Provider.ColumnExists(target, "only_first"), Is.True);
            Assert.That(Provider.ColumnExists("ns_items", "only_first"), Is.False);
            Provider.RemoveColumn(target, "only_first");
            Assert.That(Provider.ColumnExists(target, "only_first"), Is.False);
            Provider.RemoveIndex(target, "ix_same");
            Assert.That(Provider.IndexExists(target, "ix_same"), Is.False);
            Assert.That(Provider.IndexExists("ns_items", "ix_same"), Is.True);
            // Db2 forbids renaming a column while an index depends on it.
            Provider.RenameColumn(target, "payload", "message");
            Assert.That(Provider.ColumnExists(target, "message"), Is.True);
            Assert.That(Provider.ColumnExists("ns_items", "message"), Is.False);
            Provider.RenameColumn(target, "message", "payload");
            Provider.RenameTable(target, "ns_renamed");
            Assert.That(Provider.TableExists(target), Is.False);
            Assert.That(Provider.TableExists(Qualified(first, "ns_renamed")), Is.True);
            Provider.RemoveTable(Qualified(first, "ns_renamed"));
            Assert.That(Provider.TableExists("ns_items"), Is.True);
            Assert.That(Convert.ToString(Provider.ExecuteScalar("SELECT " + Provider.QuoteColumnNameIfRequired("payload") + " FROM " + Provider.QuoteTableNameIfRequired("ns_items"))), Is.EqualTo(second));
        }
        finally
        {
            foreach (var schema in created.AsEnumerable().Reverse())
            {
                foreach (var table in new[] { "ns_renamed", "ns_items" })
                    if (Provider.TableExists(Qualified(schema, table))) Provider.RemoveTable(Qualified(schema, table));
                if (database != "SQLite")
                    Provider.ExecuteNonQuery((database is "MySQL" or "MariaDB" ? "DROP DATABASE " : "DROP SCHEMA ") +
                        Provider.Dialect.QuoteIdentifier(schema) + (database == "Db2" ? " RESTRICT" : ""));
            }
            provider.SetDefaultSchema(null);
        }
    }
}
