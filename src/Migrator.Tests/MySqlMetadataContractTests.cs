using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture(false)]
[TestFixture(true)]
public class MySqlMetadataContractTests(bool mariaDb)
{
    private MySqlTransformationProvider provider;
    private IDbCommand command;
    private readonly List<DataTable> tables = new();

    [SetUp]
    public void SetUp()
    {
        var connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open); connection.Database.Returns("Example");
        command = Substitute.For<IDbCommand>(); connection.CreateCommand().Returns(command);
        Dialect dialect = mariaDb ? new MariaDBDialect() : new MysqlDialect();
        provider = Substitute.ForPartsOf<MySqlTransformationProvider>(dialect, connection, "default", null);
        provider.Configure().TableExists(Arg.Any<string>()).Returns(true);
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(1);
        provider.Configure().ExecuteScalar("SELECT DATABASE()").Returns("Example");
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); foreach (var table in tables) table.Dispose(); tables.Clear(); }

    private DataTable Data(string[] names, params object[][] rows)
    {
        var table = new DataTable(); tables.Add(table);
        foreach (var name in names) table.Columns.Add(name, typeof(object));
        foreach (var row in rows) table.Rows.Add(row);
        return table;
    }

    [TestCase("PRIMARY KEY", "DROP PRIMARY KEY")]
    [TestCase("FOREIGN KEY", "DROP FOREIGN KEY `constraint`")]
    [TestCase("UNIQUE", "DROP INDEX `constraint`")]
    [TestCase("CHECK", "check")]
    public void ConstraintRemovalSelectsTheDialectSpecificStatement(string type, string action)
    {
        provider.Configure().ExecuteScalar(Arg.Any<string>()).Returns(type);
        provider.RemoveConstraint("Order", "constraint");
        if (action == "check") action = mariaDb ? "DROP CONSTRAINT `constraint`" : "DROP CHECK `constraint`";
        provider.Received(1).ExecuteNonQuery("ALTER TABLE `Order` " + action);
    }

    [Test]
    public void MissingConstraintFailsWithoutIssuingDdl()
    {
        provider.Configure().ExecuteScalar(Arg.Any<string>()).Returns(DBNull.Value);
        Assert.Throws<MigrationException>(() => provider.RemoveConstraint("Items", "Missing"));
        provider.DidNotReceiveWithAnyArgs().ExecuteNonQuery(default(string));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ForeignKeyLookupIsCaseInsensitiveAndClosesReader(bool exists)
    {
        var table = Data(new[] { "CONSTRAINT_NAME" }, new object[] { "FK_Other" }, new object[] { "FK_Parent" });
        using var reader = table.CreateDataReader();
        provider.Configure().ExecuteQuery(Arg.Any<IDbCommand>(), Arg.Any<string>()).Returns(reader);
        Assert.That(provider.ForeignKeyExists("Items", exists ? "fk_PARENT" : "FK_Missing"), Is.EqualTo(exists));
        Assert.That(reader.IsClosed, Is.True);
        command.Received(1).Dispose();
    }

    [Test]
    public void MissingTableDoesNotAttemptForeignKeyMetadataQuery()
    {
        provider.Configure().TableExists("Missing").Returns(false);
        Assert.That(provider.ForeignKeyExists("Missing", "FK_Name"), Is.False);
        provider.DidNotReceiveWithAnyArgs().ExecuteQuery(default, default);
    }

    [TestCase(null)]
    [TestCase("ParentId")]
    public void BulkForeignKeyRemovalClosesEnumerationBeforeIssuingDrops(string column)
    {
        var metadata = Data(new[] { "TABLE_SCHEMA", "TABLE_NAME", "CONSTRAINT_NAME" }, new object[] { "Example", "Items", "FK_Parent" }, new object[] { "Other", "Children", "FK_Items" });
        var names = Data(new[] { "CONSTRAINT_NAME" }, new object[] { "FK_Parent" }, new object[] { "FK_Items" });
        using var enumeration = metadata.CreateDataReader();
        provider.Configure().ExecuteQuery(Arg.Any<IDbCommand>(), Arg.Any<string>()).Returns(c =>
            ((string)c[1]).Contains("SELECT DISTINCT k.TABLE_SCHEMA") ? enumeration : names.CreateDataReader());
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(_ => { Assert.That(enumeration.IsClosed, Is.True); return 1; });
        provider.RemoveAllForeignKeys("Items", column);
        provider.Received(1).ExecuteNonQuery("ALTER TABLE `Example`.`Items` DROP FOREIGN KEY `FK_Parent`");
        provider.Received(1).ExecuteNonQuery("ALTER TABLE `Other`.`Children` DROP FOREIGN KEY `FK_Items`");
    }

    [Test]
    public void IndexCleanupDistinguishesPrimaryUniqueAndForeignKeys()
    {
        provider.Configure().IndexExists("Items", "UQ_Code").Returns(true);
        provider.Configure().GetIndexes("Items").Returns(new[] {
            new DotNetProjects.Migrator.Framework.Index { Name = "PRIMARY", PrimaryKey = true },
            new DotNetProjects.Migrator.Framework.Index { Name = "UQ_Code", Unique = true } });
        provider.Configure().ExecuteScalar(Arg.Any<string>()).Returns("PRIMARY KEY");
        var metadata = Data(new[] { "TABLE_SCHEMA", "TABLE_NAME", "CONSTRAINT_NAME" },
            new object[] { "Other", "Children", "FK_Items" });
        var names = Data(new[] { "CONSTRAINT_NAME" }, new object[] { "FK_Items" });
        using var enumeration = metadata.CreateDataReader();
        provider.Configure().ExecuteQuery(Arg.Any<IDbCommand>(), Arg.Any<string>()).Returns(c =>
            ((string)c[1]).Contains("SELECT DISTINCT k.TABLE_SCHEMA") ? enumeration : names.CreateDataReader());
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(_ => { Assert.That(enumeration.IsClosed, Is.True); return 1; });
        provider.RemoveAllIndexes("Items");
        provider.Received(1).ExecuteNonQuery("ALTER TABLE Items DROP PRIMARY KEY");
        provider.Received(1).ExecuteNonQuery("DROP INDEX `UQ_Code` ON Items");
        provider.Received(1).ExecuteNonQuery("ALTER TABLE `Other`.`Children` DROP FOREIGN KEY `FK_Items`");
    }

    [Test, SetCulture("de-DE")]
    public void ColumnMetadataRetainsNumericPrecisionNullabilityAndGeneratedDefaults()
    {
        var metadata = Data(new[] { "COLUMN_NAME", "DATA_TYPE", "IS_NULLABLE", "COLUMN_DEFAULT", "EXTRA", "CHARACTER_MAXIMUM_LENGTH", "COLUMN_KEY", "COLUMN_TYPE", "NUMERIC_PRECISION", "NUMERIC_SCALE" },
            new object[] { "Id", "bigint", "NO", DBNull.Value, "auto_increment", DBNull.Value, "PRI", "bigint", 19, 0 },
            new object[] { "Amount", "decimal", "YES", "123.45", "", DBNull.Value, "", "decimal(12,2)", 12, 2 },
            new object[] { "Name", "varchar", "YES", mariaDb ? "'O''Brien'" : "O'Brien", "", 4294967295L, "", "varchar", DBNull.Value, DBNull.Value },
            new object[] { "Created", "timestamp", "NO", "current_timestamp()", "DEFAULT_GENERATED", DBNull.Value, "", "timestamp", DBNull.Value, DBNull.Value },
            new object[] { "Enabled", "tinyint", "NO", "0", "", DBNull.Value, "", "tinyint(1)", 1, 0 });
        using var reader = metadata.CreateDataReader();
        provider.Configure().ExecuteQuery(Arg.Any<IDbCommand>(), Arg.Any<string>()).Returns(reader);
#pragma warning disable CS0618
        var columns = provider.GetColumns("Items");
#pragma warning restore CS0618
        Assert.That(columns.Select(c => c.Name), Is.EqualTo(new[] { "Id", "Amount", "Name", "Created", "Enabled" }));
        Assert.That(columns[0].IsIdentity, Is.True); Assert.That(columns[0].IsNullable, Is.False);
        Assert.That(columns[1].Precision, Is.EqualTo(12)); Assert.That(columns[1].Scale, Is.EqualTo(2));
        Assert.That(columns[1].DefaultValue, Is.EqualTo(123.45m)); Assert.That(columns[1].IsNullable, Is.True);
        Assert.That(columns[2].DefaultValue, Is.EqualTo("O'Brien")); Assert.That(columns[2].Size, Is.EqualTo(int.MaxValue));
        Assert.That(columns[3].DefaultValue, Is.Not.TypeOf<string>());
        Assert.That(provider.Dialect.Default(columns[3].DefaultValue), Is.EqualTo("DEFAULT current_timestamp()"));
        Assert.That(columns[4].Type, Is.EqualTo(DbType.Boolean)); Assert.That(columns[4].DefaultValue, Is.False);
        Assert.That(reader.IsClosed, Is.True);
    }
}
