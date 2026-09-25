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

// Every case is assigned to exactly one existing CI database job. No catch-and-skip.
[TestFixture("SQLite", ProviderTypes.SQLite, Category = "SQLite")]
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
[NonParallelizable]
public class NamespaceLifecycleTests(string database, ProviderTypes providerType) : LiveProviderFixture(database, providerType)
{
    private string CurrentNamespace() => database switch
    {
        "SQLite" => "main",
        "SQLServer" => Convert.ToString(Provider.ExecuteScalar("SELECT SCHEMA_NAME()")),
        "PostgreSQL" => Convert.ToString(Provider.ExecuteScalar("SELECT current_schema()")),
        "Oracle" => Convert.ToString(Provider.ExecuteScalar("SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL")),
        "MySQL" or "MariaDB" => Convert.ToString(Provider.ExecuteScalar("SELECT DATABASE()")),
        "Db2" => Convert.ToString(Provider.ExecuteScalar("VALUES CURRENT SCHEMA")).Trim(),
        "Informix" => Convert.ToString(Provider.ExecuteScalar("SELECT USER FROM systables WHERE tabid=1")).Trim(),
        "Sybase" => Convert.ToString(Provider.ExecuteScalar("SELECT user_name()")),
        "Hana" => Convert.ToString(Provider.ExecuteScalar("SELECT CURRENT_SCHEMA FROM DUMMY")),
        _ => null
    };

