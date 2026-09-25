using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.Ingres;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests;

// Executes the actual catalog SELECTs against an independent catalog fixture.
// This checks joins, ordering, CHAR padding and namespace isolation, not Ingres DDL execution.
public class IngresMetadataTests
{
    private SqliteConnection connection;
    private IngresTransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.CreateFunction<string, string>("DBMSINFO", key => key == "username" ? "tenant" : "testdb");
        Sql("""
            CREATE TABLE iiconstraints(schema_name TEXT COLLATE RTRIM,table_name TEXT COLLATE RTRIM,constraint_name TEXT COLLATE RTRIM,constraint_type TEXT,text_sequence INT,text_segment TEXT);
            CREATE TABLE iikeys(schema_name TEXT COLLATE RTRIM,table_name TEXT COLLATE RTRIM,constraint_name TEXT COLLATE RTRIM,column_name TEXT COLLATE RTRIM,key_position INT);
            CREATE TABLE iiref_constraints(ref_schema_name TEXT COLLATE RTRIM,ref_table_name TEXT COLLATE RTRIM,ref_constraint_name TEXT COLLATE RTRIM,unique_schema_name TEXT COLLATE RTRIM,unique_table_name TEXT COLLATE RTRIM,unique_constraint_name TEXT COLLATE RTRIM);
            CREATE TABLE iiindexes(index_owner TEXT COLLATE RTRIM,index_name TEXT COLLATE RTRIM,base_owner TEXT COLLATE RTRIM,base_name TEXT COLLATE RTRIM,unique_rule TEXT);
            CREATE TABLE iiindex_columns(index_owner TEXT COLLATE RTRIM,index_name TEXT COLLATE RTRIM,column_name TEXT COLLATE RTRIM,key_sequence INT);
            CREATE TABLE iiconstraint_indexes(schema_name TEXT COLLATE RTRIM,constraint_name TEXT COLLATE RTRIM,index_name TEXT COLLATE RTRIM);
            CREATE TABLE iicolumns(table_owner TEXT COLLATE RTRIM,table_name TEXT COLLATE RTRIM,column_name TEXT COLLATE RTRIM,column_sequence INT,column_datatype TEXT,column_length INT,column_scale INT,column_nulls TEXT,column_default_val TEXT);
            """);
        provider = Substitute.ForPartsOf<IngresTransformationProvider>(new IngresDialect(), connection, "default", null);
        foreach (var owner in new[] { "tenant", "other", "Tenant.One" }) Seed(owner);
    }

    [TearDown]
    public void TearDown() { provider.Dispose(); connection.Dispose(); }

    private void Sql(string sql) { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private void Insert(string catalog, params object[] values)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO " + catalog + " VALUES (" + string.Join(",", values.Select((_, i) => "@p" + i)) + ")";
        for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("@p" + i, values[i] ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private void Seed(string owner)
    {
        var prefix = owner == "other" ? "wrong_" : "";
        // Deliberately insert segments and composite columns in reverse physical order.
        Insert("iiconstraints", owner, "items", "pk_items", "P", 1, "PRIMARY KEY (b,a)");
        Insert("iiconstraints", owner, "items", "uq_items", "U", 1, "UNIQUE (code)");
        Insert("iiconstraints", owner, "items", "ck_items", "C", 2, "  b' AND amount > 0)");
        Insert("iiconstraints", owner, "items", "ck_items", "C", 1, "CONSTRAINT \"CHECK\" CHECK (code <> 'a");
        Insert("iiconstraints", owner, "items", "fk_items", "R", 2, "CADE ON UPDATE SET NULL");
        Insert("iiconstraints", owner, "items", "fk_items", "R", 1, "FOREIGN KEY (b,a) REFERENCES parent (y,x) ON DELETE CAS");
        Insert("iikeys", owner, "items", "pk_items", prefix + "a  ", 2);
        Insert("iikeys", owner, "items", "pk_items", prefix + "b  ", 1);
        Insert("iikeys", owner, "items", "uq_items", prefix + "code", 1);
        Insert("iikeys", owner, "items", "fk_items", prefix + "a", 2);
        Insert("iikeys", owner, "items", "fk_items", prefix + "b", 1);
        Insert("iikeys", owner, "parent", "pk_parent", prefix + "x", 2);
        Insert("iikeys", owner, "parent", "pk_parent", prefix + "y", 1);
        Insert("iiref_constraints", owner, "items", "fk_items", owner, "parent", "pk_parent");
        Insert("iiindexes", owner, "ix_items", owner, "items", "U");
        Insert("iiindexes", owner, "$pk_storage", owner, "items", "U");
        Insert("iiindexes", owner, "$uq_storage", owner, "items", "U");
        Insert("iiconstraint_indexes", owner, "pk_items", "$pk_storage");
        Insert("iiconstraint_indexes", owner, "uq_items", "$uq_storage");
        foreach (var index in new[] { "ix_items", "$pk_storage", "$uq_storage" })
        {
            Insert("iiindex_columns", owner, index, prefix + "a", 2);
            Insert("iiindex_columns", owner, index, prefix + "b", 1);
            Insert("iicolumns", owner, index, prefix + "a", 1, "integer", 4, 0, "N", null);
            Insert("iicolumns", owner, index, prefix + "b", 2, "integer", 4, 0, "N", null);
            Insert("iicolumns", owner, index, "tidp", 3, "integer", 4, 0, "N", null);
        }
        Insert("iicolumns", owner, "ix_items", prefix + "payload", 4, "varchar", 30, 0, "Y", null);
        Insert("iicolumns", owner, "items", "amount", 1, "decimal", 12, 2, "N", "123.45");
    }

    private string SelectMode(string mode)
    {
        provider.SetDefaultSchema(mode == "default" ? "tenant" : mode == "unqualified" ? null : "other");
        return mode switch { "qualified" => "TENANT.ITEMS", "quoted" => "\"Tenant.One\".\"items\"", _ => "items" };
    }

    [TestCase("unqualified")]
    [TestCase("qualified")]
    [TestCase("quoted")]
    [TestCase("default")]
    public void CatalogsKeepCompositeKeysConstraintsAndIndexesInsideTheSelectedNamespace(string mode)
    {
        var table = SelectMode(mode);
        var constraints = provider.GetTableConstraints(table);
        Assert.That(constraints, Has.Length.EqualTo(4));
        Assert.That(constraints.OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "b", "a" }));
        Assert.That(constraints.OfType<UniqueConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "code" }));
        Assert.That(constraints.OfType<CheckConstraint>().Single().CheckConstraintString, Is.EqualTo("code <> 'a  b' AND amount > 0"));
        var fk = constraints.OfType<ForeignKeyConstraint>().Single();
        Assert.That(fk.ChildColumns, Is.EqualTo(new[] { "b", "a" }));
        Assert.That(fk.ParentColumns, Is.EqualTo(new[] { "y", "x" }));
        Assert.That(fk.ParentTable, Is.EqualTo("\"parent\""));
        Assert.That(fk.OnDelete, Is.EqualTo("CASCADE"));
        Assert.That(fk.OnUpdate, Is.EqualTo("SET NULL"));
        var indexes = provider.GetIndexes(table);
        Assert.That(indexes, Has.Length.EqualTo(3));
        Assert.That(indexes.Single(i => i.Name == "$pk_storage").PrimaryKey, Is.True);
        Assert.That(indexes.Single(i => i.Name == "$uq_storage").UniqueConstraint, Is.True);
        var userIndex = indexes.Single(i => i.Name == "ix_items");
        Assert.That(userIndex.Unique, Is.True);
        Assert.That(userIndex.UniqueConstraint, Is.False);
        Assert.That(userIndex.KeyColumns, Is.EqualTo(new[] { "b", "a" }));
        Assert.That(userIndex.IncludeColumns, Is.EqualTo(new[] { "payload" }));
        var column = provider.GetColumns(table).Single();
        Assert.That(column.Precision, Is.EqualTo(12));
        Assert.That(column.Scale, Is.EqualTo(2));
        Assert.That(column.DefaultValue, Is.EqualTo(123.45m));
        Assert.That(provider.GetForeignKeyConstraints(table), Has.Length.EqualTo(1));
    }

    [Test]
    public void CrossOwnerReferencesPreserveQuotedParentIdentityAndDoNotJoinNamesakes()
    {
        Sql("UPDATE iiref_constraints SET unique_schema_name='Parent.Owner',unique_table_name='Parent\"Name' WHERE ref_schema_name='tenant'");
        Insert("iikeys", "Parent.Owner", "Parent\"Name", "pk_parent", "right", 2);
        Insert("iikeys", "Parent.Owner", "Parent\"Name", "pk_parent", "left", 1);
        var fk = provider.GetForeignKeyConstraints("tenant.items").Single();
        Assert.That(fk.ParentTable, Is.EqualTo("\"Parent.Owner\".\"Parent\"\"Name\""));
        Assert.That(fk.ParentColumns, Is.EqualTo(new[] { "left", "right" }));
    }

    [Test]
    public void MissingObjectsAndQuotedSqlPunctuationDoNotMatchAnotherOwner()
    {
        Assert.That(provider.GetIndexes("missing.items"), Is.Empty);
        Assert.That(provider.GetTableConstraints("\"x' OR 1=1 --\".items"), Is.Empty);
        Assert.That(provider.GetForeignKeyConstraints("tenant.missing"), Is.Empty);
    }

    [TestCase("unqualified", "items", "\"new_index\"")]
    [TestCase("qualified", "TENANT.ITEMS", "\"tenant\".\"new_index\"")]
    [TestCase("quoted", "\"Tenant.One\".\"items\"", "\"Tenant.One\".\"new_index\"")]
    [TestCase("default", "tenant.items", "\"tenant\".\"new_index\"")]
    public void DdlUsesTheSameNamespaceAsTheCatalog(string mode, string qualifiedTable, string qualifiedIndex)
    {
        var table = SelectMode(mode);
        var commands = new List<string>();
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(c => { commands.Add((string)c[0]); return 1; });
        provider.AddIndex(table, new Index { Name = "new_index", Unique = true, KeyColumns = ["b", "a"], IncludeColumns = ["payload"] });
        provider.RemoveIndex(table, "ix_items");
        provider.RemoveConstraint(table, "ck_items");
        provider.RemoveColumn(table, "amount");
        Assert.That(commands[0], Is.EqualTo($"CREATE UNIQUE INDEX {qualifiedIndex} ON {qualifiedTable} (b, a, payload) WITH STRUCTURE=BTREE, KEY=(b, a), PERSISTENCE"));
        Assert.That(commands[1], Is.EqualTo("DROP INDEX " + qualifiedIndex.Replace("new_index", "ix_items")));
        Assert.That(commands[2], Is.EqualTo($"ALTER TABLE {qualifiedTable} DROP CONSTRAINT \"ck_items\" RESTRICT"));
        Assert.That(commands[3], Is.EqualTo($"ALTER TABLE {qualifiedTable} DROP COLUMN \"amount\" RESTRICT"));
    }

    [Test]
    public void SystemIndexRemovalUsesItsConstraintNameAndSelectedOwner()
    {
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(1);
        provider.RemoveIndex("tenant.items", "$pk_storage");
        provider.Received(1).ExecuteNonQuery("ALTER TABLE tenant.items DROP CONSTRAINT \"pk_items\" RESTRICT");
        provider.DidNotReceive().ExecuteNonQuery(Arg.Is<string>(s => s.Contains("DROP CONSTRAINT \"$pk_storage\"")));
    }

    [TestCase(null, 2)]
    [TestCase("b", 2)]
    [TestCase("code", 0)]
    public void BulkForeignKeyRemovalIncludesIncomingReferencesWithoutTouchingNamesakes(string column, int expected)
    {
        Insert("iiconstraints", "other", "child", "fk_incoming", "R", 1, "REFERENCES tenant.items(b,a)");
        Insert("iikeys", "other", "child", "fk_incoming", "child_b", 1);
        Insert("iiref_constraints", "other", "child", "fk_incoming", "tenant", "items", "pk_items");
        var commands = new List<string>();
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(c => { commands.Add((string)c[0]); return 1; });
        provider.SetDefaultSchema("other");
        provider.RemoveAllForeignKeys("tenant.items", column);
        Assert.That(commands, Has.Count.EqualTo(expected));
        if (expected != 0) Assert.That(commands, Is.EquivalentTo(new[] {
            "ALTER TABLE \"tenant\".\"items\" DROP CONSTRAINT \"fk_items\" RESTRICT",
            "ALTER TABLE \"other\".\"child\" DROP CONSTRAINT \"fk_incoming\" RESTRICT" }));
    }

    [TestCase("FOREIGN KEY (x) REFERENCES p (y)", "NO ACTION", "NO ACTION")]
    [TestCase("FOREIGN KEY (\"ON DELETE CASCADE\") REFERENCES \"ON UPDATE SET NULL\" (y)", "NO ACTION", "NO ACTION")]
    [TestCase("/* ON DELETE CASCADE */ REFERENCES p(y) ON UPDATE RESTRICT -- ON DELETE SET NULL\n", "NO ACTION", "RESTRICT")]
    [TestCase("REFERENCES p(y) ON /* comment */ DELETE NO ACTION ON UPDATE CASCADE", "NO ACTION", "CASCADE")]
    public void ActionsIgnoreQuotedTextAndComments(string definition, string delete, string update)
    {
        Assert.That(IngresConstraintText.Actions(definition), Is.EqualTo((delete, update)));
    }

    [Test]
    public void UnknownReferentialActionsAreNotSilentlyReplacedByNoAction() =>
        Assert.Throws<MigrationException>(() => IngresConstraintText.Actions("REFERENCES p(y) ON DELETE SOMETHING"));
}
