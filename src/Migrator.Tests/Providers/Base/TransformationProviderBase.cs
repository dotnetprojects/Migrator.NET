using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

/// <summary>
/// Base class for Provider tests for all non-constraint oriented tests.
/// </summary>
public abstract class TransformationProviderBase : TransformationProviderSimpleBase
{
    [Test]
    public void TableExistsWorks()
    {
        Assert.That(Provider.TableExists("gadadadadseeqwe"), Is.False);
        Assert.That(Provider.TableExists("TestTwo"), Is.True);
    }

    [Test]
    public void ColumnExistsWorks()
    {
        Assert.That(Provider.ColumnExists("gadadadadseeqwe", "eqweqeq"), Is.False);
        Assert.That(Provider.ColumnExists("TestTwo", "eqweqeq"), Is.False);
        Assert.That(Provider.ColumnExists("TestTwo", "Id"), Is.True);
    }

    [Test]
    public void CanExecuteBadSqlForNonCurrentProvider()
    {
        Provider["foo"].ExecuteNonQuery("select foo from bar 123");
    }

    [Test]
    public void TableCanBeAdded()
    {
        AddTable();
        Assert.That(Provider.TableExists("Test"), Is.True);
    }

    [Test]
    public void GetTablesWorks()
    {
        foreach (var name in Provider.GetTables())
        {
            Provider.Logger.Log("Table: {0}", name);
        }

        Assert.That(1, Is.EqualTo(Provider.GetTables().Length));
        AddTable();
        Assert.That(2, Is.EqualTo(Provider.GetTables().Length));
    }

    [Test]
    public void GetColumnsReturnsProperCount()
    {
        AddTable();
        var cols = Provider.GetColumns("Test");

        Assert.That(cols, Is.Not.Null);
        Assert.That(6, Is.EqualTo(cols.Length));
    }

