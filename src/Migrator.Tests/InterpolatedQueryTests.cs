using System;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.Hana;
using DotNetProjects.Migrator.Providers.Impl.Informix;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
using DtoColumn = System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;

namespace Migrator.Tests;

[Category("SQLite")]
public class InterpolatedQueryTests
{
    public class Row
    {
        [DtoColumn("select")] public int Id { get; set; }
        public string Text { get; set; }
        public bool Active { get; set; }
    }
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        provider.ExecuteNonQuery("CREATE TABLE \"query rows\" (\"select\" INTEGER, Text TEXT, Active INTEGER)");
        provider.Insert("query rows", new[] { "select", "Text", "Active" }, new object[] { 1, "O'Brien", true });
        provider.Insert("query rows", new[] { "select", "Text", "Active" }, new object[] { 2, null, false });
    }
    [TearDown] public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [Test]
    public void MapsDtosUsingExplicitIdentifierWrappersAndNativeValues()
    {
        ITransformationProvider database = provider;
        var rows = database.Query<Row>($"SELECT {Sql.Col("select")}, {Sql.Col("Text")}, {Sql.Col("Active")} FROM {Sql.Table("query rows")} WHERE {Sql.Col("Active")} = {true}");
        Assert.That(rows.Single().Id, Is.EqualTo(1));
        Assert.That(rows.Single().Text, Is.EqualTo("O'Brien"));
        Assert.That(rows.Single().Active, Is.True);
    }

    [Test]
    public void FormatSpecifiersSupportTheSameDtoMapping()
    {
        var row = provider.Query<Row>($"SELECT {"select":column}, {"Text":column}, {"Active":column} FROM {"query rows":table} WHERE {"select":column} = {2}").Single();
        Assert.That(row.Id, Is.EqualTo(2)); Assert.That(row.Text, Is.Null); Assert.That(row.Active, Is.False);
    }

    [Test]
    public void MapperOverloadSupportsTuplesAndDisposesTheReaderBeforeReturn()
    {
        var rows = provider.Query($"SELECT {Sql.Col("select")}, {Sql.Col("Text")} FROM {Sql.Table("query rows")} ORDER BY {Sql.Col("select")}",
            r => (Id: Convert.ToInt32(r.GetValue(0)), Text: r.IsDBNull(1) ? null : r.GetString(1)));
        Assert.That(rows.Select(x => x.Id), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(rows[0].Text, Is.EqualTo("O'Brien")); Assert.That(rows[1].Text, Is.Null);
    }

    [Test]
    public void OrdinaryStringsAreValuesNotIdentifiersOrSqlFragments()
    {
        var injection = "'; DROP TABLE anything; --";
        Assert.That(provider.Query($"SELECT 1 FROM {Sql.Table("query rows")} WHERE Text = {injection}", r => r.GetInt64(0)), Is.Empty);
        Assert.That(provider.Query($"SELECT {"select"}, {injection}", r => (r.GetString(0), r.GetString(1))).Single(), Is.EqualTo(("select", injection)));
        Assert.That(provider.Select<Row>("query rows").Count, Is.EqualTo(2));
    }

    [Test]
    public void PreservesEscapedBracesRepeatedArgumentsAlignmentAndNullParameters()
    {
        var sql = FormattableStringFactory.Create("SELECT '{{literal}}', {0}, {0,8}, {1}, {2}", 7, null, DBNull.Value);
        var row = provider.Query(sql, r => (r.GetString(0), r.GetInt64(1), r.GetInt64(2), r.IsDBNull(3), r.IsDBNull(4))).Single();
        Assert.That(row, Is.EqualTo(("{literal}", 7L, 7L, true, true)));
    }

    [Test]
    public void UsesTheActiveTransactionAndSupportsMapperFailures()
    {
        provider.BeginTransaction();
        provider.Update<Row>("query rows", x => new Row { Text = "uncommitted" }, x => x.Id == 1);
        var rows = provider.Query<Row>($"SELECT * FROM {Sql.Table("query rows")} WHERE {Sql.Col("select")} = {1}");
        provider.Rollback();
        Assert.That(rows.Single().Text, Is.EqualTo("uncommitted"));
        Assert.Throws<InvalidOperationException>(() => provider.Query<int>($"SELECT * FROM {Sql.Table("query rows")}", r => throw new InvalidOperationException("mapper failure")));
        Assert.That(provider.Query<Row>($"SELECT * FROM {Sql.Table("query rows")}").Count, Is.EqualTo(2));
    }

    [Test]
    public void QuotesQualifiedTablePathsAndColumnAtomsContainingDots()
    {
        provider.ExecuteNonQuery("ATTACH ':memory:' AS \"extra schema\"");
        provider.ExecuteNonQuery("CREATE TABLE \"extra schema\".\"dotted table\" (\"a.b\" INTEGER)");
        provider.ExecuteNonQuery("INSERT INTO \"extra schema\".\"dotted table\" VALUES (3)");
        Assert.That(provider.Query($"SELECT {Sql.Col("a.b")} FROM {Sql.Table("\"extra schema\".\"dotted table\"")}", r => r.GetInt64(0)).Single(), Is.EqualTo(3));
    }

    [Test]
    public void MissingDtoColumnsAreReportedWithMappingContext()
    {
        var error = Assert.Throws<DataException>(() => provider.Query<Row>($"SELECT {Sql.Col("select")} FROM {Sql.Table("query rows")}"));
        Assert.That(error.Message, Does.Contain("Text").And.Contain("Row.Text"));
        Assert.That(provider.Query<Row>($"SELECT * FROM {Sql.Table("query rows")}").Count, Is.EqualTo(2));
    }

    [TestCase("unknown-format")]
    [TestCase("wrong-identifier-type")]
    [TestCase("empty-identifier")]
    [TestCase("wrong-wrapper-format")]
    [TestCase("complex-value")]
    [TestCase("bad-placeholder")]
    public void InvalidFormatsAndValuesFailBeforeCommandCreation(string kind)
    {
        var database = Substitute.For<ITransformationProvider>();
        FormattableString sql = kind switch
        {
            "unknown-format" => $"SELECT {7:D2}",
            "wrong-identifier-type" => $"SELECT {7:column}",
            "empty-identifier" => $"SELECT {Sql.Col("")}",
            "wrong-wrapper-format" => $"SELECT {Sql.Col("Id"):table}",
            "complex-value" => $"SELECT {new object()}",
            _ => FormattableStringFactory.Create("SELECT {1}", 7)
        };
        Assert.Catch(() => database.Query(sql, r => 1));
        database.DidNotReceive().CreateCommand();
    }
}

