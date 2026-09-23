using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

public class ProviderCommandContractTests
{
    private sealed class Provider(IDbConnection connection)
        : TransformationProvider(new SqlServerDialect(), connection, "dbo", "default")
    {
        public override List<string> GetDatabases() => new() { "Example", "Archive" };
        public override bool ConstraintExists(string table, string name) => false;
        public override bool IndexExists(string table, string name) => false;
    }

    private IDbConnection connection;
    private IDbCommand command;
    private Provider provider;

    [SetUp]
    public void SetUp()
    {
        connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open);
        command = Substitute.For<IDbCommand>();
        var values = new List<object>();
        var parameters = Substitute.For<IDataParameterCollection>();
        parameters.Add(Arg.Any<object>()).Returns(call => { values.Add(call[0]); return values.Count - 1; });
        parameters[Arg.Any<int>()].Returns(call => values[(int)call[0]]);
        command.Parameters.Returns(parameters);
        command.CreateParameter().Returns(_ => Substitute.For<IDbDataParameter>());
        command.ExecuteNonQuery().Returns(1);
        connection.CreateCommand().Returns(command);
        provider = new Provider(connection) { CommandTimeout = 17 };
    }

    [TearDown]
    public void TearDown() => provider.Dispose();

    private static IEnumerable<TestCaseData> Parameters()
    {
        yield return new TestCaseData(null, DbType.String, DBNull.Value).SetName("Parameter_Null");
        yield return new TestCaseData(DBNull.Value, DbType.String, DBNull.Value).SetName("Parameter_DbNull");
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        yield return new TestCaseData(guid, DbType.Guid, guid);
        yield return new TestCaseData(new byte[] { 0, 255 }, DbType.Binary, new byte[] { 0, 255 });
        yield return new TestCaseData(byte.MaxValue, DbType.Byte, byte.MaxValue);
        yield return new TestCaseData(sbyte.MinValue, DbType.Int16, (short)sbyte.MinValue);
        yield return new TestCaseData(short.MinValue, DbType.Int16, short.MinValue);
        yield return new TestCaseData(int.MinValue, DbType.Int32, int.MinValue);
        yield return new TestCaseData(long.MaxValue, DbType.Int64, long.MaxValue);
        yield return new TestCaseData(ushort.MaxValue, DbType.UInt16, ushort.MaxValue);
        yield return new TestCaseData(uint.MaxValue, DbType.UInt32, uint.MaxValue);
        yield return new TestCaseData(ulong.MaxValue, DbType.UInt64, ulong.MaxValue);
        yield return new TestCaseData(1.25f, DbType.Single, 1.25f);
        yield return new TestCaseData(1.25d, DbType.Double, 1.25d);
        yield return new TestCaseData(decimal.MaxValue, DbType.Decimal, decimal.MaxValue);
        yield return new TestCaseData("O'Brien", DbType.String, "O'Brien");
        var date = new DateTime(2024, 2, 29, 12, 0, 0, DateTimeKind.Utc);
        yield return new TestCaseData(date, DbType.DateTime, date);
        var time = new TimeOnly(23, 59, 59).Add(TimeSpan.FromTicks(1234567));
        yield return new TestCaseData(time, DbType.Time, time.ToTimeSpan());
        yield return new TestCaseData(TimeSpan.FromHours(-51), DbType.Int64, TimeSpan.FromHours(-51).Ticks);
        var offset = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.FromHours(5.5));
        yield return new TestCaseData(offset, DbType.DateTimeOffset, offset.ToUniversalTime());
        yield return new TestCaseData(true, DbType.Boolean, true);
    }

    [TestCaseSource(nameof(Parameters))]
    public void WritesBindTypedParametersWithoutInterpolatingValues(object input, DbType type, object expected)
    {
        Assert.That(provider.ExecuteNonQuery("INSERT INTO [dbo].[Items] ([Value]) VALUES (@p0)", 17, new[] { input }), Is.EqualTo(1));
        Assert.That(command.CommandText, Is.EqualTo("INSERT INTO [dbo].[Items] ([Value]) VALUES (@p0)"));
        var parameter = (IDbDataParameter)command.Parameters[0];
        Assert.That(parameter.ParameterName, Is.EqualTo("@p0"));
        if (expected != DBNull.Value) Assert.That(parameter.DbType, Is.EqualTo(type));
        Assert.That(parameter.Value, Is.EqualTo(expected));
        Assert.That(command.CommandTimeout, Is.EqualTo(17));
        command.Received(1).Dispose();
    }

    [Test]
    public void UnsupportedParameterFailsBeforeExecutionAndDisposesCommand()
    {
        Assert.Throws<NotSupportedException>(() => provider.Insert("Items", new[] { "Value" }, new object[] { new Version(1, 2) }));
        command.DidNotReceive().ExecuteNonQuery();
        command.Received(1).Dispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CommandsEnlistInTransactionAndCompleteExactlyOnce(bool commit)
    {
        var transaction = Substitute.For<IDbTransaction>();
        connection.BeginTransaction(IsolationLevel.ReadCommitted).Returns(transaction);
        provider.BeginTransaction();
        provider.BeginTransaction();
        provider.Insert("Items", new[] { "Value" }, new object[] { 1 });
        Assert.That(command.Transaction, Is.SameAs(transaction));
        if (commit) { provider.Commit(); provider.Commit(); transaction.Received(1).Commit(); transaction.DidNotReceive().Rollback(); }
        else { provider.Rollback(); provider.Rollback(); transaction.Received(1).Rollback(); transaction.DidNotReceive().Commit(); }
        transaction.Received(1).Dispose();
        connection.Received(1).BeginTransaction(IsolationLevel.ReadCommitted);
        Assert.That(provider.HasActiveTransaction, Is.False);
    }

    [Test]
    public void DisposeRollsBackOutstandingWorkButLeavesBorrowedConnectionOpen()
    {
        var transaction = Substitute.For<IDbTransaction>();
        connection.BeginTransaction(IsolationLevel.ReadCommitted).Returns(transaction);
        provider.BeginTransaction();
        provider.Dispose();
        provider.Dispose();
        transaction.Received(1).Rollback();
        transaction.Received(1).Dispose();
        connection.DidNotReceive().Dispose();
        connection.DidNotReceive().Close();
    }

    [Test]
    public void FailedNonQueryPreservesCauseAndReleasesCommand()
    {
        var cause = new InvalidOperationException("database failure");
        command.ExecuteNonQuery().Returns(_ => throw cause);
        var error = Assert.Throws<MigrationException>(() => provider.ExecuteNonQuery("UPDATE Items SET Value=@p0", 9, 42));
        Assert.That(error.InnerException, Is.SameAs(cause));
        Assert.That(command.CommandTimeout, Is.EqualTo(9));
        Assert.That(((IDataParameter)command.Parameters[0]).Value, Is.EqualTo(42));
        command.Received(1).Dispose();
    }

    [Test]
    public void FailedScalarPreservesTheOriginalExceptionAndReleasesCommand()
    {
        var cause = new InvalidOperationException("database failure");
        command.ExecuteScalar().Returns(_ => throw cause);
        Assert.That(Assert.Throws<InvalidOperationException>(() => provider.ExecuteScalar("SELECT 1")), Is.SameAs(cause));
        command.Received(1).Dispose();
    }

    [Test]
    public void GenericColumnMetadataPreservesOrderNullabilityAndReleasesReader()
    {
        using var table = new DataTable();
        table.Columns.Add("COLUMN_NAME"); table.Columns.Add("IS_NULLABLE");
        table.Rows.Add("First", "NO"); table.Rows.Add("Second", "YES");
        using var reader = table.CreateDataReader();
        command.ExecuteReader().Returns(reader);
#pragma warning disable CS0618
        var columns = provider.GetColumns("Items");
#pragma warning restore CS0618
        Assert.That(columns.Select(c => c.Name), Is.EqualTo(new[] { "First", "Second" }));
        Assert.That(columns.Select(c => c.IsNullable), Is.EqualTo(new[] { false, true }));
        Assert.That(reader.IsClosed, Is.True);
        command.Received(1).Dispose();
    }

    [Test]
    public void GenericConstraintEnumerationDisposesItsReader()
    {
        using var table = new DataTable(); table.Columns.Add("CONSTRAINT_NAME");
        table.Rows.Add("PK_Items"); table.Rows.Add("CK_Positive");
        using var reader = table.CreateDataReader(); command.ExecuteReader().Returns(reader);
        Assert.That(provider.GetConstraints("Items"), Is.EqualTo(new[] { "PK_Items", "CK_Positive" }));
        Assert.That(reader.IsClosed, Is.True);
        command.Received(1).Dispose();
    }

    [Test]
    public void DatabaseLookupIsCaseInsensitiveAndSwitchUsesTheConnection()
    {
        Assert.That(provider.DatabaseExists("eXAMPLE"), Is.True);
        Assert.That(provider.DatabaseExists("Missing"), Is.False);
        provider.SwitchDatabase("Archive");
        connection.Received(1).ChangeDatabase("Archive");
        command.DidNotReceive().ExecuteNonQuery();
    }
}
