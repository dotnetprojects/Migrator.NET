using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;
using Sap.Data.Hana;

namespace Migrator.Tests.Providers.Live;

// Every case is assigned to exactly one existing CI database job. No catch-and-skip.
[TestFixture("SQLite", ProviderTypes.SQLite, Category = "SQLite")]
[TestFixture("SQLServer", ProviderTypes.SqlServer, Category = "SQLServer")]
[TestFixture("PostgreSQL", ProviderTypes.PostgreSQL, Category = "PostgreSQL")]
[TestFixture("Oracle", ProviderTypes.Oracle, Category = "Oracle")]
[TestFixture("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
[TestFixture("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
[TestFixture("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
[TestFixture("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
[TestFixture("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
[TestFixture("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
[TestFixture("Hana", ProviderTypes.Hana, Category = "Hana")]
[NonParallelizable]
public class DataBoundaryTests(string database, ProviderTypes providerType) : TransformationProviderBase
{
    private LiveDatabaseTests live;
    private HanaConnection hana;
    private string schema;

    [SetUp]
    public async Task SetUp()
    {
        switch (database)
        {
            case "SQLite": await BeginSQLiteTransactionAsync(); break;
            case "SQLServer": await BeginSQLServerTransactionAsync(); break;
            case "PostgreSQL": await BeginPostgreSQLTransactionAsync(); break;
            case "Oracle": await BeginOracleTransactionAsync(); break;
            case "Hana":
                hana = new HanaConnection(Environment.GetEnvironmentVariable("MIGRATOR_HANA")
                    ?? "Server=localhost:39041;UserID=SYSTEM;Password=MgT9ci7Q4xZ2");
                hana.Open();
                var name = "BOUNDARY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
                using (var command = hana.CreateCommand())
                {
                    command.CommandText = "CREATE SCHEMA " + name;
                    command.ExecuteNonQuery();
                    schema = name;
                    command.CommandText = "SET SCHEMA " + schema;
                    command.ExecuteNonQuery();
                }
                Provider = ProviderFactory.Create(providerType, hana, schema, "boundary-tests");
                break;
            default:
                live = new LiveDatabaseTests(database, providerType);
                live.SetUp();
                Provider = live.Provider;
                break;
        }
        // A too-long value must not silently truncate on engines with configurable modes.
        if (database is "MySQL" or "MariaDB") Provider.ExecuteNonQuery("SET SESSION sql_mode='STRICT_ALL_TABLES'");
        if (database == "Sybase")
        {
            Provider.ExecuteNonQuery("SET STRING_RTRUNCATION ON");
            Provider.ExecuteNonQuery("SET TEXTSIZE 2147483647");
        }
    }

    [TearDown]
    public override void TearDown()
    {
        try
        {
            if (live != null) live.TearDown();
            else if (hana != null)
            {
                Provider?.Dispose();
                if (schema != null && hana.State == ConnectionState.Open)
                {
                    using var command = hana.CreateCommand();
                    command.CommandText = "DROP SCHEMA " + schema + " CASCADE";
                    command.ExecuteNonQuery();
                }
            }
            else base.TearDown();
        }
        finally
        {
            if (live == null) Provider?.Dispose();
            hana?.Dispose();
            Provider = null;
            live = null;
            hana = null;
            schema = null;
        }
    }

    private string Table => Provider.QuoteTableNameIfRequired("Test");
    private string ValueColumn => Provider.QuoteColumnNameIfRequired("payload");
    private string IdColumn => Provider.QuoteColumnNameIfRequired("id");
    private object Read(int id) => Provider.ExecuteScalar($"SELECT {ValueColumn} FROM {Table} WHERE {IdColumn}={id}");
    private void Create(Column column) => Provider.AddTable("Test", new Column("id", DbType.Int32), column);
    private void Insert(int id, object value) => Provider.Insert("Test", ["id", "payload"], [id, value]);
    private void AssertDatabaseError(TestDelegate action)
    {
        if (live != null) live.AssertDatabaseError(action);
        else Assert.Catch<DbException>(action);
    }

    [Test]
    public void EveryDeclaredTypeHasAnExplicitSchemaContract([Values] MigratorDbType type)
    {
        var column = new Column("payload", type);
        if (type is MigratorDbType.AnsiString or MigratorDbType.String or MigratorDbType.AnsiStringFixedLength or MigratorDbType.StringFixedLength)
            column.Size = 32;
        if (!DataTypeContract.Supports(providerType, type))
        {
            Assert.Throws<ArgumentException>(() => Create(column), $"{database}: {type} must be rejected before executing DDL");
            Assert.That(Provider.TableExists("Test"), Is.False);
            return;
        }
        // ASE BIT columns cannot be nullable. Test the engine's explicit contract.
        if (database == "Sybase" && type == MigratorDbType.Boolean) column.IsNullable = false;
        Create(column);
        Assert.That(Provider.ColumnExists("Test", "payload"), Is.True);
        Assert.That(Provider.ReadLegacyColumns("Test").Select(c => c.Name.ToLowerInvariant()), Is.EquivalentTo(new[] { "id", "payload" }));
        if (database == "Sybase" && type == MigratorDbType.Boolean)
        {
            Insert(1, false);
            Assert.That(Convert.ToBoolean(Read(1)), Is.False);
        }
        else
        {
            Insert(1, DBNull.Value);
            Assert.That(Read(1), Is.EqualTo(DBNull.Value));
        }
    }

    [TestCase(DbType.Int16)]
    [TestCase(DbType.Int32)]
    [TestCase(DbType.Int64)]
    [TestCase(DbType.Byte)]
    public void IntegerExtremesSurviveInsertAndUpdate(DbType type)
    {
        Create(new Column("payload", type));
        object[] values = type switch
        {
            DbType.Int16 => [short.MinValue, (short)-1, (short)0, short.MaxValue],
            DbType.Int32 => [int.MinValue, -1, 0, int.MaxValue],
            DbType.Int64 => [long.MinValue, -1L, 0L, long.MaxValue],
            _ => [(byte)0, (byte)1, (byte)254, byte.MaxValue]
        };
        // Informix reserves the most-negative signed value for its NULL encoding.
        object reservedMinimum = null;
        if (database == "Informix" && type != DbType.Byte)
        {
            reservedMinimum = values[0];
            values[0] = type switch
            {
                DbType.Int16 => (object)(short)(short.MinValue + 1),
                DbType.Int32 => int.MinValue + 1,
                _ => long.MinValue + 1
            };
        }
        for (var i = 0; i < values.Length; i++)
        {
            Insert(i, values[i]);
            Assert.That(Convert.ToDecimal(Read(i)), Is.EqualTo(Convert.ToDecimal(values[i])));
        }
        Provider.Update("Test", ["payload"], [values[^1]], $"{IdColumn}=0");
        Assert.That(Convert.ToDecimal(Read(0)), Is.EqualTo(Convert.ToDecimal(values[^1])));
        if (reservedMinimum != null) AssertDatabaseError(() => Insert(99, reservedMinimum));
    }

    [TestCase(DbType.Decimal)]
    [TestCase(DbType.Currency)]
    public void DecimalFractionsAreNotRoundedToIntegers(DbType type)
    {
        var column = new Column("payload", type);
        if (type == DbType.Decimal) { column.Precision = 12; column.Scale = 4; }
        Create(column);
        decimal[] values = [-1234567.8901m, -0.0001m, 0m, 0.0001m, 1234567.8901m];
        for (var i = 0; i < values.Length; i++)
        {
            Insert(i, values[i]);
            Assert.That(Convert.ToDecimal(Read(i), CultureInfo.InvariantCulture), Is.EqualTo(values[i]));
        }
    }

    [TestCase(DbType.Single)]
    [TestCase(DbType.Double)]
    public void FloatingPointSignsAndFractionsRoundTrip(DbType type)
    {
        Create(new Column("payload", type));
        double[] values = [-12345.125, -0.125, 0, 0.125, 12345.125];
        for (var i = 0; i < values.Length; i++)
        {
            object value = type == DbType.Single ? (object)(float)values[i] : values[i];
            Insert(i, value);
            // MySQL's text protocol formats FLOAT with limited significant digits.
            // Read its stored value as DOUBLE to distinguish formatting from data loss.
            var stored = database is "MySQL" or "MariaDB" && type == DbType.Single
                ? Provider.ExecuteScalar($"SELECT {ValueColumn} + 0e0 FROM {Table} WHERE {IdColumn}={i}")
                : Read(i);
            Assert.That(Convert.ToDouble(stored), Is.EqualTo(values[i]).Within(0.000001));
        }
    }

    [TestCase(DbType.String, 1)]
    [TestCase(DbType.String, 32)]
    [TestCase(DbType.String, 255)]
    [TestCase(DbType.String, 256)]
    [TestCase(DbType.String, 2000)]
    [TestCase(DbType.String, 4000)]
    [TestCase(DbType.AnsiString, 1)]
    [TestCase(DbType.AnsiString, 255)]
    [TestCase(DbType.AnsiString, 256)]
    [TestCase(DbType.AnsiString, 2000)]
    public void RequestedStringCapacityPreservesEntireValue(DbType type, int length)
    {
        Create(new Column("payload", type, length));
        var value = new string('x', length - 1) + "!";
        Insert(1, value);
        Assert.That(Read(1), Is.EqualTo(value));
        Insert(2, DBNull.Value);
        Assert.That(Read(2), Is.EqualTo(DBNull.Value));
        Provider.Update("Test", ["payload"], ["z"], $"{IdColumn}=1");
        Assert.That(Read(1), Is.EqualTo("z"));
    }

    [TestCase(DbType.String)]
    [TestCase(DbType.AnsiString)]
    public void MaxLengthSentinelStoresLargeValue(DbType type)
    {
        Create(new Column("payload", type, int.MaxValue));
        // Exceeds varchar(8000), nvarchar(4000), and typical driver text-size defaults.
        var value = new string('x', 70000) + "'tail";
        Insert(1, value);
        if (database == "Informix" && !Equals(Read(1), value)) DiagnoseInformixText(value);
        Assert.That(Read(1), Is.EqualTo(value));
    }

    private void DiagnoseInformixText(string value)
    {
        using (var command = Provider.CreateCommand())
        {
            command.CommandText = $"SELECT {ValueColumn} FROM {Table} WHERE {IdColumn}=1";
            using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
            reader.Read();
            try
            {
                var bytes = new byte[100000];
                var count = (int)reader.GetBytes(0, 0, bytes, 0, bytes.Length);
                TestContext.Progress.WriteLine($"TEXT raw bytes: {count}, suffix={BitConverter.ToString(bytes, Math.Max(0, count - 12), Math.Min(count, 12))}");
            }
            catch (Exception error) { TestContext.Progress.WriteLine($"TEXT raw read: {error.Message}"); }
        }
        var id = 10;
        foreach (var binding in new[] { DbType.String, DbType.AnsiString, DbType.StringFixedLength, DbType.AnsiStringFixedLength, DbType.Binary })
        {
            try
            {
                using var command = Provider.CreateCommand();
                command.CommandText = $"INSERT INTO {Table} ({IdColumn}, {ValueColumn}) VALUES ({id}, ?)";
                var parameter = command.CreateParameter();
                parameter.DbType = binding;
                parameter.Value = binding == DbType.Binary ? (object)System.Text.Encoding.UTF8.GetBytes(value) : value;
                parameter.Size = value.Length + 1;
                command.Parameters.Add(parameter);
                command.ExecuteNonQuery();
                var actual = (string)Read(id);
                TestContext.Progress.WriteLine($"TEXT binding {binding}: length={actual.Length}, exact={actual == value}, suffix={string.Join(",", actual.TakeLast(8).Select(c => (int)c))}");
            }
            catch (Exception error) { TestContext.Progress.WriteLine($"TEXT binding {binding}: {error.Message}"); }
            id++;
        }
    }

    [Test]
    public void BoundedStringOverflowIsExplicit()
    {
        Create(new Column("payload", DbType.String, 8));
        Insert(1, "12345678");
        Assert.That(Read(1), Is.EqualTo("12345678"));
        if (database == "SQLite")
        {
            // SQLite type affinity does not enforce declared string lengths.
            Insert(2, "123456789");
            Assert.That(Read(2), Is.EqualTo("123456789"));
        }
        else if (database == "Informix")
        {
            // Informix accepts this assignment and truncates to the declared width.
            Insert(2, "123456789");
            Assert.That(Read(2), Is.EqualTo("12345678"));
        }
        else AssertDatabaseError(() => Insert(2, "123456789"));
    }

    [Test]
    public void NullEmptyWhitespaceAndSqlPunctuationRemainDistinct()
    {
        Create(new Column("payload", DbType.String, 80));
        object[] values = [DBNull.Value, "", " ", "  x  ", "O'Brien; -- %_\\\r\n"];
        for (var i = 0; i < values.Length; i++)
        {
            Insert(i, values[i]);
            var expected = i == 1 && database == "Oracle" ? DBNull.Value
                : i == 1 && database == "Sybase" ? " " : values[i];
            if (expected is string text && database is "Sybase" or "Informix")
                expected = database == "Sybase" && text.TrimEnd(' ').Length == 0 ? " " : text.TrimEnd(' ');
            Assert.That(Read(i), Is.EqualTo(expected), $"Value {i}");
        }
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar($"SELECT COUNT(*) FROM {Table}")), Is.EqualTo(values.Length));
    }

    [TestCase(0)]
    [TestCase(32)]
    [TestCase(8001)]
    [TestCase(int.MaxValue)]
    public void BinaryZerosAndHighBytesRoundTrip(int size)
    {
        Create(new Column("payload", DbType.Binary, size));
        var bytes = size == int.MaxValue
            ? Enumerable.Range(0, 70000).Select(i => (byte)(i % 256)).ToArray()
            : new byte[] { 0, 1, 39, 127, 128, 254, 255, 0 };
        Insert(1, bytes);
        Assert.That(Read(1), Is.EqualTo(bytes));
        Provider.Update("Test", ["payload"], [new byte[] { 255, 0 }], $"{IdColumn}=1");
        Assert.That(Read(1), Is.EqualTo(new byte[] { 255, 0 }));
        Insert(2, DBNull.Value);
        Assert.That(Read(2), Is.EqualTo(DBNull.Value));
        Provider.Update("Test", ["payload"], [DBNull.Value], $"{IdColumn}=1");
        Assert.That(Read(1), Is.EqualTo(DBNull.Value));
        Insert(3, bytes);
        Provider.Update("Test", ["payload"], [null], ["id"], [3]);
        Assert.That(Read(3), Is.EqualTo(DBNull.Value));
    }

    [Test]
    public void WideningAndRenamingPreserveExistingDataAndNulls()
    {
        Create(new Column("payload", DbType.String, 8));
        Insert(1, "O'Brien");
        Insert(2, DBNull.Value);
        Provider.ChangeColumn("Test", new Column("payload", DbType.String, 256));
        Assert.That(Read(1), Is.EqualTo("O'Brien"));
        Assert.That(Read(2), Is.EqualTo(DBNull.Value));
        var expanded = new string('w', 256);
        Provider.Update("Test", ["payload"], [expanded], $"{IdColumn}=1");
        Provider.RenameColumn("Test", "payload", "renamed_payload");
        Assert.That(Provider.ColumnExists("Test", "payload"), Is.False);
        var renamed = Provider.QuoteColumnNameIfRequired("renamed_payload");
        Assert.That(Provider.ExecuteScalar($"SELECT {renamed} FROM {Table} WHERE {IdColumn}=1"), Is.EqualTo(expanded));
        Assert.That(Provider.ExecuteScalar($"SELECT {renamed} FROM {Table} WHERE {IdColumn}=2"), Is.EqualTo(DBNull.Value));
    }

    [Test]
    public void NotNullIsEnforcedByTheDatabase()
    {
        Create(new Column("payload", DbType.Int32) { IsNullable = false });
        Insert(1, 0);
        Assert.That(Convert.ToInt32(Read(1)), Is.Zero);
        AssertDatabaseError(() => Insert(2, DBNull.Value));
    }

    [TestCase(DbType.AnsiStringFixedLength, 1)]
    [TestCase(DbType.AnsiStringFixedLength, 32)]
    [TestCase(DbType.AnsiStringFixedLength, 255)]
    [TestCase(DbType.StringFixedLength, 1)]
    [TestCase(DbType.StringFixedLength, 32)]
    [TestCase(DbType.StringFixedLength, 255)]
    public void FixedLengthStringsHonorRequestedCapacity(DbType type, int size)
    {
        Create(new Column("payload", type, size));
        var value = new string('f', size - 1) + "!";
        Insert(1, value);
        Assert.That(Read(1), Is.EqualTo(value));
        if (database != "SQLite")
            Assert.That(Provider.ReadLegacyColumns("Test").Single(c => c.Name.Equals("payload", StringComparison.OrdinalIgnoreCase)).Size, Is.EqualTo(size));
    }

    [Test]
    public void AccentedTextIsNotLost()
    {
        Create(new Column("payload", DbType.String, 80));
        // Latin-1 repertoire also works with the legacy ASE/Informix CI encodings.
        var value = "Grüße, déjà vu, mañana";
        Insert(1, value);
        Assert.That(Read(1), Is.EqualTo(value));
    }

    [Test]
    public void BooleanFalseAndTrueRemainDifferent()
    {
        Create(new Column("payload", DbType.Boolean) { IsNullable = false });
        Insert(1, false);
        Insert(2, true);
        Assert.That(Convert.ToBoolean(Read(1)), Is.False);
        Assert.That(Convert.ToBoolean(Read(2)), Is.True);
        Provider.Update("Test", ["payload"], [false], $"{IdColumn}=2");
        Assert.That(Convert.ToBoolean(Read(2)), Is.False);
    }

    [TestCase(DbType.Date)]
    [TestCase(DbType.DateTime)]
    [TestCase(DbType.DateTime2)]
    public void LeapDayAndYearBoundaryRoundTrip(DbType type)
    {
        var column = new Column("payload", type);
        if (!DataTypeContract.Supports(providerType, (MigratorDbType)type))
        {
            Assert.Throws<ArgumentException>(() => Create(column));
            return;
        }
        Create(column);
        var values = new[] { new DateTime(2000, 2, 29), new DateTime(2024, 12, 31), new DateTime(2025, 1, 1) };
        for (var i = 0; i < values.Length; i++)
        {
            if (type != DbType.Date) values[i] = values[i].AddHours(23).AddMinutes(59).AddSeconds(59);
            Insert(i, values[i]);
            Assert.That(Convert.ToDateTime(Read(i), CultureInfo.InvariantCulture), Is.EqualTo(values[i]));
        }
    }

    [Test]
    public void TimeOfDayMidnightAndLastSecondRoundTrip()
    {
        Create(new Column("payload", DbType.Time));
        var values = new[] { TimeOnly.MinValue, new TimeOnly(12, 34, 56), new TimeOnly(23, 59, 59) };
        for (var i = 0; i < values.Length; i++)
        {
            Insert(i, values[i]);
            var actual = Read(i) switch
            {
                DateTime date => TimeOnly.FromDateTime(date),
                TimeSpan span => TimeOnly.FromTimeSpan(span),
                TimeOnly time => time,
                var text => TimeOnly.Parse(Convert.ToString(text, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            };
            Assert.That(actual, Is.EqualTo(values[i]));
        }
    }

    [Test]
    public void DefaultDoesNotReplaceExplicitNullOrExistingValues()
    {
        Create(new Column("payload", DbType.Int32) { DefaultValue = -17 });
        Provider.Insert("Test", ["id"], [1]);
        Insert(2, DBNull.Value);
        Insert(3, 0);
        Assert.That(Convert.ToInt32(Read(1)), Is.EqualTo(-17));
        Assert.That(Read(2), Is.EqualTo(DBNull.Value));
        Assert.That(Convert.ToInt32(Read(3)), Is.Zero);
        Provider.ChangeColumn("Test", new Column("payload", DbType.Int32) { DefaultValue = 29 });
        Provider.Insert("Test", ["id"], [4]);
        Assert.That(Convert.ToInt32(Read(4)), Is.EqualTo(29));
        Assert.That(Convert.ToInt32(Read(1)), Is.EqualTo(-17));
        Assert.That(Read(2), Is.EqualTo(DBNull.Value));
        Assert.That(Convert.ToInt32(Read(3)), Is.Zero);
    }

    [TestCase(MigratorDbType.SByte)]
    [TestCase(MigratorDbType.UInt16)]
    [TestCase(MigratorDbType.UInt32)]
    [TestCase(MigratorDbType.UInt64)]
    public void UnsignedAndSignedByteRangesAreExplicit(MigratorDbType type)
    {
        var column = new Column("payload", type);
        if (!DataTypeContract.Supports(providerType, type))
        {
            Assert.Throws<ArgumentException>(() => Create(column));
            Assert.That(Provider.TableExists("Test"), Is.False);
            return;
        }
        Create(column);
        object[] values = type switch
        {
            MigratorDbType.SByte => [sbyte.MinValue, (sbyte)0, sbyte.MaxValue],
            MigratorDbType.UInt16 => [(ushort)0, (ushort)32768, ushort.MaxValue],
            MigratorDbType.UInt32 => [0U, 2147483648U, uint.MaxValue],
            _ when database == "SQLite" => [0UL, (ulong)long.MaxValue],
            _ => [0UL, (ulong)long.MaxValue + 1, ulong.MaxValue]
        };
        for (var i = 0; i < values.Length; i++)
        {
            Insert(i, values[i]);
            Assert.That(Convert.ToDecimal(Read(i)), Is.EqualTo(Convert.ToDecimal(values[i])));
        }
        if (database == "SQLite" && type == MigratorDbType.UInt64)
            Assert.Throws<OverflowException>(() => Insert(99, ulong.MaxValue), "SQLite INTEGER is signed 64-bit; never wrap or round an out-of-range UInt64.");
    }

    [Test]
    public void DecimalPrecisionBoundaryIsExplicit()
    {
        Create(new Column("payload", DbType.Decimal) { Precision = 12, Scale = 4 });
        Insert(1, 99999999.9999m);
        Insert(2, -99999999.9999m);
        Assert.That(Convert.ToDecimal(Read(1)), Is.EqualTo(99999999.9999m));
        Assert.That(Convert.ToDecimal(Read(2)), Is.EqualTo(-99999999.9999m));
        if (database == "SQLite")
        {
            Insert(3, 100000000m);
            Assert.That(Convert.ToDecimal(Read(3)), Is.EqualTo(100000000m));
        }
        else
        {
            var metadata = Provider.ReadLegacyColumns("Test").Single(c => c.Name.Equals("payload", StringComparison.OrdinalIgnoreCase));
            Assert.That(metadata.Precision, Is.EqualTo(12));
            Assert.That(metadata.Scale, Is.EqualTo(4));
            if (database == "Firebird")
            {
                // DECIMAL precision is a minimum; dialect 3 uses a scaled BIGINT.
                Insert(3, 100000000m);
                Assert.That(Convert.ToDecimal(Read(3)), Is.EqualTo(100000000m));
                Insert(4, 922337203685477.5807m);
                Assert.That(Convert.ToDecimal(Read(4)), Is.EqualTo(922337203685477.5807m));
                // The driver encodes the scaled Int64 and rejects overflow before sending SQL.
                Assert.Throws<OverflowException>(() => Insert(5, 922337203685477.5808m));
            }
            else AssertDatabaseError(() => Insert(3, 100000000m));
        }
    }
}

internal static class DataTypeContract
{
    // Explicit contract, independent of the dialect under test. New enum values fail
    // until deliberately supported or recorded as unsupported here.
    internal static bool Supports(ProviderTypes provider, MigratorDbType type) => type switch
    {
        MigratorDbType.AnsiString or MigratorDbType.Binary or MigratorDbType.Byte or
        MigratorDbType.Boolean or MigratorDbType.Currency or MigratorDbType.Date or
        MigratorDbType.DateTime or MigratorDbType.Decimal or MigratorDbType.Double or
        MigratorDbType.Int16 or MigratorDbType.Int32 or MigratorDbType.Int64 or
        MigratorDbType.Single or MigratorDbType.String or MigratorDbType.Time or
        MigratorDbType.AnsiStringFixedLength or MigratorDbType.StringFixedLength => true,
        MigratorDbType.Guid or MigratorDbType.DateTimeOffset => provider != ProviderTypes.Hana,
        MigratorDbType.DateTime2 => provider != ProviderTypes.Firebird,
        MigratorDbType.SByte => provider == ProviderTypes.SQLite,
        MigratorDbType.UInt16 or MigratorDbType.UInt32 or MigratorDbType.UInt64 =>
            provider is ProviderTypes.SQLite or ProviderTypes.SqlServer or ProviderTypes.PostgreSQL or ProviderTypes.Oracle or ProviderTypes.Mysql or ProviderTypes.MariaDB,
        MigratorDbType.VarNumeric => provider is ProviderTypes.SQLite or ProviderTypes.SqlServer or ProviderTypes.IBM_DB2,
        MigratorDbType.Interval => provider is ProviderTypes.SQLite or ProviderTypes.SqlServer or ProviderTypes.PostgreSQL or ProviderTypes.Oracle or ProviderTypes.Mysql or ProviderTypes.MariaDB,
        MigratorDbType.Json or MigratorDbType.Xml or MigratorDbType.Object => false,
        _ => throw new AssertionException($"Add an explicit data-type contract for {type}")
    };
}
