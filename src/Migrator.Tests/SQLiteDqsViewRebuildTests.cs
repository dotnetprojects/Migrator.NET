using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

public abstract class SQLiteDqsViewRebuildTestsBase
{
    protected abstract IDbConnection OpenConnection();
    protected abstract void SetDoubleQuotedStrings(IDbConnection connection, bool enabled);

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void UnrelatedLegacyViewsDoNotBlockColumnRemoval(bool readsAnotherTable, bool indirect)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        SetDoubleQuotedStrings(connection, true);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,2); CREATE TABLE Other(Account INTEGER); INSERT INTO Other VALUES(3)");
        p.ExecuteNonQuery("CREATE VIEW Unrelated AS SELECT \"legacy\" AS Label" + (readsAnotherTable ? " FROM Other" : ""));
        if (indirect) p.ExecuteNonQuery("CREATE TEMP VIEW Report AS SELECT Label FROM main.Unrelated");
        p.RemoveColumn("Account", "Obsolete");
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Label FROM " + (indirect ? "temp.Report" : "Unrelated")), Is.EqualTo("legacy"));
        AssertDoubleQuotedStrings(p, true);
    }

    [Test]
    public void SameRootPageInAnotherDatabaseIsNotADependency()
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        SetDoubleQuotedStrings(connection, true);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; ATTACH DATABASE ':memory:' AS aux; CREATE TABLE main.Account(Id INTEGER, Obsolete INTEGER UNIQUE); CREATE TABLE aux.Account(Id INTEGER); INSERT INTO aux.Account VALUES(1); CREATE VIEW aux.Report AS SELECT \"legacy\" AS Label FROM Account");
        p.RemoveColumn("main.Account", "Obsolete");
        Assert.That(p.ColumnExists("main.Account", "Obsolete"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Label FROM aux.Report"), Is.EqualTo("legacy"));
    }

    [TestCase(TriggerEvent.Insert, "main", false, false)]
    [TestCase(TriggerEvent.Update, "main", false, false)]
    [TestCase(TriggerEvent.Delete, "main", false, false)]
    [TestCase(TriggerEvent.Insert, "aux", false, false)]
    [TestCase(TriggerEvent.Insert, "aux", false, true)]
    [TestCase(TriggerEvent.Insert, "main", true, false)]
    [TestCase(TriggerEvent.Update, "main", true, false)]
    [TestCase(TriggerEvent.Delete, "main", true, false)]
    public void ExternalTriggersThroughViewsRemainExecutable(TriggerEvent operation, string schema, bool quotedColumn, bool temporary)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        SetDoubleQuotedStrings(connection, true);
        p.ExecuteNonQuery($"PRAGMA foreign_keys=OFF; ATTACH DATABASE ':memory:' AS aux; CREATE TABLE {schema}.Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO {schema}.Account VALUES(1,2); CREATE VIEW {schema}.Projection AS SELECT * FROM Account; CREATE TABLE {schema}.Other(Id INTEGER); INSERT INTO {schema}.Other VALUES(1); CREATE TABLE {schema}.Audit(Value INTEGER)");
        var field = quotedColumn ? "\"Obsolete\"" : "Obsolete";
        var row = operation == TriggerEvent.Delete ? "OLD" : "NEW";
        p.ExecuteNonQuery($"CREATE {(temporary ? "TEMP TRIGGER Track" : "TRIGGER " + schema + ".Track")} AFTER {operation.ToString().ToUpperInvariant()} ON {schema}.Other WHEN EXISTS(SELECT {field} FROM Projection WHERE Id={row}.Id) BEGIN INSERT INTO Audit(Value) SELECT {field} FROM Projection WHERE Id={row}.Id; END");
        Assert.Catch(() => p.RemoveColumn(schema + ".Account", "Obsolete"));
        Assert.That(p.ColumnExists(schema + ".Account", "Obsolete"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM " + schema + ".Other"), Is.EqualTo(1L));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM " + schema + ".Audit"), Is.EqualTo(0L), "Preparing trigger programs must never execute them.");
        if (operation == TriggerEvent.Insert) p.Insert(schema + ".Other", ["Id"], [1]);
        else if (operation == TriggerEvent.Update) p.Update(schema + ".Other", ["Id"], [1], ["Id"], [1]);
        else p.Delete(schema + ".Other", ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT Value FROM " + schema + ".Audit"), Is.EqualTo(2L));
        AssertDoubleQuotedStrings(p, true);
    }

    [TestCase("CurrentStatus")]
    [TestCase("rowid")]
    [TestCase("OID")]
    [TestCase("_rowid_")]
    public void WatchedUpdateColumnsAndGeneratedColumnsAreIncludedInProgramValidation(string watched)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,2); CREATE VIEW Projection AS SELECT * FROM Account; CREATE TABLE Other(Id INTEGER, CurrentStatus INTEGER, Computed INTEGER GENERATED ALWAYS AS (Id+1)); INSERT INTO Other(Id,CurrentStatus) VALUES(1,0); CREATE TABLE Audit(Value INTEGER)");
        p.ExecuteNonQuery("CREATE TRIGGER Track AFTER UPDATE OF " + watched + " ON Other WHEN EXISTS(SELECT Obsolete FROM Projection WHERE Id=NEW.Id) BEGIN INSERT INTO Audit SELECT Obsolete FROM Projection WHERE Id=NEW.Id; END");
        Assert.Catch(() => p.RemoveColumn("Account", "Obsolete"));
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(0L));
        p.Update("Other", [watched], [2], ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT Value FROM Audit"), Is.EqualTo(2L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LiteralTableNamesInUnrelatedTriggersDoNotBlockRebuilds(bool temporary)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); CREATE TABLE Other(Id INTEGER); CREATE TABLE Audit(Value TEXT)");
        p.ExecuteNonQuery("CREATE " + (temporary ? "TEMP " : "") + "TRIGGER Track AFTER INSERT ON main.Other BEGIN INSERT INTO Audit VALUES('Account'); END");
        p.RemoveColumn("Account", "Obsolete");
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(0L));
        p.Insert("Other", ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT Value FROM Audit"), Is.EqualTo("Account"));
    }

    [Test]
    public void InsteadOfTriggerOnAViewIsValidatedWithoutExecutingIt()
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,2); CREATE VIEW Projection AS SELECT * FROM Account; CREATE VIEW Other AS SELECT Id FROM Account; CREATE TABLE Audit(Value INTEGER); CREATE TRIGGER Track INSTEAD OF INSERT ON Other BEGIN INSERT INTO Audit SELECT Obsolete FROM Projection WHERE Id=NEW.Id; END");
        Assert.Catch(() => p.RemoveColumn("Account", "Obsolete"));
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM Audit"), Is.EqualTo(0L));
        p.Insert("Other", ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT Value FROM Audit"), Is.EqualTo(2L));
    }

    [Test]
    public void ValidExternalDependenciesAndPreExistingInvalidTriggersArePreserved()
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(1,2); CREATE VIEW Projection AS SELECT * FROM Account; CREATE TABLE Other(Id INTEGER); CREATE TABLE Broken(Id INTEGER); CREATE TABLE Audit(Value INTEGER); CREATE TRIGGER Track AFTER INSERT ON Other BEGIN INSERT INTO Audit SELECT Id FROM Projection; END; CREATE TRIGGER AlreadyBroken AFTER INSERT ON Broken BEGIN INSERT INTO MissingTable VALUES(NEW.Id); END");
        p.RemoveColumn("Account", "Obsolete");
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.False);
        p.Insert("Other", ["Id"], [1]);
        Assert.That(p.ExecuteScalar("SELECT Value FROM Audit"), Is.EqualTo(1L));
        Assert.That(Assert.Catch(() => p.Insert("Broken", ["Id"], [1])).ToString(), Does.Contain("MissingTable"));
    }

    [TestCase("main", false, true, false)]
    [TestCase("aux", false, true, false)]
    [TestCase("main", true, true, false)]
    [TestCase("main", false, false, false)]
    [TestCase("main", true, true, true)]
    public void RemovedQuotedColumnCannotBecomeALiteral(string schema, bool indirect, bool dqs, bool outerTransaction)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        SetDoubleQuotedStrings(connection, dqs);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; PRAGMA legacy_alter_table=OFF; ATTACH DATABASE ':memory:' AS aux");
        p.ExecuteNonQuery($"CREATE TABLE {schema}.Account(Id INTEGER, \"old name\" INTEGER UNIQUE); INSERT INTO {schema}.Account VALUES(1,2); CREATE VIEW {schema}.Projection AS SELECT \"old name\" AS Snapshot FROM Account");
        if (indirect) p.ExecuteNonQuery($"CREATE TEMP VIEW Report AS SELECT Snapshot FROM {schema}.Projection");
        var view = indirect ? "temp.Report" : schema + ".Projection";
        if (outerTransaction) p.BeginTransaction();
        var error = Assert.Catch(() => p.RemoveColumn(schema + ".Account", "old name"));
        Assert.That(error is MigrationException or System.Data.Common.DbException, Is.True);
        if (dqs) Assert.That(error, Is.InstanceOf<MigrationException>());
        Assert.That(error.ToString(), Does.Contain("old name"));
        Assert.That(p.ColumnExists(schema + ".Account", "old name"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT Snapshot FROM " + view), Is.EqualTo(2L));
        Assert.That(p.ExecuteScalar("PRAGMA legacy_alter_table"), Is.EqualTo(0L));
        AssertDoubleQuotedStrings(p, dqs);
        if (outerTransaction)
        {
            p.Insert(schema + ".Account", ["Id", "old name"], [2, 4]);
            p.Rollback();
            Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM " + schema + ".Account"), Is.EqualTo(1L));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void UnrelatedColumnRemovalRestoresTheOriginalSetting(bool dqs)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        SetDoubleQuotedStrings(connection, dqs);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, Obsolete INTEGER UNIQUE); INSERT INTO Account VALUES(2,3); CREATE VIEW Report AS SELECT \"Id\" AS Snapshot, 'kept' AS Label FROM Account");
        p.RemoveColumn("Account", "Obsolete");
        Assert.That(p.ColumnExists("Account", "Obsolete"), Is.False);
        Assert.That(p.ExecuteScalar("SELECT Snapshot FROM Report"), Is.EqualTo(2L));
        Assert.That(p.ExecuteScalar("SELECT Label FROM Report"), Is.EqualTo("kept"));
        AssertDoubleQuotedStrings(p, dqs);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LegacyLiteralViewsRequireExplicitStringQuotingBeforeSchemaChanges(bool mixed)
    {
        using var connection = OpenConnection();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        SetDoubleQuotedStrings(connection, true);
        p.ExecuteNonQuery("PRAGMA foreign_keys=OFF; CREATE TABLE Account(Id INTEGER, \"old name\" INTEGER UNIQUE); INSERT INTO Account VALUES(1,2)");
        p.ExecuteNonQuery("CREATE VIEW Report AS SELECT \"legacy literal\" AS Label" + (mixed ? ", \"old name\" AS Snapshot" : "") + " FROM Account");
        Assert.Catch<MigrationException>(() => p.RemoveColumn("Account", "old name"));
        Assert.That(p.ColumnExists("Account", "old name"), Is.True);
        Assert.That(p.ExecuteScalar("SELECT Label FROM Report"), Is.EqualTo("legacy literal"));
        if (mixed) Assert.That(p.ExecuteScalar("SELECT Snapshot FROM Report"), Is.EqualTo(2L));
        AssertDoubleQuotedStrings(p, true);
    }

    private static void AssertDoubleQuotedStrings(ITransformationProvider p, bool enabled)
    {
        using var command = p.CreateCommand();
        command.CommandText = "SELECT \"missing_" + Guid.NewGuid().ToString("N") + "\"";
        if (enabled) Assert.That(command.ExecuteScalar(), Is.TypeOf<string>());
        else Assert.Catch(() => command.ExecuteScalar());
    }
}

[Category("SQLite")]
public class SQLiteDqsViewRebuildTests : SQLiteDqsViewRebuildTestsBase
{
    protected override IDbConnection OpenConnection()
    { var connection = new SqliteConnection("Data Source=:memory:"); connection.Open(); return connection; }
    protected override void SetDoubleQuotedStrings(IDbConnection connection, bool enabled) =>
        Assert.That(SQLitePCL.raw.sqlite3_db_config(((SqliteConnection)connection).Handle, SQLitePCL.raw.SQLITE_DBCONFIG_DQS_DML, enabled ? 1 : 0, out _), Is.Zero);
}

[Category("SQLite")]
public class SQLiteLegacyDqsViewRebuildTests : SQLiteDqsViewRebuildTestsBase
{
    protected override IDbConnection OpenConnection()
    { var connection = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:"); connection.Open(); return connection; }
    protected override void SetDoubleQuotedStrings(IDbConnection connection, bool enabled) =>
        ((System.Data.SQLite.SQLiteConnection)connection).SetConfigurationOption(System.Data.SQLite.SQLiteConfigDbOpsEnum.SQLITE_DBCONFIG_DQS_DML, enabled);
}
