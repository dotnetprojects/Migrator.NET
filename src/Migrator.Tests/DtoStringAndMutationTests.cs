using System;
using System.Linq;
using System.Linq.Expressions;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Sybase;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
using DtoColumn = System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;

namespace Migrator.Tests;

[Category("SQLite")]
public class DtoStringAndMutationTests
{
    public class Row
    {
        public int Id { get; set; }
        public bool Archive { get; set; }
        [DtoColumn("select")] public string Text { get; set; }
        public int? Optional { get; set; }
    }
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        provider.ExecuteNonQuery("CREATE TABLE \"typed rows\" (Id INTEGER, Archive INTEGER, \"select\" TEXT, Optional INTEGER)");
        var columns = new[] { "Id", "Archive", "select", "Optional" };
        provider.Insert("typed rows", columns, new object[] { 1, false, "alpha aa omega", 7 });
        provider.Insert("typed rows", columns, new object[] { 2, true, "other", 9 });
        provider.Insert("typed rows", columns, new object[] { 3, false, null, null });
    }

    [TearDown] public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [TestCase("contains")]
    [TestCase("starts")]
    [TestCase("ends")]
    public void SupportsStringFunctionsAndCapturedText(string kind)
    {
        var needle = kind == "contains" ? "aa" : kind == "starts" ? "alpha" : "omega";
        Expression<Func<Row, bool>> predicate = kind switch
        {
            "contains" => x => x.Text.Contains(needle),
            "starts" => x => x.Text.StartsWith(needle),
            _ => x => x.Text.EndsWith(needle)
        };
        Assert.That(provider.Select("typed rows", predicate).Single().Id, Is.EqualTo(1));
    }

    [TestCase("%")]
    [TestCase("_")]
    [TestCase("~")]
    [TestCase("[ab]")]
    [TestCase("\\")]
    [TestCase("'; DROP TABLE anything; --")]
    public void StringSearchTreatsWildcardsEscapeCharactersAndSqlTextLiterally(string needle)
    {
        provider.Update<Row>("typed rows", x => new Row { Text = needle }, x => x.Id == 1);
        Assert.That(provider.Select<Row>("typed rows", x => x.Text.Contains(needle)).Single().Id, Is.EqualTo(1));
        Assert.That(provider.Select<Row>("typed rows", x => x.Text.StartsWith(needle)).Single().Id, Is.EqualTo(1));
        Assert.That(provider.Select<Row>("typed rows", x => x.Text.EndsWith(needle)).Single().Id, Is.EqualTo(1));
        Assert.That(provider.Select<Row>("typed rows").Count, Is.EqualTo(3));
    }

    [Test]
    public void StringFunctionsComposeWithBooleanLogicAndKeepSqlNullSemantics()
    {
        Assert.That(provider.Select<Row>("typed rows", x => (!x.Archive && x.Id > 4) || x.Text.Contains("aa")).Single().Id, Is.EqualTo(1));
        Assert.That(provider.Select<Row>("typed rows", x => !x.Text.Contains("aa")).Single().Id, Is.EqualTo(2));
        Assert.That(provider.Select<Row>("typed rows", x => x.Text.Contains("")).Select(x => x.Id), Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(provider.Select<Row>("typed rows", x => x.Text.StartsWith("")).Count, Is.EqualTo(2));
        Assert.That(provider.Select<Row>("typed rows", x => x.Text.EndsWith("")).Count, Is.EqualTo(2));
    }

    [TestCase("char")]
    [TestCase("comparison")]
    [TestCase("nested")]
    [TestCase("column")]
    public void UnsupportedStringOverloadsFailBeforeCommandCreation(string kind)
    {
        var database = Substitute.For<ITransformationProvider>();
        Expression<Func<Row, bool>> predicate = kind switch
        {
            "char" => x => x.Text.Contains('a'),
            "comparison" => x => x.Text.Contains("aa", StringComparison.Ordinal),
            "nested" => x => x.Text.ToLower().Contains("aa"),
            _ => x => x.Text.Contains(x.Text)
        };
        Assert.Throws<NotSupportedException>(() => database.Select("typed rows", predicate));
        database.DidNotReceive().CreateCommand();
    }

    [Test]
    public void NullSearchTextFailsBeforeCommandCreation()
    {
        var database = Substitute.For<ITransformationProvider>(); string needle = null;
        Assert.Throws<ArgumentNullException>(() => database.Select<Row>("typed rows", x => x.Text.Contains(needle)));
        database.DidNotReceive().CreateCommand();
    }

    [Test]
    public void UpdateChangesOnlyInitializedPropertiesAndReturnsAffectedRows()
    {
        var newText = "O'Brien";
        Assert.That(provider.Update<Row>("typed rows", x => new Row { Text = newText, Archive = true, Optional = null },
            x => x.Text.StartsWith("alpha")), Is.EqualTo(1));
        var row = provider.Select<Row>("typed rows", x => x.Id == 1).Single();
        Assert.That(row.Text, Is.EqualTo(newText)); Assert.That(row.Archive, Is.True); Assert.That(row.Optional, Is.Null);
        Assert.That(row.Id, Is.EqualTo(1));
        Assert.That(provider.Select<Row>("typed rows", x => x.Id == 2).Single().Text, Is.EqualTo("other"));
        Assert.That(provider.Update<Row>("typed rows", x => new Row { Archive = false }, x => x.Id == 999), Is.Zero);
    }

    [Test]
    public void DeleteUsesGroupedPredicatesAndReturnsAffectedRows()
    {
        Assert.That(provider.Delete<Row>("typed rows", x => (x.Id > 4 && x.Archive) || x.Text.Contains("aa")), Is.EqualTo(1));
        Assert.That(provider.Select<Row>("typed rows").Select(x => x.Id), Is.EquivalentTo(new[] { 2, 3 }));
        Assert.That(provider.Delete<Row>("typed rows", x => x.Id == 999), Is.Zero);
    }

    [Test]
    public void UpdatesAndDeletesParticipateInTheMigrationTransaction()
    {
        provider.BeginTransaction();
        provider.Update<Row>("typed rows", x => new Row { Text = "changed" }, x => x.Id == 1);
        provider.Delete<Row>("typed rows", x => x.Id == 2);
        Assert.That(provider.Select<Row>("typed rows").Count, Is.EqualTo(2));
        provider.Rollback();
        Assert.That(provider.Select<Row>("typed rows").Count, Is.EqualTo(3));
        Assert.That(provider.Select<Row>("typed rows", x => x.Id == 1).Single().Text, Is.EqualTo("alpha aa omega"));
    }

    [TestCase("empty")]
    [TestCase("identity")]
    [TestCase("row-dependent")]
    public void InvalidUpdateAssignmentsFailBeforeCommandCreation(string kind)
    {
        var database = Substitute.For<ITransformationProvider>();
        Expression<Func<Row, Row>> changes = kind switch
        {
            "empty" => x => new Row(),
            "identity" => x => x,
            _ => x => new Row { Id = x.Id + 1 }
        };
        Assert.Throws<NotSupportedException>(() => database.Update("typed rows", changes, x => x.Id == 1));
        database.DidNotReceive().CreateCommand();
    }

    [Test]
    public void MutationsRequirePredicatesAndDisposeCommandsOnFailure()
    {
        var database = Substitute.For<ITransformationProvider>();
        Assert.Throws<ArgumentNullException>(() => database.Delete<Row>("typed rows", null));
        Assert.Throws<ArgumentNullException>(() => database.Update<Row>("typed rows", x => new Row { Id = 1 }, null));
        database.DidNotReceive().CreateCommand();
        var command = Substitute.For<System.Data.IDbCommand>();
        database.CreateCommand().Returns(command);
        database.QuoteTableNameIfRequired(Arg.Any<string>()).Returns(x => (string)x[0]);
        database.QuoteColumnNameIfRequired(Arg.Any<string>()).Returns(x => (string)x[0]);
        database.GenerateParameterName(Arg.Any<int>()).Returns(x => "@p" + x[0]);
        command.ExecuteNonQuery().Returns(_ => throw new InvalidOperationException("driver failure"));
        Assert.Throws<InvalidOperationException>(() => database.Delete<Row>("typed rows", x => x.Id == 1));
        command.Received().Dispose();
    }
}

public class DtoLikeEscapingTests
{
    [Test]
    public void DialectsEscapeOnlyTheirOwnWildcards()
    {
        Assert.That(new OracleDialect().EscapeLikePattern("%_~[a]"), Is.EqualTo("~%~_~~[a]"));
        Assert.That(new SQLiteDialect().EscapeLikePattern("%_~[a]"), Is.EqualTo("~%~_~~[a]"));
        Assert.That(new SqlServerDialect().EscapeLikePattern("%_~[a]"), Is.EqualTo("~%~_~~~[a]"));
        Assert.That(new SybaseDialect().EscapeLikePattern("%_~[a]"), Is.EqualTo("~%~_~~~[a]"));
    }
}
