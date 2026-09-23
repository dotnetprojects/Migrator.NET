using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests;

public class DataRecordConversionTests
{
    private static T Read<T>(object value, Func<T> fallback = null)
    {
        using var table = new DataTable();
        table.Columns.Add("Value", typeof(object));
        table.Rows.Add(value);
        using var reader = table.CreateDataReader();
        Assert.That(reader.Read(), Is.True);
        return fallback == null ? reader.TryParse<T>("Value") : reader.TryParse("Value", fallback);
    }

    [Test]
    public void DatabaseNullUsesLazyFallbackAndPreservesNullableDefaults()
    {
        Assert.That(Read<int?>(DBNull.Value), Is.Null);
        Assert.That(Read<int>(DBNull.Value), Is.Zero);
        Assert.That(Read<string>(DBNull.Value), Is.Null);
        var calls = 0;
        Assert.That(Read(DBNull.Value, () => { calls++; return 42; }), Is.EqualTo(42));
        Assert.That(Read(7, () => { calls++; return -1; }), Is.EqualTo(7));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void NumericAndDateConversionsPreserveNullableAndNonNullableValues()
    {
        Assert.That(Read<int>("2147483647"), Is.EqualTo(int.MaxValue));
        Assert.That(Read<int?>((short)-7), Is.EqualTo(-7));
        Assert.That(Read<long>("9223372036854775807"), Is.EqualTo(long.MaxValue));
        Assert.That(Read<long?>(int.MinValue), Is.EqualTo((long)int.MinValue));
        var date = new DateTime(2024, 2, 29, 12, 34, 56);
        Assert.That(Read<DateTime>(date), Is.EqualTo(date));
        Assert.That(Read<DateTime?>(date), Is.EqualTo(date));
        Assert.That(Read<string>(123), Is.EqualTo("123"));
        Assert.That(Read<decimal>(12.5m), Is.EqualTo(12.5m));
    }

    [Test]
    public void GuidConversionAcceptsDatabaseBinaryAndTextRepresentations()
    {
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        Assert.That(Read<Guid>(guid.ToByteArray()), Is.EqualTo(guid));
        Assert.That(Read<Guid?>(guid.ToString()), Is.EqualTo(guid));
        Assert.That(Read<Guid>(guid), Is.EqualTo(guid));
        Assert.Throws<FormatException>(() => Read<Guid>("invalid-guid"));
        Assert.Throws<ArgumentException>(() => Read<Guid>(new byte[15]));
    }

    [TestCase(0, false)]
    [TestCase(-2, true)]
    [TestCase(0L, false)]
    [TestCase(2L, true)]
    [TestCase((short)-1, true)]
    [TestCase((ushort)0, false)]
    [TestCase(1U, true)]
    [TestCase(0UL, false)]
    [TestCase("TRUE", true)]
    [TestCase("false", false)]
    [TestCase(true, true)]
    public void BooleanConversionSupportsDriverRepresentations(object input, bool expected)
    {
        Assert.That(Read<bool>(input), Is.EqualTo(expected));
        Assert.That(Read<bool?>(input), Is.EqualTo(expected));
    }

    [Test]
    public void InvalidConversionsDoNotSilentlyReturnDefaults()
    {
        Assert.Throws<OverflowException>(() => Read<int>(long.MaxValue));
        Assert.Throws<FormatException>(() => Read<int>("not a number"));
        var error = Assert.Throws<MigrationException>(() => Read<TimeSpan>("not a duration"));
        Assert.That(error.InnerException, Is.TypeOf<InvalidCastException>());
        Assert.That(error.Message, Does.Contain("Value").And.Contain("System.TimeSpan"));
    }
}