    [Test]
    public void GetColumnsContainsProperNullInformation()
    {
        AddTableWithPrimaryKey();
        var cols = Provider.GetColumns("Test");
        Assert.That(cols, Is.Not.Null);

        foreach (var column in cols)
        {
            if (column.Name == "name")
            {
                Assert.That((column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull, Is.True);
            }
            else if (column.Name == "Title")
            {
                Assert.That((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null, Is.True);
            }
        }
    }

    [Test]
    public void CanAddTableWithPrimaryKey()
    {
        AddTableWithPrimaryKey();
        Assert.That(Provider.TableExists("Test"), Is.True);
    }

    [Test]
    public void RemoveTable()
    {
        AddTable();
        Provider.RemoveTable("Test");
        Assert.That(Provider.TableExists("Test"), Is.False);
    }

    [Test]
    public virtual void RenameTableThatExists()
    {
        AddTable();
        Provider.RenameTable("Test", "Test_Rename");

        Assert.That(Provider.TableExists("Test_Rename"), Is.True);
        Assert.That(Provider.TableExists("Test"), Is.False);
        Provider.RemoveTable("Test_Rename");
    }

    [Test]
    public void RenameTableToExistingTable()
    {
        AddTable();
        Assert.Throws<MigrationException>(() =>
        {
            Provider.RenameTable("Test", "TestTwo");
        });
    }

    [Test]
    public void RenameColumnThatExists()
    {
        AddTable();
        Provider.RenameColumn("Test", "name", "name_rename");

        Assert.That(Provider.ColumnExists("Test", "name_rename"), Is.True);
        Assert.That(Provider.ColumnExists("Test", "name"), Is.False);
    }

    [Test]
    public void RenameColumnToExistingColumn()
    {
        AddTable();
        Assert.Throws<MigrationException>(() =>
        {
            Provider.RenameColumn("Test", "Title", "name");
        });
    }

    [Test]
    public void RemoveUnexistingTable()
    {
        var exception = Assert.Catch(() => Provider.RemoveTable("abc"));
        var expectedMessage = "Table with name 'abc' does not exist to rename";

        Assert.That(exception.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void AddColumn()
    {
        Provider.AddColumn("TestTwo", "Test", DbType.String, 50);
        Assert.That(Provider.ColumnExists("TestTwo", "Test"), Is.True);
    }

    [Test]
    public void ChangeColumn()
    {
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50));
        Assert.That(Provider.ColumnExists("TestTwo", "TestId"), Is.True);
        Provider.Insert("TestTwo", ["Id", "TestId"], [1, "Not an Int val."]);
    }

    [Test]
    public void ChangeColumn_FromNullToNull()
    {
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
        Provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
        Provider.Insert("TestTwo", ["Id", "TestId"], [2, "Not an Int val."]);
    }

    [Test]
    public void AddDecimalColumn()
    {
        Provider.AddColumn("TestTwo", "TestDecimal", DbType.Decimal, 38);
        Assert.That(Provider.ColumnExists("TestTwo", "TestDecimal"), Is.True);
    }

    [Test]
    public void AddColumnWithDefault()
    {
        Provider.AddColumn("TestTwo", "TestWithDefault", DbType.Int32, 50, 0, 10);
        Assert.That(Provider.ColumnExists("TestTwo", "TestWithDefault"), Is.True);
    }

    [Test]
    public void AddColumnWithDefaultButNoSize()
    {
        Provider.AddColumn("TestTwo", "TestWithDefault", DbType.Int32, 10);
        Assert.That(Provider.ColumnExists("TestTwo", "TestWithDefault"), Is.True);

        Provider.AddColumn("TestTwo", "TestWithDefaultString", DbType.String, "'foo'");
        Assert.That(Provider.ColumnExists("TestTwo", "TestWithDefaultString"), Is.True);
    }

    [Test]
    public void AddBooleanColumnWithDefault()
    {
        Provider.AddColumn("TestTwo", "TestBoolean", DbType.Boolean, 0, 0, false);
        Assert.That(Provider.ColumnExists("TestTwo", "TestBoolean"), Is.True);
    }

    [Test]
    public void CanGetNullableFromProvider()
    {
        Provider.AddColumn("TestTwo", "NullableColumn", DbType.String, 30, ColumnProperty.Null);
        var columns = Provider.GetColumns("TestTwo");

        foreach (var column in columns)
        {
            if (column.Name == "NullableColumn")
            {
                Assert.That((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null, Is.True);
            }
        }
    }

    [Test]
    public void RemoveColumn()
    {
        AddColumn();
        Provider.RemoveColumn("TestTwo", "Test");
        Assert.That(Provider.ColumnExists("TestTwo", "Test"), Is.False);
    }

    [Test]
    public void RemoveColumnWithDefault()
    {
        AddColumnWithDefault();
        Provider.RemoveColumn("TestTwo", "TestWithDefault");
        Assert.That(Provider.ColumnExists("TestTwo", "TestWithDefault"), Is.False);
    }

    [Test]
    public void RemoveUnexistingColumn()
    {
        var exception1 = Assert.Throws<MigrationException>(() => Provider.RemoveColumn("TestTwo", "abc"));
        var exception2 = Assert.Throws<MigrationException>(() => Provider.RemoveColumn("abc", "abc"));

        Assert.That(exception1.Message, Is.EqualTo("The table 'TestTwo' does not have a column named 'abc'"));
        Assert.That(exception2.Message, Is.EqualTo("The table 'abc' does not exist"));
    }

    /// <summary>
    /// Supprimer une colonne bit causait une erreur à cause
    /// de la valeur par défaut.
    /// </summary>
    [Test]
    public void RemoveBoolColumn()
    {
        AddTable();
        Provider.AddColumn("Test", "Inactif", DbType.Boolean);
        Assert.That(Provider.ColumnExists("Test", "Inactif"), Is.True);

        Provider.RemoveColumn("Test", "Inactif");
        Assert.That(Provider.ColumnExists("Test", "Inactif"), Is.False);
    }

    [Test]
    public void HasColumn()
    {
        AddColumn();
        Assert.That(Provider.ColumnExists("TestTwo", "Test"), Is.True);
        Assert.That(Provider.ColumnExists("TestTwo", "TestPasLa"), Is.False);
    }

    [Test]
    public void HasTable()
    {
        Assert.That(Provider.TableExists("TestTwo"), Is.True);
    }

    [Test]
    public void AppliedMigrations()
    {
        Assert.That(Provider.TableExists("SchemaInfo"), Is.False);

        // Check that a "get" call works on the first run.
        Assert.That(0, Is.EqualTo(Provider.AppliedMigrations.Count));
        Assert.That(Provider.TableExists("SchemaInfo"), Is.True, "No SchemaInfo table created");

        // Check that a "set" called after the first run works.
        Provider.MigrationApplied(1, null);
        Assert.That(1, Is.EqualTo(Provider.AppliedMigrations[0]));

        Provider.RemoveTable("SchemaInfo");
        // Check that a "set" call works on the first run.
        Provider.MigrationApplied(1, null);
        Assert.That(1, Is.EqualTo(Provider.AppliedMigrations[0]));
        Assert.That(Provider.TableExists("SchemaInfo"), Is.True, "No SchemaInfo table created");
    }


    [Test]
    public void CommitTwice()
    {
        Provider.Commit();
        Assert.That(0, Is.EqualTo(Provider.AppliedMigrations.Count));
        Provider.Commit();
    }

    [Test]
    public void InsertData()
    {
        Provider.Insert("TestTwo", ["Id", "TestId"], [1, 1]);
        Provider.Insert("TestTwo", ["Id", "TestId"], [2, 2]);

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "TestId", "TestTwo");
        var vals = GetVals(reader);

        Assert.That(Array.Exists(vals, delegate (int val) { return val == 1; }), Is.True);
        Assert.That(Array.Exists(vals, delegate (int val) { return val == 2; }), Is.True);
    }

    [Test]
    public void CanInsertNullData()
    {
        AddTable();

        Provider.Insert("Test", ["Id", "Title"], [1, "foo"]);
        Provider.Insert("Test", ["Id", "Title"], [2, null]);

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "Title", "Test");
        var vals = GetStringVals(reader);

        Assert.That(Array.Exists(vals, delegate (string val) { return val == "foo"; }), Is.True);
        Assert.That(Array.Exists(vals, delegate (string val) { return val == null; }), Is.True);
    }

    [Test]
    public void CanInsertDataWithSingleQuotes()
    {
        // Arrange
        const string testString = "Test string with ' (single quote)";
        AddTable();
        Provider.Insert("Test", ["Id", "Title"], [1, testString]);

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "Title", "Test");

        Assert.That(reader.Read(), Is.True);
        Assert.That(testString, Is.EqualTo(reader.GetString(0)));
        Assert.That(reader.Read(), Is.False);
    }

