using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;
using Sap.Data.Hana;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.Hana;

[TestFixture, Category("Hana"), NonParallelizable]
public class HanaProviderTests
{
    private HanaConnection connection;
    private string connectionString;
    private ITransformationProvider provider;
    private string schema;
    [SetUp]
    public void SetUp()
    {
        connectionString = Environment.GetEnvironmentVariable("MIGRATOR_HANA")
            ?? "Server=localhost:39041;UserID=SYSTEM;Password=MgT9ci7Q4xZ2";
        connection = new HanaConnection(connectionString);
        connection.Open();
        schema = "MIGRATOR_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE SCHEMA " + schema; command.ExecuteNonQuery();
        command.CommandText = "SET SCHEMA " + schema; command.ExecuteNonQuery();
        provider = ProviderFactory.Create(ProviderTypes.Hana, connection, schema, "hana-tests");
    }
    [TearDown]
    public void TearDown()
    {
        provider?.Dispose();
        if (connection?.State == ConnectionState.Open && schema != null)
        {
            using var command = connection.CreateCommand(); command.CommandText = "DROP SCHEMA " + schema + " CASCADE"; command.ExecuteNonQuery();
        }
        connection?.Dispose();
    }
    [Test]
    public void TimeOnlyDefaultsAndParametersAndQuotedConstraintsRoundTrip()
    {
        var time = new TimeOnly(12, 34, 56);
        provider.AddTable("ClockValues", new Column("Id", DbType.Int32), new Column("Moment", DbType.Time, time),
            new UniqueConstraint("UQ ' dotted.name", "Id"));
        provider.Insert("ClockValues", ["Id"], [1]);
        provider.Insert("ClockValues", ["Id", "Moment"], [2, time]);
        foreach (var id in new[] { 1, 2 })
        {
            var stored = provider.ExecuteScalar("SELECT \"Moment\" FROM \"ClockValues\" WHERE \"Id\"=" + id);
            var actual = stored is DateTime date ? TimeOnly.FromDateTime(date) : stored is TimeSpan span ? TimeOnly.FromTimeSpan(span) : TimeOnly.Parse(Convert.ToString(stored));
            Assert.That(actual, Is.EqualTo(time));
        }
        Assert.That(provider.GetColumns("ClockValues").Single(c => c.Name == "Moment").Type, Is.EqualTo(DbType.Time));
        provider.RemoveConstraint("ClockValues", "UQ ' dotted.name");
        provider.Insert("ClockValues", ["Id"], [1]);
    }

    [Test]
    public void ConnectionStringFactoryOpensAndDisposesOwnedConnection()
    {
        using var owned = ProviderFactory.Create(ProviderTypes.Hana, connectionString, schema);
        Assert.That(Convert.ToInt32(owned.ExecuteScalar("SELECT 1 FROM DUMMY")), Is.EqualTo(1));
    }