    [TestCase("unqualified")]
    [TestCase("qualified")]
    [TestCase("quoted")]
    [TestCase("default")]
    public void TableColumnDataConstraintIndexAndRenameLifecycle(string mode)
    {
        var provider = (TransformationProvider)Provider;
        if (database == "Firebird" && mode != "unqualified")
        {
            if (mode == "default") provider.SetDefaultSchema("unsupported");
            Assert.Throws<NotSupportedException>(() => Provider.AddTable("unsupported.items", new Column("id", DbType.Int32)));
            Assert.Throws<NotSupportedException>(() => Provider.TableExists("unsupported.items"));
            Assert.Throws<NotSupportedException>(() => Provider.GetTables("unsupported").ToArray());
            return;
        }
        // Rebuilds own their transaction; SQLite cannot disable FK enforcement inside one.
        if (database == "SQLite") { Provider.Rollback(); Provider.ExecuteNonQuery("PRAGMA foreign_keys=ON"); }
        if (database == "Sybase") Provider.ExecuteNonQuery("SET QUOTED_IDENTIFIER ON");
        var ns = CurrentNamespace();
        if (mode == "default") provider.SetDefaultSchema(Provider.Dialect.QuoteIdentifier(ns));
        string Table(string name) => mode switch
        {
            "qualified" => ns + "." + name,
            "quoted" => Provider.Dialect.QuoteIdentifier(ns) + "." + Provider.Dialect.QuoteIdentifier(database is "Db2" or "Oracle" ? name.ToUpperInvariant() : name),
            _ => name
        };
        var parent = Table("ns_parent");
        var child = Table("ns_child");
        var renamed = Table("ns_renamed");
        try
        {
            Provider.AddTable(parent, new Column("id", DbType.Int32) { IsNullable = false }, new PrimaryKeyConstraint("pk_ns_parent", "id"));
            Provider.AddTable(child, new Column("id", DbType.Int32) { IsNullable = false }, new Column("parent_id", DbType.Int32),
                new Column("payload", DbType.String, 20) { IsNullable = false }, new PrimaryKeyConstraint("pk_ns_child", "id"));
            Assert.That(Provider.TableExists(child), Is.True);
            Assert.That(Provider.ColumnExists(child, "payload"), Is.True);
            Assert.That(Provider.GetTables(ns).Select(n => n.ToLowerInvariant()), Does.Contain("ns_child"));
            Assert.That(Provider.GetColumns(ns, database is "Db2" or "Oracle" ? "NS_CHILD" : "ns_child").Select(n => n.ToLowerInvariant()), Does.Contain("payload"));
            var view = Table("ns_view");
            Provider.ExecuteNonQuery($"CREATE VIEW {Provider.QuoteTableNameIfRequired(view)} AS SELECT {Provider.QuoteColumnNameIfRequired("id")} FROM {Provider.QuoteTableNameIfRequired(child)}");
            Assert.That(Provider.ViewExists(view), Is.True);
            Provider.ExecuteNonQuery("DROP VIEW " + Provider.QuoteTableNameIfRequired(view));
            Assert.That(Provider.ViewExists(view), Is.False);
            Provider.AddColumn(child, new Column("extra", DbType.Int32) { DefaultValue = 7 });
            Provider.Insert(parent, new[] { "id" }, new object[] { 1 });
            Provider.Insert(child, new[] { "id", "parent_id", "payload" }, new object[] { 1, 1, "kept" });
            Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT " + Provider.QuoteColumnNameIfRequired("extra") + " FROM " + Provider.QuoteTableNameIfRequired(child))), Is.EqualTo(7));
            Provider.Update(child, new[] { "payload" }, new object[] { "changed" }, new[] { "id" }, new object[] { 1 });
            Assert.That(Convert.ToString(Provider.ExecuteScalar("SELECT " + Provider.QuoteColumnNameIfRequired("payload") + " FROM " + Provider.QuoteTableNameIfRequired(child))), Is.EqualTo("changed"));
            Provider.Update(child, new[] { "payload" }, new object[] { "kept" });
            Provider.ChangeColumn(child, new Column("payload", DbType.String, 40) { IsNullable = false });
            Provider.RenameColumn(child, "payload", "message");
            Assert.That(Provider.ColumnExists(child, "message"), Is.True);
            Assert.That(Provider.ColumnExists(child, "payload"), Is.False);
            Provider.AddForeignKey("fk_ns_parent", child, new[] { "parent_id" }, parent, new[] { "id" });
            var fk = Provider.GetForeignKeyConstraints(child).Single();
            Assert.That(fk.ChildColumns.Select(c => c.ToLowerInvariant()), Is.EqualTo(new[] { "parent_id" }));
            Assert.That(fk.ParentColumns.Select(c => c.ToLowerInvariant()), Is.EqualTo(new[] { "id" }));
            Provider.AddUniqueConstraint("uq_ns_message", child, "message");
            Assert.That(Provider.ConstraintExists(child, "uq_ns_message"), Is.True);
            Provider.AddIndex(child, new DotNetProjects.Migrator.Framework.Index { Name = "ix_ns_extra", KeyColumns = new[] { "extra" } });
            Assert.That(Provider.IndexExists(child, "ix_ns_extra"), Is.True);
            Assert.That(Provider.GetIndexes(child).Any(i => i.Name.Equals("ix_ns_extra", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(Provider.GetTableConstraints(child).OfType<PrimaryKeyConstraint>(), Has.Exactly(1).Items);
            Provider.RemoveIndex(child, "ix_ns_extra");
            Assert.That(Provider.IndexExists(child, "ix_ns_extra"), Is.False);
            Provider.RemoveConstraint(child, "uq_ns_message");
            Provider.RemoveForeignKey(child, "fk_ns_parent");
            Assert.That(Provider.GetForeignKeyConstraints(child), Is.Empty);
            Provider.RemoveColumn(child, "extra");
            Assert.That(Provider.ColumnExists(child, "extra"), Is.False);
            if (database == "Firebird")
            {
                Assert.Throws<NotSupportedException>(() => Provider.RenameTable(child, renamed));
                // Release Firebird compiled dependencies after the preceding view/FK changes.
                Provider.Connection.Close();
                Provider.Connection.Open();
                Provider.RemoveTable(child);
                Assert.That(Provider.TableExists(child), Is.False);
                return;
            }
            Provider.RenameTable(child, renamed);
            Assert.That(Provider.TableExists(child), Is.False);
            Assert.That(Provider.TableExists(renamed), Is.True);
            Assert.That(Convert.ToString(Provider.ExecuteScalar("SELECT " + Provider.QuoteColumnNameIfRequired("message") + " FROM " + Provider.QuoteTableNameIfRequired(renamed))), Is.EqualTo("kept"));
            Provider.RemoveTable(renamed);
            Assert.That(Provider.TableExists(renamed), Is.False);
        }
        finally
        {
            // Firebird fixture drops its isolated database; cleanup must not mask a DDL error.
            foreach (var table in database == "Firebird" ? Array.Empty<string>() : new[] { renamed, child, parent })
                if (Provider.TableExists(table)) Provider.RemoveTable(table);
        }
    }
}