public class InterpolatedQueryBindingTests
{
    [TestCase("oracle", ":p0", "p0", 1)]
    [TestCase("hana", "?", "p0", true)]
    [TestCase("informix", "?", "@p0", true)]
    public void RepeatedPlaceholdersBindPerOccurrenceInOrder(string kind, string placeholder, string parameterName, object boolean)
    {
        var connection = Substitute.For<IDbConnection>(); connection.State.Returns(ConnectionState.Open);
        var command = Substitute.For<IDbCommand>(); connection.CreateCommand().Returns(command);
        using var holder = new SqliteCommand(); command.Parameters.Returns(holder.Parameters);
        command.CreateParameter().Returns(_ => new SqliteParameter());
        using var data = new DataTable(); using var reader = data.CreateDataReader(); command.ExecuteReader().Returns(reader);
        using TransformationProvider provider = kind switch
        {
            "oracle" => new OracleTransformationProvider(new OracleDialect(), connection, null, null, null),
            "hana" => new HanaTransformationProvider(new HanaDialect(), connection, null, null),
            _ => new InformixTransformationProvider(new InformixDialect(), connection, null, null)
        };
        provider.Query(FormattableStringFactory.Create("SELECT 1 WHERE {0} = {1} OR {0} = {2}", true, 7, 9), r => 1);
        Assert.That(command.CommandText, Does.Contain(placeholder));
        Assert.That(holder.Parameters.Count, Is.EqualTo(4));
        Assert.That(holder.Parameters[0].ParameterName, Is.EqualTo(parameterName));
        Assert.That(holder.Parameters.Cast<SqliteParameter>().Select(x => x.Value), Is.EqualTo(new[] { boolean, 7, boolean, 9 }));
        Assert.That(reader.IsClosed, Is.True); command.Received().Dispose();
    }

    [Test]
    public void DisposesCommandWhenParameterBindingFails()
    {
        var database = Substitute.For<ITransformationProvider>(); var command = Substitute.For<IDbCommand>();
        database.CreateCommand().Returns(command); database.GenerateParameterName(0).Returns("@p0");
        database.When(x => x.AddQueryParameter(command, 0, 1)).Do(_ => throw new NotSupportedException("binding failure"));
        Assert.Throws<NotSupportedException>(() => database.Query($"SELECT {1}", r => 1));
        command.Received().Dispose(); database.DidNotReceive().ExecuteQuery(Arg.Any<IDbCommand>(), Arg.Any<string>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisposesReaderAndCommandOnExecutionOrMapperFailure(bool mapperFailure)
    {
        var database = Substitute.For<ITransformationProvider>(); var command = Substitute.For<IDbCommand>();
        database.CreateCommand().Returns(command);
        var reader = Substitute.For<IDataReader>(); reader.Read().Returns(true);
        if (mapperFailure) database.ExecuteQuery(command, Arg.Any<string>()).Returns(reader);
        else database.ExecuteQuery(command, Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("driver failure"));
        Assert.Throws<InvalidOperationException>(() => database.Query<int>($"SELECT 1", r => throw new InvalidOperationException("mapper failure")));
        command.Received().Dispose();
        if (mapperFailure) reader.Received().Dispose();
    }
}
