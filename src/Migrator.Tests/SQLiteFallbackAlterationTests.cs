using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteFallbackAlterationTests
{
    // Select the compatibility path, but execute every schema/data statement against SQLite.
    private sealed class LegacyProvider(IDbConnection connection)
        : SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null)
    {
        public bool FailParentRebuild { get; set; }
        public override object ExecuteScalar(string sql) => sql == "SELECT sqlite_version()" ? "3.25.0" : base.ExecuteScalar(sql);
        public override int ExecuteNonQuery(string sql)
        {
            if (FailParentRebuild && sql.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase) && sql.Contains("OriginalTemp"))
                throw new InvalidOperationException("Injected rebuild failure.");
            return base.ExecuteNonQuery(sql);
        }
    }

    private SqliteConnection connection;
    private LegacyProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        provider = new LegacyProvider(connection);
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [Test]
    public void RenamePreservesPrimaryUniqueAndForeignKeysIndexesAndRows()
    {
        provider.ExecuteNonQuery("""
            CREATE TABLE Lookup (Id INTEGER PRIMARY KEY);
            CREATE TABLE Parent (Old INTEGER, Value TEXT, CONSTRAINT PK_Parent PRIMARY KEY(Old),
                CONSTRAINT UQ_Parent UNIQUE(Old), CONSTRAINT FK_Lookup FOREIGN KEY(Old) REFERENCES Lookup(Id));
            CREATE INDEX IX_Parent ON Parent(Old);
            CREATE TABLE Child (Id INTEGER, ParentId INTEGER, LookupId INTEGER,
                CONSTRAINT FK_Parent FOREIGN KEY(ParentId) REFERENCES Parent(Old),
                CONSTRAINT FK_Other FOREIGN KEY(LookupId) REFERENCES Lookup(Id));
            INSERT INTO Lookup VALUES (1);
            INSERT INTO Parent VALUES (1, 'preserved');
            INSERT INTO Child VALUES (2, 1, 1);
            """);
        provider.RenameColumn("Parent", "Old", "Renamed");
        Assert.That(provider.ColumnExists("Parent", "Old"), Is.False);
        Assert.That(provider.ExecuteScalar("SELECT Value FROM Parent WHERE Renamed=1"), Is.EqualTo("preserved"));
        Assert.That(provider.GetTableConstraints("Parent").OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Renamed" }));
        Assert.That(provider.GetTableConstraints("Parent").OfType<DotNetProjects.Migrator.Framework.UniqueConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Renamed" }));
        Assert.That(provider.GetIndexes("Parent").Single(x => x.Name == "IX_Parent").KeyColumns, Is.EqualTo(new[] { "Renamed" }));
        Assert.That(provider.GetForeignKeyConstraints("Parent").Single().ChildColumns, Is.EqualTo(new[] { "Renamed" }));
        Assert.That(provider.GetForeignKeyConstraints("Child").Single(x => x.Name == "FK_Parent").ParentColumns, Is.EqualTo(new[] { "Renamed" }));
        Assert.That(provider.GetForeignKeyConstraints("Child").Single(x => x.Name == "FK_Other").ParentColumns, Is.EqualTo(new[] { "Id" }));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
        Assert.That(provider.GetTables(), Does.Not.Contain("ParentTemp").And.Not.Contain("ChildTemp"));
    }

    [TestCase("", "Old")]
    [TestCase("Existing", "Old")]
    [TestCase("New", "Missing")]
    public void InvalidRenamePreservesOriginalSchemaAndData(string newName, string oldName)
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Old INTEGER, Existing TEXT); INSERT INTO Original VALUES (7, 'keep')");
        Assert.Catch(() => provider.RenameColumn("Original", oldName, newName));
        Assert.That(provider.ExecuteScalar("SELECT Existing FROM Original WHERE Old=7"), Is.EqualTo("keep"));
        Assert.That(provider.GetTables(), Is.EquivalentTo(new[] { "Original" }));
    }

    [Test]
    public void RenameWithEnabledForeignKeysIsRejectedBeforeRebuild()
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Old INTEGER); INSERT INTO Original VALUES (7)");
        provider.SetPragmaForeignKeys(true);
        Assert.Catch(() => provider.RenameColumn("Original", "Old", "New"));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
        Assert.That(provider.ExecuteScalar("SELECT Old FROM Original"), Is.EqualTo(7L));
    }

    [Test]
    public void RenameWithTriggerIsRejectedWithoutDroppingTheTrigger()
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Old INTEGER); CREATE TABLE Audit (Id INTEGER); CREATE TRIGGER RecordInsert AFTER INSERT ON Original BEGIN INSERT INTO Audit VALUES (NEW.Old); END");
        Assert.Throws<NotSupportedException>(() => provider.RenameColumn("Original", "Old", "New"));
        provider.ExecuteNonQuery("INSERT INTO Original VALUES (7)");
        Assert.That(provider.ExecuteScalar("SELECT Id FROM Audit"), Is.EqualTo(7L));
    }

    [TestCase("CONSTRAINT CK_Value CHECK (Obsolete > 0)", "check constraint")]
    [TestCase("CONSTRAINT UQ_Value UNIQUE (Obsolete, Retained)", "composite unique")]
    [TestCase("CONSTRAINT PK_Value PRIMARY KEY (Obsolete, Retained)", "primary-key")]
    public void RemovingAConstrainedColumnRequiresExplicitConstraintRemoval(string constraint, string message)
    {
        provider.ExecuteNonQuery($"CREATE TABLE Original (Obsolete INTEGER, Retained INTEGER, {constraint}); INSERT INTO Original VALUES (1, 2)");
        var error = Assert.Catch(() => provider.RemoveColumn("Original", "Obsolete"));
        Assert.That(error.Message, Does.Contain(message));
        Assert.That(provider.ExecuteScalar("SELECT Retained FROM Original WHERE Obsolete=1"), Is.EqualTo(2L));
        Assert.That(provider.GetTables(), Is.EquivalentTo(new[] { "Original" }));
    }

    [Test]
    public void RemovingColumnWithCompositeIndexLeavesIndexAndRowsUntouched()
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Obsolete INTEGER, Retained INTEGER); CREATE INDEX IX_Both ON Original(Obsolete, Retained); INSERT INTO Original VALUES (1, 2)");
        Assert.That(Assert.Catch(() => provider.RemoveColumn("Original", "Obsolete")).Message, Does.Contain("composite index"));
        Assert.That(provider.GetIndexes("Original").Single().KeyColumns, Is.EqualTo(new[] { "Obsolete", "Retained" }));
        Assert.That(provider.ExecuteScalar("SELECT Retained FROM Original WHERE Obsolete=1"), Is.EqualTo(2L));
    }

    [Test]
    public void FailedRebuildRollsBackDataAndRestoresForeignKeySetting()
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER, Name TEXT); INSERT INTO Original VALUES (1, NULL)");
        provider.SetPragmaForeignKeys(true);
        Assert.Catch(() => provider.ChangeColumn("Original", new Column("Name", DbType.String) { IsNullable = false }));
        Assert.That(provider.IsPragmaForeignKeysOn(), Is.True);
        Assert.That(provider.HasActiveTransaction, Is.False);
        Assert.That(provider.GetTables(), Is.EquivalentTo(new[] { "Original" }));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Original WHERE Name IS NULL"), Is.EqualTo(1L));
        provider.Insert("Original", new[] { "Id", "Name" }, new object[] { 2, null });
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Original"), Is.EqualTo(2L));
    }

    [Test]
    public void FailureRebuildingDependentTableRollsBackTheParentRename()
    {
        provider.ExecuteNonQuery("""
            CREATE TABLE Parent (Old INTEGER PRIMARY KEY);
            CREATE TABLE Child (Id INTEGER PRIMARY KEY, ParentId INTEGER REFERENCES Parent(Old)) WITHOUT ROWID;
            INSERT INTO Parent VALUES (1); INSERT INTO Child VALUES (2, 1);
            """);
        Assert.Throws<NotSupportedException>(() => provider.RenameColumn("Parent", "Old", "New"));
        Assert.That(provider.HasActiveTransaction, Is.False);
        Assert.That(provider.ColumnExists("Parent", "Old"), Is.True);
        Assert.That(provider.ColumnExists("Parent", "New"), Is.False);
        Assert.That(provider.ExecuteScalar("SELECT Old FROM Parent"), Is.EqualTo(1L));
        Assert.That(provider.ExecuteScalar("SELECT ParentId FROM Child"), Is.EqualTo(1L));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
        Assert.That(provider.GetTables(), Is.EquivalentTo(new[] { "Parent", "Child" }));
    }

    [Test]
    public void RenameInsideCallerTransactionCanBeRolledBackByTheCaller()
    {
        provider.ExecuteNonQuery("CREATE TABLE Parent (Old INTEGER PRIMARY KEY); CREATE TABLE Child (Id INTEGER REFERENCES Parent(Old)); INSERT INTO Parent VALUES (1); INSERT INTO Child VALUES (1)");
        provider.BeginTransaction();
        provider.RenameColumn("Parent", "Old", "New");
        Assert.That(provider.HasActiveTransaction, Is.True);
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
        provider.Rollback();
        Assert.That(provider.ExecuteScalar("SELECT Old FROM Parent"), Is.EqualTo(1L));
        Assert.That(provider.GetForeignKeyConstraints("Child").Single().ParentColumns, Is.EqualTo(new[] { "Old" }));
    }

    [Test]
    public void RenameUpdatesSelfReferencingParentColumns()
    {
        provider.ExecuteNonQuery("CREATE TABLE Nodes (Old INTEGER PRIMARY KEY, ParentId INTEGER REFERENCES Nodes(Old)); INSERT INTO Nodes VALUES (1, NULL), (2, 1)");
        provider.RenameColumn("Nodes", "Old", "Id");
        Assert.That(provider.GetForeignKeyConstraints("Nodes").Single().ParentColumns, Is.EqualTo(new[] { "Id" }));
        Assert.That(provider.ExecuteScalar("SELECT ParentId FROM Nodes WHERE Id=2"), Is.EqualTo(1L));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
    }

    [TestCase("Obsolete")]
    [TestCase("obsolete")]
    public void RemoveColumnUpdatesIncomingKeysCaseInsensitively(string name)
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER PRIMARY KEY, Obsolete INTEGER UNIQUE); CREATE TABLE Child (Value INTEGER REFERENCES Original(Obsolete)); INSERT INTO Original VALUES (1, 7); INSERT INTO Child VALUES (7)");
        provider.RemoveColumn("Original", name);
        Assert.That(provider.ColumnExists("Original", "Obsolete"), Is.False);
        Assert.That(provider.GetForeignKeyConstraints("Child"), Is.Empty);
        Assert.That(provider.ExecuteScalar("SELECT Value FROM Child"), Is.EqualTo(7L));
        Assert.That(provider.ExecuteScalar("SELECT Id FROM Original"), Is.EqualTo(1L));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailureRemovingColumnPreservesEarlierDependentTables(bool callerTransaction)
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER PRIMARY KEY, Obsolete INTEGER UNIQUE); CREATE TABLE Child (Value INTEGER REFERENCES Original(Obsolete)); INSERT INTO Original VALUES (1, 7); INSERT INTO Child VALUES (7)");
        if (callerTransaction) provider.BeginTransaction();
        provider.FailParentRebuild = true;
        Assert.That(Assert.Throws<InvalidOperationException>(() => provider.RemoveColumn("Original", "Obsolete")).Message, Is.EqualTo("Injected rebuild failure."));
        Assert.That(provider.HasActiveTransaction, Is.EqualTo(callerTransaction));
        if (callerTransaction) provider.Rollback();
        Assert.That(provider.GetForeignKeyConstraints("Child"), Has.Length.EqualTo(1));
        Assert.That(provider.ExecuteScalar("SELECT Obsolete FROM Original"), Is.EqualTo(7L));
        Assert.That(provider.ExecuteScalar("SELECT Value FROM Child"), Is.EqualTo(7L));
        Assert.That(provider.GetTables(), Is.EquivalentTo(new[] { "Original", "Child" }));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
    }

    [Test]
    public void RemovingSelfReferencedColumnPreservesOtherValues()
    {
        provider.ExecuteNonQuery("CREATE TABLE Nodes (Id INTEGER PRIMARY KEY, Obsolete INTEGER UNIQUE, ParentValue INTEGER REFERENCES Nodes(Obsolete)); INSERT INTO Nodes VALUES (1, 7, NULL), (2, 8, 7)");
        provider.RemoveColumn("Nodes", "obsolete");
        Assert.That(provider.GetForeignKeyConstraints("Nodes"), Is.Empty);
        Assert.That(provider.ExecuteScalar("SELECT ParentValue FROM Nodes WHERE Id=2"), Is.EqualTo(7L));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
    }

    [Test]
    public void SuccessfulRemovalRemainsInTheCallerTransaction()
    {
        provider.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER PRIMARY KEY, Obsolete INTEGER UNIQUE); CREATE TABLE Child (Value INTEGER REFERENCES Original(Obsolete)); INSERT INTO Original VALUES (1, 7); INSERT INTO Child VALUES (7)");
        provider.BeginTransaction();
        provider.RemoveColumn("Original", "Obsolete");
        Assert.That(provider.HasActiveTransaction, Is.True);
        Assert.That(provider.GetForeignKeyConstraints("Child"), Is.Empty);
        provider.Rollback();
        Assert.That(provider.GetForeignKeyConstraints("Child"), Has.Length.EqualTo(1));
        Assert.That(provider.ExecuteScalar("SELECT Obsolete FROM Original"), Is.EqualTo(7L));
    }
}
