using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.Informix;
using DotNetProjects.Migrator.Providers.Impl.Ingres;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests.Providers.Live;

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
public class PortableCheckTests(string database, ProviderTypes type) : LiveProviderFixture(database, type)
{
    [TestCase(false)]
    [TestCase(true)]
    public void JsonValidation(bool add) => VerifyJson(Provider, add, type);
    public static void VerifyJson(ITransformationProvider p, bool add, ProviderTypes type)
    {
        var rule = Column("Payload").IsJson(JsonShape.Array);
        if (type is not (ProviderTypes.SQLite or ProviderTypes.PostgreSQL or ProviderTypes.SqlServer or ProviderTypes.Mysql or ProviderTypes.MariaDB or ProviderTypes.Oracle))
        {
            Assert.Throws<NotSupportedException>(() => rule.ToSql(new SqlExpressionContext(p.Dialect))); return;
        }
        string table = "check_json_" + Guid.NewGuid().ToString("N")[..8];
        var field = new Column("Payload", DbType.String, 100) { DefaultValue = "[]" };
        if (add) { p.AddTable(table, field); p.Insert(table, ["Payload"], ["[]"]); p.AddCheckConstraint("ck_" + Guid.NewGuid().ToString("N")[..8], table, rule); }
        else p.AddTable(table, field, CheckConstraint.FromExpression("ck_" + Guid.NewGuid().ToString("N")[..8], rule));
        p.Insert(table, ["Payload"], ["[]"]); p.Insert(table, ["Payload"], ["[1,null,true]"]);
        // Savepoints are needed after PostgreSQL constraint errors inside the fixture transaction.
        foreach (string invalid in new[] { "{}", "1", "null", "[", "" }) Reject(p, () => p.Insert(table, ["Payload"], [invalid]));
        Reject(p, () => p.Insert(table, ["Payload"], [null]));
        Assert.That(Convert.ToInt32(p.ExecuteScalar("SELECT COUNT(*) FROM " + p.QuoteTableNameIfRequired(table))), Is.EqualTo(add ? 3 : 2));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void BinaryLengthIsMeasuredInBytes(bool add) => VerifyBinary(Provider, add);
    public static void VerifyBinary(ITransformationProvider p, bool add)
    {
        var value = Column("Payload"); var rule = value.IsNotNull().And(value.BinaryLength().GreaterThan(Value(0)));
        if (p.Dialect is OracleDialect or InformixDialect or IngresDialect or DotNetProjects.Migrator.Providers.Impl.Sybase.SybaseDialect)
        { Assert.Throws<NotSupportedException>(() => rule.ToSql(new SqlExpressionContext(p.Dialect))); return; }
        string table = "check_bin_" + Guid.NewGuid().ToString("N")[..8];
        var field = new Column("Payload", DbType.Binary, 100);
        if (add) { p.AddTable(table, field); p.Insert(table, ["Payload"], [new byte[] { 0 }]); p.AddCheckConstraint("ck_" + Guid.NewGuid().ToString("N")[..8], table, rule); }
        else p.AddTable(table, field, CheckConstraint.FromExpression("ck_" + Guid.NewGuid().ToString("N")[..8], rule));
        p.Insert(table, ["Payload"], [new byte[] { 0 }]); p.Insert(table, ["Payload"], [new byte[] { 1, 0, 32 }]);
        Reject(p, () => p.Insert(table, ["Payload"], [Array.Empty<byte>()]));
        Reject(p, () => p.Insert(table, ["Payload"], [null]));
        Assert.That(Convert.ToInt32(p.ExecuteScalar("SELECT COUNT(*) FROM " + p.QuoteTableNameIfRequired(table))), Is.EqualTo(add ? 3 : 2));
    }
    private static void Reject(ITransformationProvider p, TestDelegate write)
    {
        bool pg = p.Dialect is DotNetProjects.Migrator.Providers.Impl.PostgreSQL.PostgreSQLDialect;
        if (pg) p.ExecuteNonQuery("SAVEPOINT expected_failure");
        try { Assert.Catch(write); }
        finally { if (pg) { p.ExecuteNonQuery("ROLLBACK TO SAVEPOINT expected_failure"); p.ExecuteNonQuery("RELEASE SAVEPOINT expected_failure"); } }
    }
}

[Category("SQLite")]
public class SQLitePortableCheckTests
{
    [Test]
    public void OptionalJsonAcceptsScalarsAndRejectsMalformedValues()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.AddTable("OptionalJson", new Column("Value", DbType.String, 100), CheckConstraint.FromExpression("ck_json", Column("Value").IsJson(allowNull: true)));
        foreach (var value in new[] { null, "null", "true", "42", "\"text\"", "{}", "[]" }) p.Insert("OptionalJson", ["Value"], [value]);
        foreach (var value in new[] { "", "[", "bad", "1,2" }) Assert.Catch(() => p.Insert("OptionalJson", ["Value"], [value]));
        Assert.That(p.ExecuteScalar("SELECT COUNT(*) FROM OptionalJson"), Is.EqualTo(7L));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void GroupedColumnRulesDoNotPassThroughUnknown(bool add)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        var gross = Column("Gross"); var tax = Column("Tax"); var date = Column("ReversedAt"); var reason = Column("Reason");
        var rule = gross.IsNotNull().And(gross.GreaterThan(Value(0))).And(tax.IsNotNull()).And(tax.GreaterOrEqual(Value(0))).And(tax.LessOrEqual(gross))
            .And(date.IsNull().And(reason.IsNull()).Or(date.IsNotNull().And(reason.Trim().IsNotNull()).And(reason.Trim().TextLength().GreaterThan(Value(0)))));
        p.AddTable("Amounts", new Column("Gross", DbType.Int32), new Column("Tax", DbType.Int32), new Column("ReversedAt", DbType.String), new Column("Reason", DbType.String));
        if (add) p.Insert("Amounts", ["Gross", "Tax"], [100, 0]);
        p.AddCheckConstraint("ck_amounts", "Amounts", rule);
        p.Insert("Amounts", ["Gross", "Tax"], [100, 100]);
        p.Insert("Amounts", ["Gross", "Tax", "ReversedAt", "Reason"], [100, 10, "2026-01-01", " reason "]);
        foreach (var row in new object[][] { [0, 0, null, null], [100, -1, null, null], [100, 101, null, null], [null, 1, null, null], [100, 0, "date", null], [100, 0, null, "reason"], [100, 0, "date", "   "] })
            Assert.Catch(() => p.Insert("Amounts", ["Gross", "Tax", "ReversedAt", "Reason"], row));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void JsonChecks(bool add)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        PortableCheckTests.VerifyJson(p, add, ProviderTypes.SQLite);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void BinaryChecks(bool add)
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        PortableCheckTests.VerifyBinary(p, add);
    }
    [Test]
    public void AddingInvalidConstraintLeavesDataDefaultsAndSchemaIntact()
    {
        using var c = new SqliteConnection("Data Source=:memory:"); c.Open(); using var p = new SQLiteTransformationProvider(new SQLiteDialect(), c, null, null);
        p.ExecuteNonQuery("CREATE TABLE Original (Id INTEGER, Payload TEXT DEFAULT '[]'); INSERT INTO Original VALUES (1,'invalid')");
        Assert.Catch(() => p.AddCheckConstraint("ck_invalid", "Original", Column("Payload").IsJson()));
        Assert.That(p.ExecuteScalar("SELECT Payload FROM Original"), Is.EqualTo("invalid"));
        p.ExecuteNonQuery("INSERT INTO Original (Id) VALUES (2)");
        Assert.That(p.ExecuteScalar("SELECT Payload FROM Original WHERE Id=2"), Is.EqualTo("[]"));
        Assert.That(p.GetCheckConstraints("Original"), Is.Empty);
    }
}
