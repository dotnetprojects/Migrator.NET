using System;
using System.ComponentModel.DataAnnotations.Schema;
using DtoColumn = System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.Hana;
using DotNetProjects.Migrator.Providers.Impl.Informix;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("SQLite")]
public class DtoSelectTests
{
    private SqliteConnection connection;
    private SQLiteTransformationProvider provider;
    public enum State { New = 1, Archived = 2 }

    public class ScreenDto
    {
        public int Id { get; set; }
        public bool Archive { get; set; }
        [DtoColumn("GenericDefinition")] public string Definition { get; set; }
        public int? Optional { get; set; }
        public State State { get; set; }
        [NotMapped] public string Display => $"{Id}: {Definition}";
        [NotMapped] public object Ignored { get; set; }
    }

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        provider = new SQLiteTransformationProvider(new SQLiteDialect(), connection, "default", null);
        provider.ExecuteNonQuery("CREATE TABLE Common_Screens (Id INTEGER, Archive INTEGER, GenericDefinition TEXT, Optional INTEGER, State INTEGER)");
        provider.Insert("Common_Screens", new[] { "Id", "Archive", "GenericDefinition", "Optional", "State" }, new object[] { 1, false, "O'Brien", null, 1 });
        provider.Insert("Common_Screens", new[] { "Id", "Archive", "GenericDefinition", "Optional", "State" }, new object[] { 2, true, "other", 7, 2 });
        provider.Insert("Common_Screens", new[] { "Id", "Archive", "GenericDefinition", "Optional", "State" }, new object[] { 3, false, null, 9, 1 });
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    [Test]
    public void MapsNamedColumnsNullableNumbersBooleansAndEnumsThroughInterface()
    {
        ITransformationProvider database = provider;
        var rows = database.Select<ScreenDto>("Common_Screens");
        Assert.That(rows.Count, Is.EqualTo(3));
        var first = rows.Single(x => x.Id == 1);
        Assert.Multiple(() =>
        {
            Assert.That(first.Definition, Is.EqualTo("O'Brien"));
            Assert.That(first.Optional, Is.Null);
            Assert.That(first.Archive, Is.False);
            Assert.That(first.State, Is.EqualTo(State.New));
            Assert.That(rows.Single(x => x.Id == 2).Optional, Is.EqualTo(7));
            Assert.That(rows.Single(x => x.Id == 3).Definition, Is.Null);
        });
    }

