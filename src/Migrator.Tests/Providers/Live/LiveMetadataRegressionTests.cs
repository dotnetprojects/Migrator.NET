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
        f.Provider.ExecuteNonQuery("INSERT INTO copied_values () VALUES ()");
        Assert.That(Convert.ToInt32(f.Provider.ExecuteScalar("SELECT amount FROM copied_values")), Is.EqualTo(7));
        Assert.That(f.Provider.ExecuteScalar("SELECT label FROM copied_values"), Is.EqualTo("O'Brien"));
        Assert.That(f.Provider.ExecuteScalar("SELECT stamp FROM copied_values"), Is.Not.Null.And.Not.EqualTo(DBNull.Value));
    });
}
