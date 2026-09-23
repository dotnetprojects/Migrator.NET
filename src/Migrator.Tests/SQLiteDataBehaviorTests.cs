using System;
using System.Data;
using System.IO;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteDataBehaviorTests
{
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        provider.CommandTimeout = 12;
        provider.ExecuteNonQuery("CREATE TABLE Items (Id INTEGER, Name TEXT, Deleted TEXT, Note TEXT)");
        provider.Insert("Items", new[] { "Id", "Name", "Deleted", "Note" }, new object[] { 1, "O'Brien", null, null });
        provider.Insert("Items", new[] { "Id", "Name", "Deleted", "Note" }, new object[] { 2, "other", "yes", "note" });
        provider.Insert("Items", new[] { "Id", "Name", "Deleted", "Note" }, new object[] { 3, "other", null, "note" });
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [TestCase(false, true, false, 1)]
    [TestCase(false, false, true, 2)]
    [TestCase(true, true, false, 1)]
    [TestCase(false, true, true, 3)]
    [TestCase(true, true, true, 3)]
    public void SelectCombinesEqualityNullAndNotNullPredicates(bool equality, bool nulls, bool notNulls, int expectedId)
    {
        using var command = provider.CreateCommand();
        using var reader = provider.SelectComplex(command, "Items", new[] { "Id", "Name" },
            equality ? new[] { "Id" } : null, equality ? new object[] { expectedId } : null,
            nulls ? (notNulls ? new[] { "Deleted" } : new[] { "Deleted", "Note" }) : null,
            notNulls ? (nulls ? new[] { "Note", "Name" } : new[] { "Deleted", "Note" }) : null);
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetInt64(0), Is.EqualTo(expectedId));
        Assert.That(reader.Read(), Is.False);
    }

    [Test]
    public void ParameterizedUpdateAndDeleteDistinguishNullPredicatesAndKeepOtherRows()
    {
        Assert.That(provider.Update("Items", new[] { "Name", "Note" }, new object[] { "updated ' value", "saved" },
            new[] { "Id", "Deleted" }, new object[] { 1, null }), Is.EqualTo(1));
        Assert.That(provider.SelectScalar("Name", "Items", new[] { "Id" }, new object[] { 1 }), Is.EqualTo("updated ' value"));
        Assert.That(provider.Delete("Items", new[] { "Name", "Deleted" }, new object[] { "other", DBNull.Value }), Is.EqualTo(1));
        Assert.That(provider.ExecuteStringQuery("SELECT Name FROM Items ORDER BY Id"), Is.EqualTo(new[] { "updated ' value", "other" }));
        Assert.That(provider.SelectScalar("Note", "Items", "Id = 1"), Is.EqualTo("saved"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeleteBindsNonNullParametersAfterNullPredicates(bool databaseNull)
    {
        Assert.That(provider.Delete("Items", new[] { "Deleted", "Name", "Id" }, new object[] { databaseNull ? DBNull.Value : null, "other", 3 }), Is.EqualTo(1));
        Assert.That(provider.ExecuteStringQuery("SELECT Id FROM Items ORDER BY Id"), Is.EqualTo(new[] { "1", "2" }));
    }

    [TestCase("columns-null")]
    [TestCase("values-null")]
    [TestCase("length-mismatch")]
    [TestCase("empty")]
    public void InvalidDeletePredicatesMustNotDeleteTheWholeTable(string invalid)
    {
        var columns = invalid == "columns-null" ? null : invalid == "empty" ? Array.Empty<string>() : new[] { "Id" };
        var values = invalid == "values-null" ? null : invalid is "length-mismatch" or "empty" ? Array.Empty<object>() : new object[] { 1 };
        Assert.Catch<ArgumentException>(() => provider.Delete("Items", columns, values));
        Assert.That(provider.SelectScalar("COUNT(*)", "Items"), Is.EqualTo(3L));
    }

    [Test]
    public void InsertIfMissingOnlyInsertsOnceAndRetainsOriginalValues()
    {
        var columns = new[] { "Id", "Name" };
        Assert.That(provider.InsertIfNotExists("Items", columns, new object[] { 4, "first" }, new[] { "Id" }, new object[] { 4 }), Is.EqualTo(1));
        Assert.That(provider.InsertIfNotExists("Items", columns, new object[] { 4, "overwrite" }, new[] { "Id" }, new object[] { 4 }), Is.Zero);
        Assert.That(provider.SelectScalar("Name", "Items", "Id=4"), Is.EqualTo("first"));
    }

    [Test]
    public void UpdateWithSqlPredicateAndWholeTableDeleteHaveExplicitRowCounts()
    {
        Assert.That(provider.Update("Items", new[] { "Name", "Note" }, new object[] { "all", "changed" }, "Id >= 2"), Is.EqualTo(2));
        Assert.That(provider.Update("Items", new[] { "Note" }, new object[] { "everyone" }), Is.EqualTo(3));
        Assert.That(provider.SelectScalar("COUNT(*)", "Items"), Is.EqualTo(3L));
        Assert.That(provider.Delete("Items"), Is.EqualTo(3));
        Assert.That(provider.SelectScalar("COUNT(*)", "Items"), Is.EqualTo(0L));
    }

    [Test]
    public void ParameterizedNonQueryAndStringResultsPreserveNullsAndQuotes()
    {
        Assert.That(provider.ExecuteNonQuery("INSERT INTO Items (Id, Name) VALUES (@p0, @p1)", 9, 4, "'; DROP TABLE Items; --"), Is.EqualTo(1));
        Assert.That(provider.ExecuteStringQuery("SELECT Note FROM Items WHERE Id IN (1,2) ORDER BY Id"), Is.EqualTo(new string[] { null, "note" }));
        Assert.That(provider.SelectScalar("Name", "Items", "Id=4"), Is.EqualTo("'; DROP TABLE Items; --"));
        Assert.That(provider.TableExists("Items"), Is.True);
    }

    [TestCase("insert", "table")]
    [TestCase("insert", "columns")]
    [TestCase("insert", "values")]
    [TestCase("update", "table")]
    [TestCase("update", "columns")]
    [TestCase("update", "values")]
    [TestCase("filtered-update", "table")]
    [TestCase("filtered-update", "columns")]
    [TestCase("filtered-update", "values")]
    public void MissingWriteArgumentsAreRejectedWithoutChangingData(string operation, string missing)
    {
        var table = missing == "table" ? null : "Items";
        var columns = missing == "columns" ? null : new[] { "Name" };
        var values = missing == "values" ? null : new object[] { "unexpected" };
        var error = Assert.Throws<ArgumentNullException>(() =>
        {
            if (operation == "insert") provider.Insert(table, columns, values);
            else if (operation == "update") provider.Update(table, columns, values, "Id=1");
            else provider.Update(table, columns, values, new[] { "Id" }, new object[] { 1 });
        });
        Assert.That(error.ParamName, Is.EqualTo(missing));
        Assert.That(provider.SelectScalar("COUNT(*)", "Items"), Is.EqualTo(3L));
        Assert.That(provider.SelectScalar("Name", "Items", "Id=1"), Is.EqualTo("O'Brien"));
    }

    [TestCase("insert")]
    [TestCase("update")]
    [TestCase("filtered-update")]
    [TestCase("where")]
    public void MismatchedWriteArgumentsFailBeforeExecution(string operation)
    {
        Assert.Catch(() =>
        {
            if (operation == "insert") provider.Insert("Items", new[] { "Id", "Name" }, new object[] { 9 });
            else if (operation == "update") provider.Update("Items", new[] { "Id", "Name" }, new object[] { 9 }, "Id=1");
            else provider.Update("Items", new[] { "Name" }, operation == "where" ? new object[] { "wrong" } : Array.Empty<object>(),
                new[] { "Id" }, operation == "where" ? Array.Empty<object>() : new object[] { 1 });
        });
        Assert.That(provider.SelectScalar("Name", "Items", "Id=1"), Is.EqualTo("O'Brien"));
        Assert.That(provider.SelectScalar("COUNT(*)", "Items"), Is.EqualTo(3L));
    }

    [Test]
    public void InvalidReadAndDeleteArgumentsLeaveTheConnectionUsable()
    {
        using var command = provider.CreateCommand();
        Assert.Throws<ArgumentNullException>(() => provider.SelectComplex(command, null, new[] { "Id" }));
        Assert.Throws<ArgumentNullException>(() => provider.SelectComplex(command, "Items", null));
        Assert.Throws<ArgumentNullException>(() => provider.Delete(null));
        Assert.Throws<SqliteException>(() => provider.ExecuteScalar("SELECT Missing FROM Items"));
        Assert.That(provider.SelectScalar("COUNT(*)", "Items"), Is.EqualTo(3L));
    }

    [Test]
    public void EmbeddedScriptExecutesAndMissingResourceReportsItsName()
    {
        provider.ExecuteNonQuery("CREATE TABLE ScriptData (Id INTEGER)");
        provider.ExecuteResourceScript(GetType().Assembly, "Migrator.Tests.ScriptResource.sql");
        Assert.That(provider.SelectScalar("Id", "ScriptData"), Is.EqualTo(2L));
        var error = Assert.Throws<FileNotFoundException>(() => provider.ExecuteResourceScript(GetType().Assembly, "missing.sql"));
        Assert.That(error.FileName, Is.EqualTo("missing.sql"));
        Assert.That(provider.SelectScalar("COUNT(*)", "ScriptData"), Is.EqualTo(1L));
    }

    [Test]
    public void InspectorCallbacksDisposeReadersEvenWhenTheCallbackThrows()
    {
        var schema = new SchemaInspector(provider);
        IDataReader captured = null;
        Assert.Throws<InvalidOperationException>(() => schema.Query("SELECT * FROM Items", reader =>
        {
            captured = reader;
            Assert.That(reader.Read(), Is.True);
            throw new InvalidOperationException("callback");
        }));
        Assert.That(captured.IsClosed, Is.True);
        Assert.That(schema.Table("Items").SelectScalar("COUNT(*)"), Is.EqualTo(3L));
        Assert.That(schema.Table("Items").SelectScalar("Name", "Id=1"), Is.EqualTo("O'Brien"));
        Assert.That(schema.NullableScalar<long>("SELECT MAX(Id) FROM Items WHERE Id < 0"), Is.Null);
        Assert.That(schema.Strings("SELECT Name FROM Items WHERE Id={0}", 1), Is.EqualTo(new[] { "O'Brien" }));
    }
}