    [Test]
    public void TranslatesCapturedValuesBooleanLogicAndGrouping()
    {
        var minimumId = 1;
        var definition = "O'Brien";
        var rows = provider.Select<ScreenDto>("Common_Screens", x =>
            !x.Archive && (x.Id > minimumId || x.Definition == definition));
        Assert.That(rows.Select(x => x.Id), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [TestCase("equal", 2)]
    [TestCase("not-equal", 1, 3)]
    [TestCase("less", 1)]
    [TestCase("less-equal", 1, 2)]
    [TestCase("greater", 3)]
    [TestCase("greater-equal", 2, 3)]
    [TestCase("reversed", 3)]
    [TestCase("null", 1)]
    [TestCase("not-null", 2, 3)]
    [TestCase("captured-null", 1)]
    [TestCase("enum", 2)]
    public void SupportsComparisonsAndNullChecks(string operation, params int[] expected)
    {
        int? missing = null;
        State state = State.Archived;
        Expression<Func<ScreenDto, bool>> predicate = operation switch
        {
            "equal" => x => x.Id == 2,
            "not-equal" => x => x.Id != 2,
            "less" => x => x.Id < 2,
            "less-equal" => x => x.Id <= 2,
            "greater" => x => x.Id > 2,
            "greater-equal" => x => x.Id >= 2,
            "reversed" => x => 2 < x.Id,
            "null" => x => x.Optional == null,
            "not-null" => x => x.Optional != null,
            "captured-null" => x => x.Optional == missing,
            _ => x => x.State == state
        };
        Assert.That(provider.Select<ScreenDto>("Common_Screens", predicate).Select(x => x.Id), Is.EquivalentTo(expected));
    }

    [Test]
    public void ValuesCannotChangeSqlStructure()
    {
        var injection = "'; DROP TABLE Common_Screens; --";
        Assert.That(provider.Select<ScreenDto>("Common_Screens", x => x.Definition == injection), Is.Empty);
        Assert.That(provider.TableExists("Common_Screens"), Is.True);
    }

    [Test]
    public void ComparisonsKeepSqlNullSemanticsAndAllowExplicitNullInclusion()
    {
        Assert.That(provider.Select<ScreenDto>("Common_Screens", x => x.Optional != 7).Select(x => x.Id), Is.EqualTo(new[] { 3 }));
        Assert.That(provider.Select<ScreenDto>("Common_Screens", x => x.Optional == null || x.Optional != 7).Select(x => x.Id), Is.EquivalentTo(new[] { 1, 3 }));
    }

    [Test]
    public void SupportsLosslessIntegralPromotions()
    {
        long threshold = 2;
        Assert.That(provider.Select<ScreenDto>("Common_Screens", x => x.Id > threshold).Single().Id, Is.EqualTo(3));
    }

    public class QuotedDto { [DtoColumn("select")] public int Id { get; set; } }

    public class BaseDto { public int Id { get; set; } }
    public class DerivedDto : BaseDto { public string GenericDefinition { get; set; } }

    [Test]
    public void MapsAndFiltersInheritedDtoProperties()
    {
        var row = provider.Select<DerivedDto>("Common_Screens", x => x.Id == 1).Single();
        Assert.That(row.Id, Is.EqualTo(1));
        Assert.That(row.GenericDefinition, Is.EqualTo("O'Brien"));
    }

    [Test]
    public void QuotesTableAndMappedColumnIdentifiers()
    {
        provider.ExecuteNonQuery("CREATE TABLE \"table with spaces\" (\"select\" INTEGER)");
        provider.ExecuteNonQuery("INSERT INTO \"table with spaces\" VALUES (4)");
        Assert.That(provider.Select<QuotedDto>("table with spaces", x => x.Id == 4).Single().Id, Is.EqualTo(4));
    }

    [Test]
    public void UsesTheActiveMigrationTransactionAndReturnsMaterializedRows()
    {
        provider.BeginTransaction();
        provider.Update("Common_Screens", new[] { "GenericDefinition" }, new object[] { "uncommitted" }, new[] { "Id" }, new object[] { 1 });
        var rows = provider.Select<ScreenDto>("Common_Screens", x => x.Id == 1);
        provider.Rollback();
        Assert.That(rows.Single().Definition, Is.EqualTo("uncommitted"));
        Assert.That(provider.Select<ScreenDto>("Common_Screens", x => x.Id == 1).Single().Definition, Is.EqualTo("O'Brien"));
    }

    [TestCase("method")]
    [TestCase("column-column")]
    [TestCase("arithmetic")]
    [TestCase("unmapped")]
    [TestCase("static-property")]
    [TestCase("narrowing")]
    public void RejectsUnsupportedPredicatesBeforeCreatingACommand(string kind)
    {
        var database = Substitute.For<ITransformationProvider>();
        Expression<Func<ScreenDto, bool>> predicate = kind switch
        {
            "method" => x => x.Definition.StartsWith("O"),
            "column-column" => x => x.Id == x.Optional,
            "arithmetic" => x => x.Id + 1 == 2,
            "unmapped" => x => x.Display == "1",
            "static-property" => x => x.Id == DateTime.Now.Day,
            _ => x => (byte)x.Id == 1
        };
        Assert.Throws<NotSupportedException>(() => database.Select("Common_Screens", predicate));
        database.DidNotReceive().CreateCommand();
    }

    public class EmptyDto { public int ReadOnly => 0; }
    public class UnsupportedDto { public object Value { get; set; } }
    public class DuplicateDto { [DtoColumn("Id")] public int A { get; set; } [DtoColumn("id")] public int B { get; set; } }

    [Test]
    public void RejectsInvalidMappingsAndTables()
    {
        Assert.Throws<ArgumentException>(() => provider.Select<EmptyDto>("Common_Screens"));
        Assert.Throws<ArgumentException>(() => provider.Select<UnsupportedDto>("Common_Screens"));
        Assert.Throws<ArgumentException>(() => provider.Select<DuplicateDto>("Common_Screens"));
        Assert.Throws<ArgumentException>(() => provider.Select<ScreenDto>(" "));
    }

    public class RequiredDto { [DtoColumn("Optional")] public int Value { get; set; } }
    public class WrongTypeDto { [DtoColumn("GenericDefinition")] public int Value { get; set; } }

    [Test]
    public void MappingErrorsNameTheColumnAndDtoProperty()
    {
        var nullError = Assert.Throws<DataException>(() => provider.Select<RequiredDto>("Common_Screens"));
        Assert.That(nullError.Message, Does.Contain("Optional").And.Contain("RequiredDto.Value"));
        var typeError = Assert.Throws<DataException>(() => provider.Select<WrongTypeDto>("Common_Screens"));
        Assert.That(typeError.Message, Does.Contain("GenericDefinition").And.Contain("WrongTypeDto.Value"));
        // A failed materialization leaves the connection usable.
        Assert.That(provider.Select<ScreenDto>("Common_Screens").Count, Is.EqualTo(3));
    }

    public class ConversionDto
    {
        public Guid Key { get; set; }
        public DateTime Created { get; set; }
        public TimeOnly Time { get; set; }
        public TimeSpan Duration { get; set; }
        public byte[] Data { get; set; }
        public decimal Amount { get; set; }
    }

    [Test]
    public void MapsProviderStorageRepresentations()
    {
        var key = Guid.NewGuid();
        provider.ExecuteNonQuery("CREATE TABLE Converted (Key BLOB, Created TEXT, Time TEXT, Duration INTEGER, Data BLOB, Amount NUMERIC)");
        provider.Insert("Converted", new[] { "Key", "Created", "Time", "Duration", "Data", "Amount" },
            new object[] { key, "2026-10-14T10:30:00Z", new TimeOnly(10, 30), TimeSpan.FromSeconds(2), new byte[] { 1, 2 }, 12.5m });
        var row = provider.Select<ConversionDto>("Converted", x => x.Key == key).Single();
        Assert.Multiple(() =>
        {
            Assert.That(row.Key, Is.EqualTo(key));
            Assert.That(row.Created, Is.EqualTo(new DateTime(2026, 10, 14, 10, 30, 0, DateTimeKind.Utc)));
            Assert.That(row.Time, Is.EqualTo(new TimeOnly(10, 30)));
            Assert.That(row.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(row.Data, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(row.Amount, Is.EqualTo(12.5m));
        });
    }
}

public class DtoSelectBindingTests
{
    public class Row { public int Id { get; set; } public bool Active { get; set; } }

    [TestCase("oracle", ":p0", "p0", "p1", 1, false)]
    [TestCase("hana", "?", "p0", "p1", true, false)]
    [TestCase("informix", "?", "@p0", "@p1", true, false)]
    [TestCase("oracle", ":p0", "p0", "p1", 1, true)]
    [TestCase("hana", "?", "p0", "p1", true, true)]
    [TestCase("informix", "?", "@p0", "@p1", true, true)]
    public void PreservesProviderParameterNamesConversionsAndOccurrenceOrder(string kind, string placeholder,
        string firstName, string secondName, object boolean, bool useFilter)
    {
        var connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open);
        var command = Substitute.For<IDbCommand>();
        connection.CreateCommand().Returns(command);
        using var holder = new SqliteCommand();
        command.Parameters.Returns(holder.Parameters);
        command.CreateParameter().Returns(_ => new SqliteParameter());
        using var data = new DataTable();
        data.Columns.Add("Id", typeof(int)); data.Columns.Add("Active", typeof(bool));
        using var reader = data.CreateDataReader();
        command.ExecuteReader().Returns(reader);
        using TransformationProvider provider = kind switch
        {
            "oracle" => new OracleTransformationProvider(new OracleDialect(), connection, null, null, null),
            "hana" => new HanaTransformationProvider(new HanaDialect(), connection, null, null),
            _ => new InformixTransformationProvider(new InformixDialect(), connection, null, null)
        };
        if (useFilter)
            provider.Select("Rows", Filter<Row>.GreaterThan(x => x.Id, 5).And(Filter<Row>.Equal(x => x.Active, true)));
        else
            provider.Select<Row>("Rows", x => x.Id > 5 && x.Active);
        Assert.Multiple(() =>
        {
            Assert.That(command.CommandText, Does.Contain(placeholder));
            Assert.That(holder.Parameters.Count, Is.EqualTo(2));
            Assert.That(holder.Parameters[0].ParameterName, Is.EqualTo(firstName));
            Assert.That(holder.Parameters[0].Value, Is.EqualTo(5));
            Assert.That(holder.Parameters[1].ParameterName, Is.EqualTo(secondName));
            Assert.That(holder.Parameters[1].Value, Is.EqualTo(boolean));
            Assert.That(reader.IsClosed, Is.True);
        });
        command.Received().Dispose();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisposesResourcesOnExecutionOrMappingFailure(bool mappingFailure)
    {
        var database = Substitute.For<ITransformationProvider>();
        var command = Substitute.For<IDbCommand>();
        database.CreateCommand().Returns(command);
        database.QuoteColumnNameIfRequired(Arg.Any<string>()).Returns(x => (string)x[0]);
        database.QuoteTableNameIfRequired(Arg.Any<string>()).Returns(x => (string)x[0]);
        using var data = new DataTable(); data.Columns.Add("Id", typeof(int)); data.Columns.Add("Active", typeof(bool));
        data.Rows.Add(DBNull.Value, true);
        using var reader = data.CreateDataReader();
        if (mappingFailure) database.ExecuteQuery(command, Arg.Any<string>()).Returns(reader);
        else database.ExecuteQuery(command, Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("driver failure"));
        Assert.Catch(() => database.Select<Row>("Rows"));
        command.Received().Dispose();
        if (mappingFailure) Assert.That(reader.IsClosed, Is.True);
        else reader.Dispose();
    }
}