    [Test]
    public void ImperativeSchemaConstraintsMetadataAndPersistedData()
    {
        provider.AddTable("Items", new Column("Id", DbType.Int32) { IsIdentity = true },
            new Column("Label", DbType.String, 40) { DefaultValue = "initial" },
            new PrimaryKeyConstraint("PK_Items", "Id"), new UniqueConstraint("UQ_Label", "Label"),
            new CheckConstraint("CK_Label", "LENGTH(\"Label\") > 0"));
        provider.Insert("Items", ["Label"], ["one"]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Items\"")), Is.EqualTo(1));
        Assert.That(provider.GetColumns("Items").Single(c => c.Name == "Id").IsIdentity, Is.True);
        var constraints = provider.GetTableConstraints("Items");
        Assert.That(constraints.OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Id" }));
        Assert.That(constraints.OfType<UniqueConstraint>().Single().Name, Is.EqualTo("UQ_Label"));
        Assert.That(constraints.OfType<CheckConstraint>().Single().Name, Is.EqualTo("CK_Label"));
        Assert.Catch(() => provider.Insert("Items", ["Label"], ["one"]));
        Assert.Catch(() => provider.Insert("Items", ["Label"], [""]));
        Assert.That(provider.GetColumns(schema + ".Items").Select(c => c.Name), Is.EqualTo(new[] { "Id", "Label" }));
    }
    [Test]
    public void FluentAndPreviewCreateEquivalentSchemasAndRawDefaults()
    {
        MigrationBuilder Definition(string table)
        {
            var builder = new MigrationBuilder();
            builder.Create.Table(table).WithColumn("Id").AsInt32()
                .WithColumn("Created").AsDateTime().WithDefaultValue(RawSql.Insert("CURRENT_TIMESTAMP"))
                .WithPrimaryKey("PK_" + table, "Id");
            return builder;
        }
        provider.AddTable("Imperative", new Column("Id", DbType.Int32),
            new Column("Created", DbType.DateTime) { DefaultValue = RawSql.Insert("CURRENT_TIMESTAMP") },
            new PrimaryKeyConstraint("PK_Imperative", "Id"));
        Definition("Fluent").Apply(provider);
        foreach (var sql in Definition("Preview").Preview(new SqlGenerationContext(ProviderTypes.Hana))) provider.ExecuteNonQuery(sql.TrimEnd(';'));
        foreach (var table in new[] { "Imperative", "Fluent", "Preview" })
        {
            provider.Insert(table, ["Id"], [1]);
            Assert.That(provider.ExecuteScalar("SELECT \"Created\" FROM \"" + table + "\""), Is.TypeOf<DateTime>());
            Assert.That(provider.GetColumns(table).Single(c => c.Name == "Created").DefaultValue, Is.TypeOf<RawSql>());
            Assert.That(provider.GetTableConstraints(table).OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Id" }));
        }
    }
    [Test]
    public void AlterRenameAndIndexOperationsPreserveData()
    {
        provider.AddTable("Names", new Column("Id", DbType.Int32), new Column("Label", DbType.String, 20));
        provider.Insert("Names", ["Id", "Label"], [1, "kept"]);
        provider.AddColumn("Names", new Column("Extra", DbType.Int32) { DefaultValue = 7 });
        provider.ChangeColumn("Names", new Column("Label", DbType.String, 60));
        ((TransformationProvider)provider).AddColumnDefaultValue("Names", "Extra", 8);
        provider.Insert("Names", ["Id", "Label"], [2, "second"]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT \"Extra\" FROM \"Names\" WHERE \"Id\"=2")), Is.EqualTo(8));
        provider.RemoveColumnDefaultValue("Names", "Extra");
        provider.ChangeColumn("Names", new Column("Label", DbType.String, 60) { IsNullable = false });
        provider.ChangeColumn("Names", new Column("Label", DbType.String, 60) { IsNullable = true });
        provider.Insert("Names", ["Id"], [3]);
        Assert.That(provider.ExecuteScalar("SELECT \"Extra\" FROM \"Names\" WHERE \"Id\"=3"), Is.EqualTo(DBNull.Value));
        Assert.That(provider.GetColumns("Names").Single(c => c.Name == "Label").IsNullable, Is.True);
        provider.RenameColumn("Names", "Label", "Text");
        provider.RenameTable("Names", "Renamed");
        provider.AddIndex("Renamed", new Index { Name = "IX_Text", KeyColumns = ["Text"] });
        Assert.That(provider.IndexExists("Renamed", "IX_Text"), Is.True);
        Assert.That(provider.GetIndexes("Renamed").Single(i => i.Name == "IX_Text").KeyColumns, Is.EqualTo(new[] { "Text" }));
        Assert.That(provider.ExecuteScalar("SELECT \"Text\" FROM \"Renamed\" WHERE \"Id\"=1"), Is.EqualTo("kept"));
        provider.RemoveIndex("Renamed", "IX_Text");
        provider.RemoveColumn("Renamed", "Extra");
        Assert.That(provider.ColumnExists("Renamed", "Extra"), Is.False);
        provider.RemoveTable("Renamed");
        Assert.That(provider.TableExists("Renamed"), Is.False);
    }
    [Test]
    public void ForeignKeysPreservePairsAndIndependentActions()
    {
        provider.AddTable("Parents", new Column("A", DbType.Int32), new Column("B", DbType.Int32),
            new PrimaryKeyConstraint("PK_Parents", "B", "A"));
        provider.AddTable("Children", new Column("X", DbType.Int32), new Column("Y", DbType.Int32));
        ((IForeignKeyActions)provider).AddForeignKey("FK_Children", "Children", ["X", "Y"], "Parents", ["B", "A"],
            ForeignKeyConstraintType.Cascade, ForeignKeyConstraintType.Restrict);
        var key = provider.GetForeignKeyConstraints("Children").Single();
        Assert.That(key.ParentColumns, Is.EqualTo(new[] { "B", "A" }));
        Assert.That(key.ChildColumns, Is.EqualTo(new[] { "X", "Y" }));
        Assert.That(key.OnDelete, Is.EqualTo("CASCADE"));
        Assert.That(key.OnUpdate, Is.EqualTo("RESTRICT"));
        provider.Insert("Parents", ["A", "B"], [1, 2]);
        provider.Insert("Children", ["X", "Y"], [2, 1]);
        provider.Delete("Parents", ["A"], [1]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Children\"")), Is.Zero);
        provider.RemoveForeignKey("Children", "FK_Children");
        Assert.That(provider.GetForeignKeyConstraints("Children"), Is.Empty);
    }
    [Test]
    public void DataTransactionsRollbackAndCallerConnectionSurvives()
    {
        provider.AddTable("Numbers", new Column("Id", DbType.Int32));
        provider.Insert("Numbers", ["Id"], [1]);
        provider.BeginTransaction();
        provider.Insert("Numbers", ["Id"], [2]);
        provider.Rollback();
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(*) FROM \"Numbers\"")), Is.EqualTo(1));
        provider.Dispose();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }
    [Test]
    public void RunnerHistoryRestartDowngradeAndReadonlyPlan()
    {
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(HanaMigration));
        runner.DryRun = true;
        runner.MigrateToLastVersion();
        Assert.That(provider.TableExists(provider.SchemaInfoTable), Is.False);
        runner.DryRun = false;
        runner.MigrateToLastVersion();
        Assert.That(provider.TableExists("RunnerItems"), Is.True);
        var restarted = new DotNetProjects.Migrator.Migrator(provider, false, typeof(HanaMigration));
        restarted.MigrateToLastVersion();
        Assert.That(((IMigrationHistory)provider).ReadAppliedMigrations(), Is.EqualTo(new long[] { 1 }));
        restarted.MigrateTo(0);
        Assert.That(provider.TableExists("RunnerItems"), Is.False);
        Assert.That(((IMigrationHistory)provider).ReadAppliedMigrations(), Is.Empty);
    }
    [Test]
    public void UnsupportedCapabilitiesFailBeforeSchemaChanges()
    {
        Assert.Throws<NotSupportedException>(() => provider.AddTable("InvalidCollation",
            new Column("Name", DbType.String, 40) { Collation = Collation.CaseInsensitive }));
        Assert.That(provider.TableExists("InvalidCollation"), Is.False);
        Assert.Throws<NotSupportedException>(() => provider.CreateDatabases("unused"));
        var runner = new DotNetProjects.Migrator.Migrator(provider, false, typeof(HanaMigration));
        runner.Options.TransactionMode = MigrationTransactionMode.WholeSession;
        Assert.Catch(() => runner.MigrateToLastVersion());
        Assert.That(provider.TableExists("RunnerItems"), Is.False);
    }
    [Migration(1, Scope = "hana-tests", Ignore = true)]
    public class HanaMigration : Migration
    {
        public override void Up() => Database.AddTable("RunnerItems", new Column("Id", DbType.Int32), new PrimaryKeyConstraint("PK_RunnerItems", "Id"));
        public override void Down() => Database.RemoveTable("RunnerItems");
    }
}
