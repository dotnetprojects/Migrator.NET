using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Live;

[NonParallelizable]
public class LiveMetadataRegressionTests
{
    [Test, Category("Sybase")]
    public void SybaseLargeTextMetadataPreservesCapacity() => new LiveDatabaseTests("Sybase", ProviderTypes.Sybase).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (text_value TEXT NULL, unicode_value UNITEXT NULL)");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Select(c => c.Type), Is.EqualTo(new[] { DbType.String, DbType.String }));
        Assert.That(columns.Select(c => c.Size), Is.EqualTo(new[] { int.MaxValue, int.MaxValue }));
        f.Provider.AddTable("copied_values", columns);
        Assert.That(f.Provider.GetColumns("copied_values").Select(c => c.Size), Is.EqualTo(new[] { int.MaxValue, int.MaxValue }));
        var content = new string('x', 5000);
        f.Provider.Insert("copied_values", ["text_value", "unicode_value"], [content, content]);
        Assert.That(f.Provider.ExecuteScalar("SELECT text_value FROM copied_values"), Is.EqualTo(content));
        Assert.That(f.Provider.ExecuteScalar("SELECT unicode_value FROM copied_values"), Is.EqualTo(content));
    });

    [Test, Category("Informix")]
    public void InformixTypedDefaultsSurviveMetadataCopy() => new LiveDatabaseTests("Informix", ProviderTypes.IBM_Informix).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (id INTEGER, amount INTEGER DEFAULT 7, price DECIMAL(12,3) DEFAULT 12.345, enabled BOOLEAN DEFAULT 't', disabled BOOLEAN DEFAULT 'f', label VARCHAR(40) DEFAULT ' O''Brien', stamp DATETIME YEAR TO FRACTION(5) DEFAULT CURRENT YEAR TO FRACTION(5), today_value DATE DEFAULT TODAY, null_value INTEGER DEFAULT NULL)");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Single(c => c.Name == "amount").DefaultValue, Is.TypeOf<int>().And.EqualTo(7));
        Assert.That(columns.Single(c => c.Name == "price").DefaultValue, Is.TypeOf<decimal>().And.EqualTo(12.345m));
        Assert.That(columns.Single(c => c.Name == "enabled").DefaultValue, Is.TypeOf<bool>().And.EqualTo(true));
        Assert.That(columns.Single(c => c.Name == "disabled").DefaultValue, Is.TypeOf<bool>().And.EqualTo(false));
        Assert.That(columns.Single(c => c.Name == "label").DefaultValue, Is.EqualTo(" O'Brien"));
        Assert.That(columns.Single(c => c.Name == "null_value").DefaultValue, Is.Null);
        f.Provider.AddTable("copied_values", columns);
        f.Provider.Insert("copied_values", ["id"], [1]);
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT amount FROM copied_values")), Is.EqualTo(7));
        Assert.That(Convert.ToDecimal(f.Provider.ExecuteScalar("SELECT price FROM copied_values")), Is.EqualTo(12.345m));
        Assert.That(Convert.ToBoolean(f.Provider.ExecuteScalar("SELECT enabled FROM copied_values")), Is.True);
        Assert.That(Convert.ToBoolean(f.Provider.ExecuteScalar("SELECT disabled FROM copied_values")), Is.False);
        Assert.That(f.Provider.ExecuteScalar("SELECT label FROM copied_values"), Is.EqualTo(" O'Brien"));
        Assert.That(f.Provider.ExecuteScalar("SELECT stamp FROM copied_values"), Is.Not.Null.And.Not.EqualTo(DBNull.Value));
        Assert.That(f.Provider.ExecuteScalar("SELECT today_value FROM copied_values"), Is.Not.Null.And.Not.EqualTo(DBNull.Value));
        Assert.That(f.Provider.ExecuteScalar("SELECT null_value FROM copied_values"), Is.EqualTo(DBNull.Value));
    });

    [TestCase("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
    [TestCase("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
    [TestCase("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
    public void ChangeColumnCreatesRequestedUniqueConstraint(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        f.Provider.AddTable("unique_values", new Column("amount", DbType.Int32, ColumnProperty.NotNull));
        f.Provider.Insert("unique_values", ["amount"], [7]);
        f.Provider.ChangeColumn("unique_values", new Column("amount", DbType.Int64, ColumnProperty.NotNull | ColumnProperty.Unique));
        Assert.That(f.Provider.ConstraintExists("unique_values", "UX_unique_values_amount"), Is.True);
        Assert.That(f.Provider.GetIndexes("unique_values").Any(i => i.UniqueConstraint && i.KeyColumns.Single().Equals("amount", StringComparison.OrdinalIgnoreCase)), Is.True);
        f.AssertDatabaseError(() => f.Provider.Insert("unique_values", ["amount"], [7L]));
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT COUNT(*) FROM unique_values")), Is.EqualTo(1));
        f.Provider.RemoveConstraint("unique_values", "UX_unique_values_amount");
        f.Provider.Insert("unique_values", ["amount"], [7L]);
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT COUNT(*) FROM unique_values")), Is.EqualTo(2));
    });

    [Test, Category("Informix")]
    public void InformixLargeTextMetadataCopiesAsLargeObjects() => new LiveDatabaseTests("Informix", ProviderTypes.IBM_Informix).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (text_value TEXT, clob_value CLOB)");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Select(c => c.Type), Is.EqualTo(new[] { DbType.String, DbType.String }));
        Assert.That(columns.Select(c => c.Size), Is.EqualTo(new[] { int.MaxValue, int.MaxValue }));
        f.Provider.AddTable("copied_values", columns);
        Assert.That(f.Provider.GetColumns("copied_values").Select(c => c.Size), Is.EqualTo(new[] { int.MaxValue, int.MaxValue }));
        // A maximum-width LVARCHAR leaves insufficient row space for an additional LOB locator.
        f.Provider.AddTable("bounded_values", new Column("bounded_value", DbType.String, 32739));
        f.Provider.AddTable("large_values", new Column("large_value", DbType.AnsiString, int.MaxValue));
        Assert.That(f.Provider.GetColumns("bounded_values").Single().Size, Is.EqualTo(32739));
        Assert.That(f.Provider.GetColumns("large_values").Single().Size, Is.EqualTo(int.MaxValue));
        var content = new string('z', 40000);
        f.Provider.Insert("copied_values", ["text_value", "clob_value"], [content, content]);
        Assert.That(f.Provider.ExecuteScalar("SELECT text_value FROM copied_values"), Is.EqualTo(content));
        Assert.That(f.Provider.ExecuteScalar("SELECT clob_value FROM copied_values"), Is.EqualTo(content));
    });

    [Test, Category("Informix")]
    public void InformixCharacterLengthsSurviveMetadataCopy() => new LiveDatabaseTests("Informix", ProviderTypes.IBM_Informix).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (long_text LVARCHAR(3000), short_text VARCHAR(40,10), fixed_text CHAR(300))");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Select(c => c.Size), Is.EqualTo(new[] { 3000, 40, 300 }));
        Assert.That(columns.Select(c => c.Type), Is.EqualTo(new[] { DbType.String, DbType.String, DbType.StringFixedLength }));
        f.Provider.AddTable("copied_values", columns);
        Assert.That(f.Provider.GetColumns("copied_values").Select(c => c.Size), Is.EqualTo(new[] { 3000, 40, 300 }));
        Assert.That(f.Provider.GetColumns("copied_values").Last().Type, Is.EqualTo(DbType.StringFixedLength));
        var content = new string('x', 2500);
        f.Provider.Insert("copied_values", ["long_text", "short_text", "fixed_text"], [content, "short", new string('y', 300)]);
        Assert.That(f.Provider.ExecuteScalar("SELECT long_text FROM copied_values"), Is.EqualTo(content));
        Assert.That(f.Provider.ExecuteScalar("SELECT fixed_text FROM copied_values"), Is.EqualTo(new string('y', 300)));
    });

    [Test, Category("Firebird")]
    public void FirebirdNativeDateTimeAndBooleanSurviveMetadataCopy() => new LiveDatabaseTests("Firebird", ProviderTypes.Firebird).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (date_value DATE, time_value TIME, enabled BOOLEAN)");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Select(c => c.Type), Is.EqualTo(new[] { DbType.Date, DbType.Time, DbType.Boolean }));
        f.Provider.AddTable("copied_values", columns);
        Assert.That(f.Provider.GetColumns("copied_values").Select(c => c.Type), Is.EqualTo(new[] { DbType.Date, DbType.Time, DbType.Boolean }));
        f.Provider.ExecuteNonQuery("INSERT INTO copied_values VALUES (DATE '2026-09-22', TIME '12:34:56', TRUE)");
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT COUNT(*) FROM copied_values WHERE date_value = DATE '2026-09-22' AND time_value = TIME '12:34:56' AND enabled IS TRUE")), Is.EqualTo(1));
    });

    [TestCase("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
    [TestCase("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
    [TestCase("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
    [TestCase("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
    public void DecimalShapeSurvivesCreateAlterAndCopy(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        f.Provider.AddTable("numbers", new Column("amount", DbType.Decimal, ColumnProperty.Null) { Precision = 12, Scale = 3 });
        var original = f.Provider.GetColumns("numbers").Single();
        Assert.That(original.Precision, Is.EqualTo(12));
        Assert.That(original.Scale, Is.EqualTo(3));
        f.Provider.Insert("numbers", ["amount"], [123.456m]);
        f.Provider.ChangeColumn("numbers", new Column("amount", DbType.Decimal, ColumnProperty.Null) { Precision = 15, Scale = 3 });
        var changed = f.Provider.GetColumns("numbers").Single();
        Assert.That(changed.Precision, Is.EqualTo(15));
        Assert.That(changed.Scale, Is.EqualTo(3));
        Assert.That(Convert.ToDecimal(f.Provider.ExecuteScalar("SELECT amount FROM numbers")), Is.EqualTo(123.456m));
        f.Provider.AddTable("copied_numbers", changed);
        var copied = f.Provider.GetColumns("copied_numbers").Single();
        Assert.That(copied.Precision, Is.EqualTo(15));
        Assert.That(copied.Scale, Is.EqualTo(3));
        f.Provider.AddColumn("copied_numbers", new Column("extra", DbType.Decimal, ColumnProperty.Null) { Precision = 10, Scale = 2 });
        var added = f.Provider.GetColumns("copied_numbers").Single(c => c.Name.Equals("extra", StringComparison.OrdinalIgnoreCase));
        Assert.That(added.Precision, Is.EqualTo(10));
        Assert.That(added.Scale, Is.EqualTo(2));
    });

    [TestCase("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
    [TestCase("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
    [TestCase("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
    public void PrimaryKeyMetadataIncludesIdentityAndCompositeMembers(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        f.Provider.AddTable("identities", new Column("id", DbType.Int32, ColumnProperty.PrimaryKeyWithIdentity));
        Assert.That(f.Provider.GetColumns("identities").Single().ColumnProperty.HasFlag(ColumnProperty.PrimaryKeyWithIdentity), Is.True);
        f.Provider.AddTable("pairs", new Column("first_id", DbType.Int32, ColumnProperty.PrimaryKey), new Column("second_id", DbType.Int32, ColumnProperty.PrimaryKey), new Column("label", DbType.String, 20));
        var columns = f.Provider.GetColumns("pairs");
        Assert.That(columns.Count(c => c.IsPrimaryKey), Is.EqualTo(2));
        Assert.That(columns.Single(c => c.Name.Equals("label", StringComparison.OrdinalIgnoreCase)).IsPrimaryKey, Is.False);
    });

    [Test, Category("Db2")]
    public void Db2DecfloatPrecisionRoundTrips() => new LiveDatabaseTests("Db2", ProviderTypes.IBM_DB2).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE floats (small_value DECFLOAT(16), large_value DECFLOAT(34))");
        var columns = f.Provider.GetColumns("floats");
        Assert.That(columns.Select(c => c.Type), Is.All.EqualTo(DbType.VarNumeric));
        Assert.That(columns.Select(c => c.Precision), Is.EqualTo(new int?[] { 16, 34 }));
        f.Provider.AddTable("copied_floats", columns);
        Assert.That(f.Provider.GetColumns("copied_floats").Select(c => c.Precision), Is.EqualTo(new int?[] { 16, 34 }));
    });

    [TestCase("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
    [TestCase("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
    public void TypedCatalogDefaultsRoundTrip(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        var boolean = database == "Db2" ? "BOOLEAN DEFAULT TRUE" : "BIT DEFAULT 1";
        var timestamp = database == "Db2" ? "TIMESTAMP DEFAULT CURRENT TIMESTAMP" : "DATETIME DEFAULT GETDATE()";
        f.Provider.ExecuteNonQuery($"CREATE TABLE source_values (id INTEGER, amount INTEGER DEFAULT 7, enabled {boolean}, label VARCHAR(40) DEFAULT 'O''Brien', stamp {timestamp})");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Single(c => c.Name.Equals("amount", StringComparison.OrdinalIgnoreCase)).DefaultValue, Is.TypeOf<int>().And.EqualTo(7));
        Assert.That(columns.Single(c => c.Name.Equals("enabled", StringComparison.OrdinalIgnoreCase)).DefaultValue, Is.TypeOf<bool>().And.EqualTo(true));
        Assert.That(columns.Single(c => c.Name.Equals("label", StringComparison.OrdinalIgnoreCase)).DefaultValue, Is.EqualTo("O'Brien"));
        f.Provider.AddTable("copied_values", columns);
        f.Provider.Insert("copied_values", ["id"], [1]);
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT amount FROM copied_values")), Is.EqualTo(7));
        Assert.That(f.Provider.ExecuteScalar("SELECT label FROM copied_values"), Is.EqualTo("O'Brien"));
        Assert.That(f.Provider.ExecuteScalar("SELECT stamp FROM copied_values"), Is.Not.Null.And.Not.EqualTo(DBNull.Value));
    });

    [Test, Category("Firebird")]
    public void FirebirdTextAndBinaryBlobsRoundTrip() => new LiveDatabaseTests("Firebird", ProviderTypes.Firebird).RunRegression(f =>
    {
        f.Provider.AddTable("large_values", new Column("contents", DbType.String, int.MaxValue), new Column("binary_value", DbType.Binary));
        var columns = f.Provider.GetColumns("large_values");
        Assert.That(columns[0].Type, Is.EqualTo(DbType.String));
        Assert.That(columns[0].Size, Is.EqualTo(int.MaxValue));
        Assert.That(columns[1].Type, Is.EqualTo(DbType.Binary));
        f.Provider.AddTable("copied_values", columns);
        var content = new string('x', 5000);
        f.Provider.Insert("copied_values", ["contents", "binary_value"], [content, new byte[] { 0, 1, 255 }]);
        Assert.That(f.Provider.ExecuteScalar("SELECT contents FROM copied_values"), Is.EqualTo(content));
        Assert.That(f.Provider.ExecuteScalar("SELECT binary_value FROM copied_values"), Is.EqualTo(new byte[] { 0, 1, 255 }));
    });

    [TestCase("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
    [TestCase("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
    public void MySqlBlobVariantsRemainBinary(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE blobs (tiny_value TINYBLOB, medium_value MEDIUMBLOB, ordinary_value BLOB, large_value LONGBLOB)");
        var columns = f.Provider.GetColumns("blobs");
        Assert.That(columns.Select(c => c.Type), Is.All.EqualTo(DbType.Binary));
        f.Provider.AddTable("copied_blobs", columns);
        f.Provider.Insert("copied_blobs", ["tiny_value", "medium_value"], [new byte[] { 0, 255 }, new byte[] { 1, 255 }]);
        Assert.That(f.Provider.ExecuteScalar("SELECT tiny_value FROM copied_blobs"), Is.EqualTo(new byte[] { 0, 255 }));
        Assert.That(f.Provider.ExecuteScalar("SELECT medium_value FROM copied_blobs"), Is.EqualTo(new byte[] { 1, 255 }));
    });

    [Test, Category("Informix")]
    public void InformixTimeMetadataRoundTrips() => new LiveDatabaseTests("Informix", ProviderTypes.IBM_Informix).RunRegression(f =>
    {
        f.Provider.AddTable("times", new Column("time_value", DbType.Time));
        var column = f.Provider.GetColumns("times").Single();
        Assert.That(column.Type, Is.EqualTo(DbType.Time));
        f.Provider.AddTable("copied_times", column);
        Assert.That(f.Provider.GetColumns("copied_times").Single().Type, Is.EqualTo(DbType.Time));
        f.Provider.ExecuteNonQuery("INSERT INTO copied_times VALUES (INTERVAL(12:34:56) HOUR TO SECOND)");
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT COUNT(*) FROM copied_times WHERE time_value=INTERVAL(12:34:56) HOUR TO SECOND")), Is.EqualTo(1));
    });

    [Test, Category("Sybase")]
    public void SybaseByteMetadataRoundTrips() => new LiveDatabaseTests("Sybase", ProviderTypes.Sybase).RunRegression(f =>
    {
        f.Provider.AddTable("bytes", new Column("byte_value", DbType.Byte));
        var column = f.Provider.GetColumns("bytes").Single();
        Assert.That(column.Type, Is.EqualTo(DbType.Byte));
        f.Provider.AddTable("copied_bytes", column);
        f.Provider.Insert("copied_bytes", ["byte_value"], [(byte)255]);
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT byte_value FROM copied_bytes")), Is.EqualTo(255));
    });

    [TestCase("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
    [TestCase("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
    [TestCase("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
    [TestCase("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
    public void InlineIndexedColumnCreatesIndex(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        f.Provider.AddTable("indexed_values", new Column("amount", DbType.Int32, ColumnProperty.Indexed));
        Assert.That(f.Provider.GetIndexes("indexed_values").Any(i => i.KeyColumns.Select(c => c.ToLowerInvariant()).SequenceEqual(new[] { "amount" })), Is.True);
    });

    [Test, Category("Sybase")]
    public void SybaseRemovesConstraintBackedIndexes() => new LiveDatabaseTests("Sybase", ProviderTypes.Sybase).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE numbers (id INTEGER NOT NULL PRIMARY KEY, amount INTEGER NOT NULL, CONSTRAINT uq_amount UNIQUE(amount))");
        Assert.That(f.Provider.GetIndexes("numbers").Count(i => i.UniqueConstraint), Is.EqualTo(1));
        f.Provider.RemoveAllIndexes("numbers");
        Assert.That(f.Provider.GetIndexes("numbers"), Is.Empty);
        Assert.That(f.Provider.ConstraintExists("numbers", "uq_amount"), Is.False);
    });

    [Test, Category("Firebird")]
    public void FirebirdDefaultsRoundTrip() => new LiveDatabaseTests("Firebird", ProviderTypes.Firebird).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (amount INTEGER DEFAULT 7, label VARCHAR(40) DEFAULT 'O''Brien', stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP)");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Single(c => c.Name == "AMOUNT").DefaultValue, Is.TypeOf<int>().And.EqualTo(7));
        Assert.That(columns.Single(c => c.Name == "LABEL").DefaultValue, Is.EqualTo("O'Brien"));
        f.Provider.AddTable("copied_values", columns);
        f.Provider.ExecuteNonQuery("INSERT INTO copied_values DEFAULT VALUES");
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT amount FROM copied_values")), Is.EqualTo(7));
        Assert.That(f.Provider.ExecuteScalar("SELECT label FROM copied_values"), Is.EqualTo("O'Brien"));
        Assert.That(f.Provider.ExecuteScalar("SELECT stamp FROM copied_values"), Is.Not.Null.And.Not.EqualTo(DBNull.Value));
    });

    [Test, Category("Firebird")]
    public void FirebirdDecimalPrecisionAndScale() => new LiveDatabaseTests("Firebird", ProviderTypes.Firebird).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE numbers (amount DECIMAL(12,3))");
        var column = f.Provider.GetColumns("numbers").Single();
        Assert.That(column.Type, Is.EqualTo(DbType.Decimal));
        Assert.That(column.Precision, Is.EqualTo(12));
        Assert.That(column.Scale, Is.EqualTo(3));
    });

    [Test, Category("Firebird")]
    public void FirebirdDropsOnlyAttachedDatabase() => new LiveDatabaseTests("Firebird", ProviderTypes.Firebird).RunRegression(f =>
    {
        Assert.Throws<ArgumentException>(() => f.Provider.DropDatabases("another_database"));
        f.DropCreatedDatabase();
    });

    [Test, Category("Db2")]
    public void Db2RemovesConstraintBackedIndexes() => new LiveDatabaseTests("Db2", ProviderTypes.IBM_DB2).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE numbers (id INTEGER NOT NULL PRIMARY KEY, amount INTEGER NOT NULL, CONSTRAINT uq_amount UNIQUE(amount))");
        Assert.That(f.Provider.GetIndexes("numbers").Count(i => i.UniqueConstraint), Is.EqualTo(1));
        f.Provider.RemoveAllIndexes("numbers");
        Assert.That(f.Provider.GetIndexes("numbers"), Is.Empty);
        Assert.That(f.Provider.ConstraintExists("numbers", "uq_amount"), Is.False);
    });

    [Test, Category("Informix")]
    public void InformixRemovesConstraintBackedIndexes() => new LiveDatabaseTests("Informix", ProviderTypes.IBM_Informix).RunRegression(f =>
    {
        f.Provider.AddTable("numbers", new Column("id", DbType.Int32, ColumnProperty.NotNull), new Column("amount", DbType.Int32, ColumnProperty.NotNull));
        f.Provider.AddPrimaryKey("pk_numbers", "numbers", "id");
        f.Provider.AddUniqueConstraint("uq_amount", "numbers", "amount");
        Assert.That(f.Provider.GetIndexes("numbers").Count(i => i.PrimaryKey), Is.EqualTo(1));
        Assert.That(f.Provider.GetIndexes("numbers").Count(i => i.UniqueConstraint), Is.EqualTo(1));
        f.Provider.RemoveAllIndexes("numbers");
        Assert.That(f.Provider.GetIndexes("numbers"), Is.Empty);
        Assert.That(f.Provider.ConstraintExists("numbers", "uq_amount"), Is.False);
    });

    [Test, Category("Informix")]
    public void InformixPreservesQuotedCatalogNames() => new LiveDatabaseTests("Informix", ProviderTypes.IBM_Informix).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE \"MixedCase\" (id INTEGER)");
        Assert.That(f.Provider.TableExists("\"MixedCase\""), Is.True);
        Assert.That(f.Provider.GetColumns("\"MixedCase\"").Single().Name, Is.EqualTo("id"));
    });

    [TestCase("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
    [TestCase("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
    public void MySqlDefaultsAndBooleanMetadataRoundTrip(string database, ProviderTypes type) => new LiveDatabaseTests(database, type).RunRegression(f =>
    {
        f.Provider.ExecuteNonQuery("CREATE TABLE source_values (enabled TINYINT(1) DEFAULT 1, amount INTEGER DEFAULT 7, label VARCHAR(40) DEFAULT 'O''Brien', stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP)");
        var columns = f.Provider.GetColumns("source_values");
        Assert.That(columns.Single(c => c.Name == "enabled").Type, Is.EqualTo(DbType.Boolean));
        Assert.That(columns.Single(c => c.Name == "enabled").DefaultValue, Is.EqualTo(true));
        Assert.That(columns.Single(c => c.Name == "amount").DefaultValue, Is.TypeOf<int>().And.EqualTo(7));
        Assert.That(columns.Single(c => c.Name == "label").DefaultValue, Is.EqualTo("O'Brien"));
        f.Provider.AddTable("copied_values", columns);
        f.Provider.AddUniqueConstraint("uq_label", "copied_values", "label");
        Assert.That(f.Provider.GetIndexes("copied_values").Single(i => i.Name == "uq_label").UniqueConstraint, Is.True);
        f.Provider.ExecuteNonQuery("INSERT INTO copied_values () VALUES ()");
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT amount FROM copied_values")), Is.EqualTo(7));
        Assert.That(f.Provider.ExecuteScalar("SELECT label FROM copied_values"), Is.EqualTo("O'Brien"));
        Assert.That(f.Provider.ExecuteScalar("SELECT stamp FROM copied_values"), Is.Not.Null.And.Not.EqualTo(DBNull.Value));
    });
}
