using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Framework.Models;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class FluentSchemaLifecycleTests
{
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    private sealed class InventoryMigration : FluentMigration
    {
        public override void BuildUp(MigrationBuilder migration)
        {
            if (Schema.Table("Inventory").Exists()) return;
            Assert.That(Context.Connection.State, Is.EqualTo(ConnectionState.Open));
            migration.Create.Table("Inventory")
                .WithColumn("Id").AsInt64().NotNullable()
                .WithColumn("Name").AsString(40).Nullable()
                .WithColumn("Active").AsBoolean().WithDefaultValue(true)
                .WithCheckConstraint("CK_Positive", "Id > 0");
            migration.Create.PrimaryKey("PK_Inventory").OnTable("Inventory").WithColumns("Id");
            migration.Create.UniqueConstraint("UQ_Name").OnTable("Inventory").WithColumns("Name");
            migration.Create.Index("IX_Active").OnTable("Inventory").WithColumns("Active");
        }
        public override void BuildDown(MigrationBuilder migration) => migration.Delete.Table("Inventory");
    }

    [Test]
    public void FluentMigrationAndInspectorTrackRealSchemaThroughUpAndDown()
    {
        var migration = new InventoryMigration { Database = provider };
        migration.Up(); migration.Up();
        var schema = new SchemaInspector(provider);
        var table = schema.Table("Inventory");
        Assert.That(schema.Tables(), Does.Contain("Inventory"));
        Assert.That(table.Exists(), Is.True);
        Assert.That(table.ColumnExists("Name"), Is.True);
        Assert.That(table.ConstraintExists("CK_Positive"), Is.True);
        Assert.That(table.PrimaryKeyExists("PK_Inventory"), Is.True);
        Assert.That(table.IndexExists("IX_Active"), Is.True);
        Assert.That(table.Constraints(), Does.Contain("CK_Positive").And.Contain("PK_Inventory").And.Contain("UQ_Name"));
        Assert.That(table.ConstraintDefinitions().OfType<CheckConstraint>().Single().CheckConstraintString, Does.Contain("Id > 0"));
        Assert.That(table.Indexes().Single(i => i.Name == "IX_Active").KeyColumns, Is.EqualTo(new[] { "Active" }));
        Assert.That(table.ForeignKeys(), Is.Empty);
        Assert.That(table.NullableContentSize("Name"), Is.Null);
        provider.Insert("Inventory", new[] { "Id", "Name" }, new object[] { 1, "sample" });
        Assert.That(table.ContentSize("Name"), Is.EqualTo(6));
        Assert.That(table.NullableContentSize("Name"), Is.EqualTo(6));
        Assert.That(schema.Scalar($"SELECT {schema.Concatenate("Name", "'!' ")} FROM Inventory"), Is.EqualTo("sample!"));
        Assert.That(schema.QuoteColumns("select", "two words"), Is.EqualTo(new[] { "\"select\"", "\"two words\"" }));
        Assert.That(schema.QuoteColumn("two words"), Is.EqualTo("\"two words\""));
        Assert.That(schema.QuoteTable("two words"), Is.EqualTo("\"two words\""));
        Assert.That(schema.ParameterName(3), Is.EqualTo("@p3"));
        migration.Down();
        Assert.That(table.Exists(), Is.False);
    }

    [TestCase(RemoveKind.ForeignKey)]
    [TestCase(RemoveKind.ForeignKeysForColumn)]
    [TestCase(RemoveKind.Constraint)]
    [TestCase(RemoveKind.PrimaryKey)]
    [TestCase(RemoveKind.Default)]
    [TestCase(RemoveKind.Index)]
    [TestCase(RemoveKind.AllIndexes)]
    [TestCase(RemoveKind.AllConstraints)]
    public void FluentRemovalChangesOnlyTheRequestedSchemaFeature(RemoveKind kind)
    {
        provider.ExecuteNonQuery("""
            CREATE TABLE Parent (Id INTEGER PRIMARY KEY); INSERT INTO Parent VALUES (1);
            CREATE TABLE Items (Id INTEGER, ParentId INTEGER, Value TEXT DEFAULT 'default',
                CONSTRAINT PK_Items PRIMARY KEY(Id), CONSTRAINT UQ_Value UNIQUE(Value),
                CONSTRAINT FK_Parent FOREIGN KEY(ParentId) REFERENCES Parent(Id));
            CREATE INDEX IX_Parent ON Items(ParentId);
            INSERT INTO Items VALUES (1, 1, 'keep');
            """);
        var builder = new MigrationBuilder();
        switch (kind)
        {
            case RemoveKind.ForeignKey: builder.Delete.ForeignKey("FK_Parent").FromTable("Items"); break;
            case RemoveKind.ForeignKeysForColumn: builder.Delete.ForeignKeysForColumn("ParentId").FromTable("Items"); break;
            case RemoveKind.Constraint: builder.Delete.Constraint("UQ_Value").FromTable("Items"); break;
            case RemoveKind.PrimaryKey: builder.Delete.PrimaryKey().FromTable("Items"); break;
            case RemoveKind.Default: builder.Delete.DefaultValue("Value").FromTable("Items"); break;
            case RemoveKind.Index: builder.Delete.Index("IX_Parent").FromTable("Items"); break;
            case RemoveKind.AllIndexes: builder.Delete.AllIndexes().FromTable("Items"); break;
            case RemoveKind.AllConstraints: builder.Delete.AllConstraints().FromTable("Items"); break;
        }
        builder.Apply(provider);
        Assert.That(provider.ExecuteScalar("SELECT Value FROM Items WHERE Id=1"), Is.EqualTo("keep"));
        var constraints = provider.GetConstraints("Items");
        Assert.That(constraints.Contains("FK_Parent"), Is.EqualTo(kind is not (RemoveKind.ForeignKey or RemoveKind.ForeignKeysForColumn or RemoveKind.AllConstraints)));
        Assert.That(constraints.Contains("PK_Items"), Is.EqualTo(kind is not (RemoveKind.PrimaryKey or RemoveKind.AllConstraints)));
        Assert.That(constraints.Contains("UQ_Value"), Is.EqualTo(kind is not (RemoveKind.Constraint or RemoveKind.AllConstraints)));
        Assert.That(provider.IndexExists("Items", "IX_Parent"), Is.EqualTo(kind is not (RemoveKind.Index or RemoveKind.AllIndexes)));
        provider.Insert("Items", new[] { "Id", "ParentId" }, new object[] { 2, 1 });
        Assert.That(provider.ExecuteScalar("SELECT Value FROM Items WHERE Id=2"), Is.EqualTo(kind == RemoveKind.Default ? DBNull.Value : "default"));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
    }

    [Test]
    public void AlterAndDeleteColumnPreserveDataAndHonorNewDefault()
    {
        provider.ExecuteNonQuery("CREATE TABLE Items (Id INTEGER, Value TEXT, Obsolete TEXT); INSERT INTO Items VALUES (1, 'keep', 'remove')");
        var definition = new Column("Value", DbType.String, 80, "new default");
        var builder = new MigrationBuilder();
        builder.Alter.Column(definition).OnTable("Items");
        builder.Delete.Column("Obsolete").FromTable("Items");
        definition.DefaultValue = "mutated";
        builder.Apply(provider);
        Assert.That(provider.ColumnExists("Items", "Obsolete"), Is.False);
        provider.Insert("Items", new[] { "Id" }, new object[] { 2 });
        Assert.That(provider.ExecuteStringQuery("SELECT Value FROM Items ORDER BY Id"), Is.EqualTo(new[] { "keep", "new default" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemoveForeignKeysForColumnHandlesIncomingAndOutgoingReferences(bool callerTransaction)
    {
        provider.ExecuteNonQuery("""
            CREATE TABLE Root (Id INTEGER PRIMARY KEY);
            CREATE TABLE Middle (Id INTEGER PRIMARY KEY REFERENCES Root(Id), Other INTEGER REFERENCES Root(Id));
            CREATE TABLE Leaf (Id INTEGER PRIMARY KEY, MiddleId INTEGER REFERENCES Middle(Id), RootId INTEGER REFERENCES Root(Id));
            INSERT INTO Root VALUES (1); INSERT INTO Middle VALUES (1, 1); INSERT INTO Leaf VALUES (1, 1, 1);
            """);
        if (callerTransaction) { provider.SetPragmaForeignKeys(false); provider.BeginTransaction(); }
        provider.RemoveAllForeignKeys("mIdDlE", "iD");
        Assert.That(provider.GetForeignKeyConstraints("Middle").Single().ChildColumns, Is.EqualTo(new[] { "Other" }));
        Assert.That(provider.GetForeignKeyConstraints("Leaf").Single().ParentTable, Is.EqualTo("Root"));
        Assert.That(provider.ExecuteScalar("SELECT MiddleId FROM Leaf"), Is.EqualTo(1L));
        Assert.That(provider.HasActiveTransaction, Is.EqualTo(callerTransaction));
        if (callerTransaction)
        {
            provider.Rollback();
            Assert.That(provider.GetForeignKeyConstraints("Middle"), Has.Length.EqualTo(2));
            Assert.That(provider.GetForeignKeyConstraints("Leaf"), Has.Length.EqualTo(2));
        }
        else Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
    }

    [Test]
    public void RemovingForeignKeysRollsBackAllTablesWhenOneCannotBeRebuilt()
    {
        provider.ExecuteNonQuery("""
            CREATE TABLE Parent (Id INTEGER PRIMARY KEY);
            CREATE TABLE FirstChild (Id INTEGER REFERENCES Parent(Id));
            CREATE TABLE SecondChild (Id INTEGER PRIMARY KEY REFERENCES Parent(Id)) WITHOUT ROWID;
            INSERT INTO Parent VALUES (1); INSERT INTO FirstChild VALUES (1); INSERT INTO SecondChild VALUES (1);
            """);
        Assert.Throws<NotSupportedException>(() => provider.RemoveAllForeignKeys("Parent", null));
        Assert.That(provider.GetForeignKeyConstraints("FirstChild"), Has.Length.EqualTo(1));
        Assert.That(provider.GetForeignKeyConstraints("SecondChild"), Has.Length.EqualTo(1));
        Assert.That(provider.ExecuteScalar("SELECT Id FROM FirstChild"), Is.EqualTo(1L));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
        Assert.That(provider.HasActiveTransaction, Is.False);
    }

    [Test]
    public void RemovingAllForeignKeysWithoutColumnAlsoRemovesIncomingReferences()
    {
        provider.ExecuteNonQuery("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE Child (Id INTEGER REFERENCES Parent(Id)); INSERT INTO Parent VALUES (1); INSERT INTO Child VALUES (1)");
        provider.RemoveAllForeignKeys("Parent", null);
        Assert.That(provider.GetForeignKeyConstraints("Child"), Is.Empty);
        provider.ExecuteNonQuery("INSERT INTO Child VALUES (999)");
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Child"), Is.EqualTo(2L));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
    }

    [Test]
    public void ForeignKeyRemovalRejectsUnsafeCallerTransactionBeforeChangingSchema()
    {
        provider.ExecuteNonQuery("CREATE TABLE Parent (Id INTEGER PRIMARY KEY); CREATE TABLE Child (Id INTEGER REFERENCES Parent(Id))");
        provider.BeginTransaction();
        Assert.Throws<MigrationException>(() => provider.RemoveAllForeignKeys("Parent", "Id"));
        Assert.That(provider.HasActiveTransaction, Is.True);
        Assert.That(provider.GetForeignKeyConstraints("Child"), Has.Length.EqualTo(1));
        provider.Rollback();
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
    }

    [Test]
    public void RemovingUnmatchedForeignKeysDoesNotStartATransaction()
    {
        provider.ExecuteNonQuery("CREATE TABLE Items (Id INTEGER)");
        provider.RemoveAllForeignKeys("Items", "Id");
        Assert.That(provider.HasActiveTransaction, Is.False);
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
        Assert.That(provider.TableExists("Items"), Is.True);
    }

    [Test]
    public void CopyUpdateAndTruncateOperateOnActualRows()
    {
        provider.ExecuteNonQuery("CREATE TABLE Source (Id INTEGER, Value TEXT); CREATE TABLE Target (Key INTEGER, Label TEXT); INSERT INTO Source VALUES (2, 'second'), (1, 'first')");
        var copy = new MigrationBuilder();
        copy.Execute.CopyDataFromTable("Source").ToTable("Target").WithColumns(new[] { "Id", "Value" }, new[] { "Key", "Label" });
        copy.Apply(provider);
        Assert.That(provider.ExecuteStringQuery("SELECT Label FROM Target ORDER BY Key"), Is.EqualTo(new[] { "first", "second" }));
        provider.ExecuteNonQuery("UPDATE Source SET Value='changed' WHERE Id=2");
        var update = new MigrationBuilder();
        update.Execute.UpdateTable("Target").FromTable("Source")
            .Set(new ColumnPair { ColumnNameSource = "Value", ColumnNameTarget = "Label" })
            .Match(new ColumnPair { ColumnNameSource = "Id", ColumnNameTarget = "Key" });
        update.Apply(provider);
        Assert.That(provider.ExecuteStringQuery("SELECT Label FROM Target ORDER BY Key"), Is.EqualTo(new[] { "first", "changed" }));
        var truncate = new MigrationBuilder(); truncate.Execute.Truncate("Target"); truncate.Apply(provider);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Target"), Is.EqualTo(0L));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Source"), Is.EqualTo(2L));
    }
}

public class FluentCallbackOwnershipTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void CommandCallbackOwnsCommandEvenOnFailure(bool fail)
    {
        var provider = Substitute.For<ITransformationProvider>();
        var command = Substitute.For<IDbCommand>(); provider.CreateCommand().Returns(command);
        var builder = new MigrationBuilder();
        builder.Execute.WithCommand(c =>
        {
            c.CommandText = "UPDATE Items SET Value=1";
            c.ExecuteNonQuery();
            if (fail) throw new InvalidOperationException("callback failed");
        });
        if (fail) Assert.Throws<InvalidOperationException>(() => builder.Apply(provider));
        else builder.Apply(provider);
        command.Received(1).ExecuteNonQuery();
        command.Received(1).Dispose();
    }

    [Test]
    public void ConnectionCallbackBorrowsConnection()
    {
        var provider = Substitute.For<ITransformationProvider>();
        var connection = Substitute.For<IDbConnection>(); provider.Connection.Returns(connection);
        var builder = new MigrationBuilder();
        builder.Execute.WithConnection(c => { Assert.That(c, Is.SameAs(connection)); throw new InvalidOperationException("callback failed"); });
        Assert.Throws<InvalidOperationException>(() => builder.Apply(provider));
        connection.DidNotReceive().Dispose();
        connection.DidNotReceive().Close();
    }
}
