using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using FirebirdSql.Data.FirebirdClient;
using MySql.Data.MySqlClient;
using NUnit.Framework;
using ProviderFactories = DotNetProjects.Migrator.Providers.DbProviderFactories;
using DbIndex = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.Live;

// A fresh database/schema per test also isolates engines with auto-committing DDL.
[TestFixture("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
[TestFixture("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
[TestFixture("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
[TestFixture("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
[TestFixture("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
[TestFixture("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
[NonParallelizable]
public class LiveDatabaseTests(string database, ProviderTypes providerType)
{
    private ITransformationProvider provider;
    internal ITransformationProvider Provider => provider;

    internal void RunRegression(Action<LiveDatabaseTests> action)
    {
        try { SetUp(); action(this); }
        finally { TearDown(); }
    }

    internal void DropCreatedDatabase()
    {
        provider.DropDatabases(provider.GetDatabases().Single());
        created = false;
        provider.Dispose();
        provider = null;
        using var connection = new FbConnection(connectionString);
        Assert.Catch<DbException>(() => connection.Open());
    }
    private DbConnection admin;
    private string connectionString;
    private string isolatedName;
    private bool created;

    private static DbProviderFactory LoadFactory(string assemblyName)
    {
        var type = Assembly.Load(assemblyName).GetTypes()
            .Single(t => !t.IsAbstract && typeof(DbProviderFactory).IsAssignableFrom(t));
        return (DbProviderFactory)(type.GetField("Instance")?.GetValue(null)
            ?? type.GetProperty("Instance")?.GetValue(null)
            ?? Activator.CreateInstance(type));
    }

    [SetUp]
    public void SetUp()
    {
        isolatedName = "m" + Guid.NewGuid().ToString("N")[..12];
        string invariant;
        DbProviderFactory factory;
        var configured = Environment.GetEnvironmentVariable("MIGRATOR_" + database.ToUpperInvariant());
        switch (database)
        {
            case "MySQL":
            case "MariaDB":
                invariant = "MySql.Data.MySqlClient";
                factory = MySqlClientFactory.Instance;
                connectionString = configured ?? "Server=127.0.0.1;Database=testdb;User ID=root;Password=rootpass;Pooling=false";
                break;
            case "Firebird":
                invariant = "FirebirdSql.Data.FirebirdClient";
                factory = FirebirdClientFactory.Instance;
                connectionString = configured ?? "DataSource=localhost;Database=/var/lib/firebird/data/test.fdb;User=SYSDBA;Password=masterkey;Pooling=false";
                break;
            case "Db2":
                invariant = "IBM.Data.DB2";
                factory = LoadFactory("IBM.Data.Db2");
                connectionString = configured ?? "Server=localhost:50000;Database=testdb;UID=db2inst1;PWD=testpass;Pooling=false";
                break;
            case "Informix":
                invariant = "IBM.Data.Informix.Client";
                factory = LoadFactory("Informix.Net.Core");
                connectionString = configured ?? "Host=localhost;Service=9088;Server=informix;Database=testdb;User ID=informix;Password=in4mix;Protocol=onsoctcp;Pooling=false";
                break;
            case "Sybase":
                invariant = "Sybase.Data.AseClient";
                factory = LoadFactory("AdoNetCore.AseClient");
                connectionString = configured ?? "Data Source=localhost;Port=5000;Database=master;Uid=sa;Pwd=myPassword;Pooling=false";
                break;
            default: throw new InvalidOperationException(database);
        }

        ProviderFactories.RegisterFactory(invariant, () => factory);
        admin = factory.CreateConnection();
        var adminBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        // Informix CREATE DATABASE needs a server-only connection, not an open database.
        if (database == "Informix") adminBuilder["Database"] = "";
        admin.ConnectionString = adminBuilder.ConnectionString;
        admin.Open();
        if (database == "Firebird")
        {
            var builder = new FbConnectionStringBuilder(connectionString);
            var slash = builder.Database.LastIndexOf('/');
            builder.Database = builder.Database[..(slash + 1)] + isolatedName + ".fdb";
            connectionString = builder.ConnectionString;
            FbConnection.CreateDatabase(connectionString);
        }
        else if (database == "Db2")
        {
            ExecuteAdmin("CREATE SCHEMA " + isolatedName);
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            builder["CurrentSchema"] = isolatedName.ToUpperInvariant();
            connectionString = builder.ConnectionString;
        }
        else
        {
            ExecuteAdmin("CREATE DATABASE " + isolatedName + (database == "Informix" ? " WITH LOG" : database == "Sybase" ? " ON migrator_data = 32 LOG ON migrator_log = 16" : ""));
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            builder["Database"] = isolatedName;
            connectionString = builder.ConnectionString;
        }
        created = true;
        if (database == "Informix")
        {
            // CREATE DATABASE implicitly selects it; release that attachment for teardown.
            admin.Close();
            admin.Open();
        }
        if (database == "Sybase")
        {
            ExecuteAdmin("EXEC sp_dboption " + isolatedName + ", 'ddl in tran', true");
            ExecuteAdmin("EXEC sp_dboption " + isolatedName + ", 'full logging for alter table', true");
        }
        provider = ProviderFactory.Create(providerType, connectionString, null);
        if (database == "Db2") provider.ExecuteNonQuery("SET CURRENT SCHEMA " + isolatedName);
        if (database == "Sybase") provider.ExecuteNonQuery("CHECKPOINT");
    }

    private void ExecuteAdmin(string sql)
    {
        using var command = admin.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            // Db2 DROP SCHEMA RESTRICT requires its objects to be removed first.
            if (database == "Db2" && provider != null)
            {
                var tables = provider.GetTables();
                foreach (var table in tables.OrderBy(t => t.Equals("children", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
                    provider.RemoveTable(table);
            }
        }
        finally
        {
            provider?.Dispose();
            provider = null;
            try
            {
                if (created)
                {
                    if (database == "Sybase")
                    {
                        // The managed ASE client closes its socket asynchronously. Wait only
                        // for our isolated database's sessions to detach before dropping it.
                        using var command = admin.CreateCommand();
                        command.CommandText = "SELECT COUNT(*) FROM master..sysprocesses WHERE dbid=db_id('" + isolatedName + "')";
                        var deadline = DateTime.UtcNow.AddSeconds(10);
                        while (Convert.ToInt32(command.ExecuteScalar()) != 0)
                        {
                            if (DateTime.UtcNow >= deadline) throw new TimeoutException("ASE sessions did not detach from " + isolatedName);
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                    if (database == "Firebird") FbConnection.DropDatabase(connectionString);
                    else ExecuteAdmin("DROP " + (database == "Db2" ? "SCHEMA " + isolatedName + " RESTRICT" : "DATABASE " + isolatedName));
                }
            }
            finally
            {
                admin?.Dispose();
                admin = null;
                created = false;
            }
        }
    }

    internal void AssertDatabaseError(TestDelegate action)
    {
        // AdoNetCore's ASE exception predates DbException inheritance.
        var error = Assert.Catch(action);
        Assert.That(error is DbException || (database == "Sybase" && error.GetType().FullName == "AdoNetCore.AseClient.AseException"), Is.True, error?.ToString());
    }

    private void CreateItems() => provider.AddTable("items",
        new Column("id",DbType.Int32){IsNullable = false},
        new Column("label",DbType.String,40),
        new Column("amount", DbType.Int32) { DefaultValue = 7, IsNullable = false},new PrimaryKeyConstraint("PK_" + "items", "id"));

    [Test]
    public void TableAndColumnMetadata()
    {
        Assert.That(provider.TableExists("items"), Is.False);
        Assert.That(provider.ColumnExists("items", "id"), Is.False);
        CreateItems();
        Assert.That(provider.TableExists("items"), Is.True);
        Assert.That(provider.GetTables(), Has.Some.EqualTo("items").IgnoreCase);
        var columns = provider.GetColumns("items");
        Assert.That(columns, Has.Length.EqualTo(3));
        Assert.That(columns.Single(c => c.Name.Equals("id", StringComparison.OrdinalIgnoreCase)).Type, Is.EqualTo(DbType.Int32));
        Assert.That(columns.Single(c => c.Name.Equals("label", StringComparison.OrdinalIgnoreCase)).IsNullable, Is.True);
        Assert.That(columns.Single(c => c.Name.Equals("amount", StringComparison.OrdinalIgnoreCase)).IsNullable, Is.False);
        provider.RemoveTable("items");
        Assert.That(provider.TableExists("items"), Is.False);
    }

    [Test]
    public void DatabaseAndViewCatalogs()
    {
        Assert.That(provider.GetDatabases(), Is.Not.Empty);
        Assert.That(provider.ViewExists("item_view"), Is.False);
        CreateItems();
        provider.ExecuteNonQuery("CREATE VIEW item_view AS SELECT id FROM items");
        Assert.That(provider.ViewExists("item_view"), Is.True);
        Assert.That(provider.TableExists("item_view"), Is.False);
        provider.ExecuteNonQuery("DROP VIEW item_view");
        Assert.That(provider.ViewExists("item_view"), Is.False);
    }

    [Test]
    public void DataDefaultsAndPersistence()
    {
        CreateItems();
        provider.Insert("items", ["id", "label"], [1, "O'Brien"]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT amount FROM items WHERE id=1")), Is.EqualTo(7));
        provider.Dispose();
        provider = ProviderFactory.Create(providerType, connectionString, null);
        if (database == "Db2") provider.ExecuteNonQuery("SET CURRENT SCHEMA " + isolatedName);
        if (database == "Sybase") provider.ExecuteNonQuery("CHECKPOINT");
        Assert.That(provider.ExecuteScalar("SELECT label FROM items WHERE id=1"), Is.EqualTo("O'Brien"));
        provider.Update("items", ["label"], ["changed"], "id=1");
        Assert.That(provider.ExecuteScalar("SELECT label FROM items WHERE id=1"), Is.EqualTo("changed"));
        provider.Delete("items", ["id"], [1]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(*) FROM items")), Is.Zero);
    }

    [Test]
    public void AddRenameChangeAndDropColumn()
    {
        CreateItems();
        provider.AddColumn("items", new Column("extra",DbType.String,20));
        provider.RenameColumn("items", "extra", "renamed");
        provider.ChangeColumn("items", new Column("renamed",DbType.String,80,"fallback"){IsNullable = false});
        var changed = provider.GetColumns("items").Single(c => c.Name.Equals("renamed", StringComparison.OrdinalIgnoreCase));
        Assert.That(changed.Size, Is.EqualTo(80));
        Assert.That(changed.IsNullable, Is.False);
        provider.Insert("items", ["id"], [1]);
        Assert.That(provider.ExecuteScalar("SELECT renamed FROM items"), Is.EqualTo("fallback"));
        provider.RemoveColumnDefaultValue("items", "renamed");
        AssertDatabaseError(() => provider.Insert("items", ["id"], [2]));
        provider.RemoveColumn("items", "renamed");
        Assert.That(provider.ColumnExists("items", "renamed"), Is.False);
    }

    [Test]
    public void PrimaryKeyAndIdentity()
    {
        provider.AddTable("items",
            new Column("id",DbType.Int32){IsNullable = false,IsIdentity = true},
            new Column("label", DbType.String, 40),new PrimaryKeyConstraint("PK_" + "items", "id"));
        provider.Insert("items", ["label"], ["first"]);
        provider.Insert("items", ["label"], ["second"]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(DISTINCT id) FROM items")), Is.EqualTo(2));
        Assert.That(provider.GetColumns("items").Single(c => c.Name.Equals("id", StringComparison.OrdinalIgnoreCase)).IsIdentity, Is.True);
    }

    [Test]
    public void NamedPrimaryKey()
    {
        provider.AddTable("items", new Column("id",DbType.Int32){IsNullable = false});
        provider.AddPrimaryKey("pk_items", "items", "id");
        Assert.That(provider.PrimaryKeyExists("items", "pk_items"), Is.True);
        provider.Insert("items", ["id"], [1]);
        AssertDatabaseError(() => provider.Insert("items", ["id"], [1]));
        provider.RemovePrimaryKey("items");
        Assert.That(provider.PrimaryKeyExists("items", "pk_items"), Is.False);
        provider.Insert("items", ["id"], [1]);
    }

    [Test]
    public void ForeignKeyIsEnforcedAndRemoved()
    {
        CreateItems();
        provider.AddTable("children", new Column("parentid", DbType.Int32));
        provider.AddForeignKey("fk_children", "children", "parentid", "items", "id");
        Assert.That(provider.ConstraintExists("children", "fk_children"), Is.True);
        AssertDatabaseError(() => provider.Insert("children", ["parentid"], [99]));
        provider.Insert("items", ["id"], [1]);
        provider.Insert("children", ["parentid"], [1]);
        provider.RemoveForeignKey("children", "fk_children");
        Assert.That(provider.ConstraintExists("children", "fk_children"), Is.False);
        provider.Insert("children", ["parentid"], [99]);
    }

    [Test]
    public void UniqueAndCheckConstraints()
    {
        CreateItems();
        // Db2 requires NOT NULL for columns participating in a UNIQUE constraint.
        provider.ChangeColumn("items", new Column("label",DbType.String,40){IsNullable = false});
        provider.AddUniqueConstraint("uq_label", "items", "label");
        provider.AddCheckConstraint("ck_amount", "items", "amount >= 0");
        Assert.That(provider.ConstraintExists("items", "uq_label"), Is.True);
        Assert.That(provider.ConstraintExists("items", "ck_amount"), Is.True);
        provider.Insert("items", ["id", "label"], [1, "unique"]);
        AssertDatabaseError(() => provider.Insert("items", ["id", "label"], [2, "unique"]));
        AssertDatabaseError(() => provider.Insert("items", ["id", "label", "amount"], [3, "negative", -1]));
        provider.RemoveConstraint("items", "ck_amount");
        provider.RemoveConstraint("items", "uq_label");
        provider.Insert("items", ["id", "label", "amount"], [2, "unique", -1]);
    }

    [Test]
    public void CompositeIndexMetadataAndRemoval()
    {
        CreateItems();
        provider.AddIndex("items", new DbIndex { Name = "ix_items", KeyColumns = ["amount", "label"], Unique = true });
        Assert.That(provider.IndexExists("items", "ix_items"), Is.True);
        var index = provider.GetIndexes("items").Single(i => i.Name.Equals("ix_items", StringComparison.OrdinalIgnoreCase));
        Assert.That(index.KeyColumns.Select(c => c.ToLowerInvariant()), Is.EqualTo(new[] { "amount", "label" }));
        Assert.That(index.Unique, Is.True);
        provider.RemoveIndex("items", "ix_items");
        Assert.That(provider.IndexExists("items", "ix_items"), Is.False);
    }

    [Test]
    public void MigrateUpDownAndRepeat()
    {
        for (var cycle = 0; cycle < 2; cycle++)
        {
            var migrator = new DotNetProjects.Migrator.Migrator(provider, false, typeof(LiveMigration));
            migrator.MigrateToLastVersion();
            Assert.That(provider.TableExists("migration_items"), Is.True);
            Assert.That(provider.AppliedMigrations, Does.Contain(987654L));
            migrator.MigrateTo(0);
            Assert.That(provider.TableExists("migration_items"), Is.False);
            Assert.That(provider.AppliedMigrations, Is.Empty);
        }
    }

    [Migration(987654)]
    public class LiveMigration : Migration
    {
        public override void Up() => Database.AddTable("migration_items", new Column("id", DbType.Int32));
        public override void Down() => Database.RemoveTable("migration_items");
    }
}
