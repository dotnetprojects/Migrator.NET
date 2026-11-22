using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteRebuildDependencyTests
{
    [TestCase("Account")]
    [TestCase("'Account'")]
    [TestCase("main.'Account'")]
    [TestCase("'main'.'Account'")]
    public void ExternalTriggerRelationReferencesPreventBreakingColumnRemoval(string relation)
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=False"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,42); CREATE TABLE Other(Id INTEGER); CREATE TABLE Audit(Value INTEGER)");
        p.ExecuteNonQuery("CREATE TRIGGER TrackOther AFTER INSERT ON Other BEGIN INSERT INTO Audit(Value) SELECT Obsolete FROM " + relation + "; END");
        Assert.Catch(() => p.RemoveColumn("Account", "Obsolete"));
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.True);
        p.Insert("Other", new[] { "Id" }, new[] { "1" });
        Assert.That(p.ExecuteScalar("SELECT Value FROM Audit"), Is.EqualTo(42L));
    }
    [TestCase("SELECT Obsolete FROM AccountProjection")]
    [TestCase("SELECT Obsolete FROM 'Account'")]
    public void IndirectAndQuotedViewDependenciesPreventBreakingColumnRemoval(string report)
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=False"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,42); CREATE VIEW AccountProjection AS SELECT * FROM Account; CREATE VIEW Report AS " + report);
        Assert.Catch(() => p.RemoveColumn("Account", "Obsolete"));
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT Obsolete FROM Report"), Is.EqualTo(42L));
    }
    [Test]
    public void PreExistingBrokenViewsDoNotBlockAnUnrelatedRebuild()
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=False"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,42); CREATE VIEW Healthy AS SELECT Id FROM Account; CREATE VIEW Broken AS SELECT Missing FROM MissingTable");
        p.RemoveColumn("Account", "Obsolete");
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Id FROM Healthy"), Is.EqualTo(1L));
        Assert.Catch(() => p.ExecuteScalar("SELECT * FROM Broken"));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void TemporaryTriggersKeepTheirTargetDatabaseAfterRebuildOrRollback(bool invalidCheck)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("""
            ATTACH DATABASE ':memory:' AS aux;
            CREATE TABLE main.Parent(Id INTEGER, Qty INTEGER); INSERT INTO main.Parent VALUES(1,1);
            CREATE TABLE aux.Parent(Id INTEGER, Qty INTEGER); INSERT INTO aux.Parent VALUES(2,1);
            CREATE TABLE main.Touches(Id INTEGER);
            CREATE VIEW main.ParentView AS SELECT Qty FROM Parent;
            CREATE TEMP TRIGGER TrackMain AFTER UPDATE ON main.Parent BEGIN INSERT INTO Touches VALUES(NEW.Id); END;
            CREATE TEMP TRIGGER TrackAux AFTER UPDATE ON aux.Parent BEGIN INSERT INTO Touches VALUES(NEW.Id); END;
            """);
        if (invalidCheck) Assert.Catch(() => p.AddCheckConstraint("ck", "main.Parent", "Qty>100"));
        else p.AddCheckConstraint("ck", "main.Parent", "Qty>0");
        p.ExecuteNonQuery("UPDATE main.Parent SET Qty=2; UPDATE aux.Parent SET Qty=2");
        Assert.That(p.ExecuteScalar("SELECT SUM(Id) FROM main.Touches"), Is.EqualTo(3L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM temp.sqlite_master WHERE type='trigger'"), Is.EqualTo(2L));
        Assert.That(p.ExecuteScalar("SELECT Qty FROM ParentView"), Is.EqualTo(2L));
    }
    [Test]
    public void ReferencedViewColumnRemovalRollsBackWithoutTriggers()
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=False"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Parent(Id INTEGER, Payload TEXT); INSERT INTO Parent VALUES(1,'kept'); CREATE VIEW Dependent AS SELECT Payload FROM Parent");
        Assert.Catch(() => p.RemoveColumn("Parent", "Payload"));
        Assert.That(p.ColumnExists("Parent", "Payload"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT Payload FROM Dependent"), Is.EqualTo("kept"));
    }
    [Test]
    public void UnrelatedViewColumnNameDoesNotBlockColumnRemoval()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); CREATE TABLE Other(Account INTEGER); INSERT INTO Other VALUES(42); CREATE VIEW Unrelated AS SELECT Account FROM Other");
        p.SetPragmaForeignKeys(false);
        p.RemoveColumn("Account", "Obsolete");
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Account FROM Unrelated"), Is.EqualTo(42L));
    }
    private static void Seed(SQLiteTransformationProvider p)
    {
        p.ExecuteNonQuery("""
            CREATE TABLE Parent (Id INTEGER PRIMARY KEY AUTOINCREMENT, K1 INTEGER NOT NULL, K2 INTEGER NOT NULL,
                Data BLOB, Payload TEXT DEFAULT '[]', Archived INTEGER DEFAULT 0, Qty INTEGER DEFAULT 1,
                CONSTRAINT UQ_Parent UNIQUE(K1,K2));
            CREATE UNIQUE INDEX UX_Active ON Parent(K1) WHERE Archived=0;
            INSERT INTO Parent (Id,K1,K2,Data,Archived) VALUES (1,1,1,X'00FF',0),(2,1,2,NULL,1),(100,100,100,NULL,1);
            DELETE FROM Parent WHERE Id=100;
            CREATE TABLE Child (First INTEGER, Second INTEGER, FOREIGN KEY(First,Second) REFERENCES Parent(K1,K2));
            INSERT INTO Child VALUES (1,1),(1,2);
            CREATE VIEW ParentChild AS SELECT p.Id AS ParentId, c.Second AS Revision FROM Parent p JOIN Child c ON p.K1=c.First AND p.K2=c.Second;
            CREATE TEMP VIEW TempDependent AS SELECT Payload FROM main.Parent;
            CREATE TABLE Other (ParentId INTEGER);
            CREATE TRIGGER GuardOther BEFORE INSERT ON Other WHEN EXISTS(SELECT 1 FROM Parent WHERE Id=NEW.ParentId AND Archived=0)
              BEGIN SELECT RAISE(ABORT, 'Active parent'); END;
            CREATE TABLE Touches (Id INTEGER);
            CREATE TRIGGER TrackParent AFTER UPDATE ON Parent BEGIN INSERT INTO Touches VALUES(NEW.Id); END;
            """);
    }
    [TestCase(0)]
    [TestCase(1)]
    public void RebuildPreservesDependentObjectsAndConnectionSettings(int legacy)
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null); Seed(p);
        p.ExecuteNonQuery("PRAGMA legacy_alter_table=" + legacy);
        p.AddCheckConstraint("CK_Qty", "Parent", "Qty>0");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM ParentChild"), Is.EqualTo(2L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM TempDependent"), Is.EqualTo(2L));
        Assert.That((byte[])p.ExecuteScalar("SELECT Data FROM Parent WHERE Id=1"), Is.EqualTo(new byte[] { 0, 255 }));
        p.ExecuteNonQuery("INSERT INTO Other VALUES(2)"); Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO Other VALUES(1)"));
        p.ExecuteNonQuery("UPDATE Parent SET Qty=2 WHERE Id=1"); Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Touches"), Is.EqualTo(1L));
        p.ExecuteNonQuery("INSERT INTO Parent(K1,K2) VALUES(3,3)");
        Assert.That(Convert.ToInt64(p.ExecuteScalar("SELECT Id FROM Parent WHERE K1=3")), Is.GreaterThan(100));
        Assert.That(p.ExecuteScalar("SELECT Payload FROM Parent WHERE K1=3"), Is.EqualTo("[]"));
        Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO Parent(K1,K2,Qty) VALUES(4,4,0)"));
        Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO Parent(K1,K2,Archived) VALUES(1,3,0)"));
        p.ExecuteNonQuery("INSERT INTO Parent(K1,K2,Archived) VALUES(1,3,1)");
        Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO Child VALUES(9,9)"));
        Assert.That(p.ExecuteScalar("PRAGMA foreign_keys"), Is.EqualTo(1L));
        Assert.That(p.ExecuteScalar("PRAGMA legacy_alter_table"), Is.EqualTo((long)legacy));
        Assert.That(p.CheckForeignKeyIntegrity(), Is.True);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void FailedCopyRestoresAllObjectsAndCanContinue(bool outerTransaction)
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null); Seed(p);
        if (outerTransaction) { p.SetPragmaForeignKeys(false); p.BeginTransaction(); }
        Assert.Catch(() => p.AddCheckConstraint("CK_Invalid", "Parent", "Qty>100"));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM ParentChild"), Is.EqualTo(2L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM TempDependent"), Is.EqualTo(2L));
        Assert.That(p.TableExists("ParentTemp"), Is.False);
        p.ExecuteNonQuery("INSERT INTO Other VALUES(2)"); Assert.Catch(() => p.ExecuteNonQuery("INSERT INTO Other VALUES(1)"));
        p.ExecuteNonQuery("UPDATE Parent SET Qty=2 WHERE Id=1"); Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Touches"), Is.EqualTo(1L));
        if (outerTransaction) { p.Commit(); p.SetPragmaForeignKeys(true); }
        Assert.That(p.ExecuteScalar("PRAGMA foreign_keys"), Is.EqualTo(1L));
        Assert.That(p.ExecuteScalar("PRAGMA legacy_alter_table"), Is.EqualTo(0L));
    }
    [Test]
    public void RemovingReferencedColumnsFailsAndNativeRenameKeepsViewsUsable()
    {
        using var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); c.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null); Seed(p);
        p.SetPragmaForeignKeys(false);
        Assert.Catch(() => p.RemoveColumn("Parent", "Payload"));
        Assert.That(p.ColumnExists("Parent", "Payload"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM TempDependent"), Is.EqualTo(2L));
        p.RenameColumn("Parent", "Payload", "RenamedPayload");
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM TempDependent"), Is.EqualTo(2L));
    }
}
