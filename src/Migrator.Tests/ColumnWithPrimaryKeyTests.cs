using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;

namespace Migrator.Tests;

[Category("SQLite")]
[TestFixture(false)]
[TestFixture(true)]
public class ColumnWithPrimaryKeyTests(bool systemData)
{
    private IDbConnection connection;
    private ITransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = systemData
            ? new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
            : new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.ExecuteNonQuery("CREATE TABLE Settings (Value TEXT, CONSTRAINT UQ_Value UNIQUE(Value), CHECK(length(Value)>0)); CREATE INDEX IX_Value ON Settings(Value); INSERT INTO Settings VALUES ('first'), ('second');");
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [Test]
    public void AddsIdentityAndKeyTogetherAndSupportsPortableDowngrade()
    {
        var column = new Column("Id", DbType.Int32) { IsIdentity = true };
        provider.AddColumn("Settings", column, new PrimaryKeyConstraint("PK_Settings", "Id"));
        Assert.That(column.IsNullable, Is.True, "The caller's definition must not be mutated.");
        Assert.That(provider.ExecuteScalar("SELECT COUNT(DISTINCT Id) FROM Settings"), Is.EqualTo(2));
        Assert.That(provider.GetTableConstraints("Settings").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("PK_Settings"));
        provider.Insert("Settings", ["Value"], ["third"]);
        Assert.That(provider.ExecuteScalar("SELECT Id FROM Settings WHERE Value='third'"), Is.EqualTo(3));
        provider.RemovePrimaryKey("Settings");
        provider.RemoveColumn("Settings", "Id");
        Assert.That(provider.GetColumns("Settings").Select(c => c.Name), Is.EqualTo(new[] { "Value" }));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Settings"), Is.EqualTo(3));
        Assert.That(provider.GetTableConstraints("Settings").OfType<UniqueConstraint>().Single().Name, Is.EqualTo("UQ_Value"));
        Assert.That(provider.GetTableConstraints("Settings").OfType<CheckConstraint>().Count(), Is.EqualTo(1));
        Assert.That(provider.GetIndexes("Settings").Any(index => index.Name == "IX_Value"), Is.True);
    }

    [TestCase("Missing")]
    [TestCase("Value")]
    public void InvalidIdentityKeyLeavesOriginalSchemaAndData(string keyColumn)
    {
        Assert.Catch(() => provider.AddColumn("Settings", new Column("Id", DbType.Int32) { IsIdentity = true }, new PrimaryKeyConstraint("PK_Settings", keyColumn)));
        Assert.That(provider.ColumnExists("Settings", "Id"), Is.False);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Settings"), Is.EqualTo(2));
        Assert.That(provider.GetTables(), Is.EqualTo(new[] { "Settings" }));
    }

    [Test]
    public void ExistingPrimaryKeyIsNotReplaced()
    {
        provider.AddPrimaryKey("PK_Old", "Settings", "Value");
        Assert.Catch(() => provider.AddColumn("Settings", new Column("Id", DbType.Int32) { IsIdentity = true }, new PrimaryKeyConstraint("PK_New", "Id")));
        Assert.That(provider.ColumnExists("Settings", "Id"), Is.False);
        Assert.That(provider.GetTableConstraints("Settings").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("PK_Old"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemovesOnlyTheSelectedUniqueDefinition(bool named)
    {
        provider.ExecuteNonQuery($"CREATE TABLE Legacy (A TEXT, B TEXT, {(named ? "CONSTRAINT UQ_A " : "")}UNIQUE(A), UNIQUE(B), CHECK(length(A)>0)); INSERT INTO Legacy VALUES ('a','b');");
        var constraint = provider.GetTableConstraints("Legacy").OfType<UniqueConstraint>().Single(key => key.KeyColumns.SequenceEqual(new[] { "A" }));
        provider.RemoveUniqueConstraint("Legacy", constraint);
        Assert.That(provider.GetTableConstraints("Legacy").OfType<UniqueConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "B" }));
        Assert.That(provider.GetTableConstraints("Legacy").OfType<CheckConstraint>().Count(), Is.EqualTo(1));
        provider.ExecuteNonQuery("INSERT INTO Legacy VALUES ('a','c')");
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Legacy"), Is.EqualTo(2));
    }

    [Test]
    public void CompositeKeyKeepsOrderAndFailedBackfillRollsBack()
    {
        provider.AddColumn("Settings", new Column("Part", DbType.Int32) { DefaultValue = 1 }, new PrimaryKeyConstraint("PK_Settings", "Part", "Value"));
        Assert.That(provider.GetTableConstraints("Settings").OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Part", "Value" }));
        provider.RemovePrimaryKey("Settings");
        Assert.Catch(() => provider.AddColumn("Settings", new Column("Duplicate", DbType.String, 20) { DefaultValue = "same" }, new PrimaryKeyConstraint("PK_Duplicate", "Duplicate")));
        Assert.That(provider.ColumnExists("Settings", "Duplicate"), Is.False);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Settings"), Is.EqualTo(2));
        Assert.That(provider.GetTables(), Is.EqualTo(new[] { "Settings" }));
    }

    [Test]
    public void UnknownUniqueDefinitionDoesNotRemoveOtherConstraints()
    {
        Assert.Throws<MigrationException>(() => provider.RemoveUniqueConstraint("Settings", new UniqueConstraint("UQ_Value", "Missing")));
        Assert.That(provider.GetTableConstraints("Settings").OfType<UniqueConstraint>().Single().Name, Is.EqualTo("UQ_Value"));
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Settings"), Is.EqualTo(2));
    }
}