    [Test]
    public void DeleteData()
    {
        InsertData();
        Provider.Delete("TestTwo", "TestId", "1");
        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "TestId", "TestTwo");
        Assert.That(reader.Read(), Is.True);
        Assert.That(2, Is.EqualTo(Convert.ToInt32(reader[0])));
        Assert.That(reader.Read(), Is.False);
    }

    [Test]
    public void DeleteDataWithArrays()
    {
        InsertData();

        Provider.Delete("TestTwo", ["TestId"], [1]);

        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "TestId", "TestTwo");

        Assert.That(reader.Read(), Is.True);
        Assert.That(2, Is.EqualTo(Convert.ToInt32(reader[0])));
        Assert.That(reader.Read(), Is.False);
    }

    [Test]
    public void UpdateData()
    {
        Provider.Insert("TestTwo", ["Id", "TestId"], [20, 1]);
        Provider.Insert("TestTwo", ["Id", "TestId"], [21, 2]);

        Provider.Update("TestTwo", ["TestId"], [3]);
        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "TestId", "TestTwo");
        var vals = GetVals(reader);

        Assert.That(Array.Exists(vals, delegate (int val) { return val == 3; }), Is.True);
        Assert.That(Array.Exists(vals, delegate (int val) { return val == 1; }), Is.False);
        Assert.That(Array.Exists(vals, delegate (int val) { return val == 2; }), Is.False);
    }

    [Test]
    public void CanUpdateWithNullData()
    {
        AddTable();
        Provider.Insert("Test", ["Id", "Title"], [1, "foo"]);
        Provider.Insert("Test", ["Id", "Title"], [2, null]);

        Provider.Update("Test", ["Title"], [null]);
        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "Title", "Test");
        var vals = GetStringVals(reader);

        Assert.That(vals[0], Is.Null);
        Assert.That(vals[1], Is.Null);
    }

    [Test]
    public void UpdateDataWithWhere()
    {
        Provider.Insert("TestTwo", ["Id", "TestId"], [10, 1]);
        Provider.Insert("TestTwo", ["Id", "TestId"], [11, 2]);

        Provider.Update("TestTwo", ["TestId"], [3], "TestId='1'");
        using var cmd = Provider.CreateCommand();
        using var reader = Provider.Select(cmd, "TestId", "TestTwo");
        var vals = GetVals(reader);

        Assert.That(Array.Exists(vals, delegate (int val) { return val == 3; }), Is.True);
        Assert.That(Array.Exists(vals, delegate (int val) { return val == 2; }), Is.True);
        Assert.That(Array.Exists(vals, delegate (int val) { return val == 1; }), Is.False);
    }

    [Test]
    public void AddIndex()
    {
        var indexName = "test_index";

        Assert.That(Provider.IndexExists("TestTwo", indexName), Is.False);
        Provider.AddIndex(indexName, "TestTwo", "Id", "TestId");
        Assert.That(Provider.IndexExists("TestTwo", indexName), Is.True);
    }

    [Test]
    public void RemoveIndex()
    {
        var indexName = "test_index";

        Assert.That(Provider.IndexExists("TestTwo", indexName), Is.False);
        Provider.AddIndex(indexName, "TestTwo", "Id", "TestId");
        Provider.RemoveIndex("TestTwo", indexName);
        Assert.That(Provider.IndexExists("TestTwo", indexName), Is.False);
    }


    private int[] GetVals(IDataReader reader)
    {
        var vals = new int[2];
        Assert.That(reader.Read(), Is.True);
        vals[0] = Convert.ToInt32(reader[0]);
        Assert.That(reader.Read(), Is.True);
        vals[1] = Convert.ToInt32(reader[0]);

        return vals;
    }

    private string[] GetStringVals(IDataReader reader)
    {
        var vals = new string[2];
        Assert.That(reader.Read(), Is.True);
        vals[0] = reader[0] as string;
        Assert.That(reader.Read(), Is.True);
        vals[1] = reader[0] as string;

        return vals;
    }
}
