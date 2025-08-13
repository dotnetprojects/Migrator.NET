using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

/// <summary>
/// Base class for provider tests.
/// </summary>
public abstract class TransformationProviderGenericMiscTests : TransformationProviderSimpleBase
{
    [Test]
    public void GetColumns_DefaultValues_Succeeds()
    {
        // Arrange
        var dateTimeDefaultValue = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var guidDefaultValue = Guid.NewGuid();
        var decimalDefaultValue = 14.56565m;

        const string testTableName = "MyDefaultTestTable";

        const string dateTimeColumnName1 = "datetimecolumn1";
        const string dateTimeColumnName2 = "datetimecolumn2";
        const string decimalColumnName1 = "decimalcolumn";
        const string guidColumnName1 = "guidcolumn1";
        const string booleanColumnName1 = "booleancolumn1";
        const string int32ColumnName1 = "int32column1";
        const string int64ColumnName1 = "int64column1";
        const string int64ColumnName2 = "int64column2";
        const string stringColumnName1 = "stringcolumn1";
        const string binaryColumnName1 = "binarycolumn1";
        const string doubleColumnName1 = "doublecolumn1";

        // Should be extended by remaining types
        Provider.AddTable(testTableName,
            new Column(dateTimeColumnName1, DbType.DateTime, dateTimeDefaultValue),
            new Column(dateTimeColumnName2, DbType.DateTime2, dateTimeDefaultValue),
            new Column(decimalColumnName1, DbType.Decimal, decimalDefaultValue),
            new Column(guidColumnName1, DbType.Guid, guidDefaultValue),

            // other boolean default values are tested in another test
            new Column(booleanColumnName1, DbType.Boolean, true),

            new Column(int32ColumnName1, DbType.Int32, defaultValue: 43),
            new Column(int64ColumnName1, DbType.Int64, defaultValue: 88),
            new Column(int64ColumnName2, DbType.Int64, defaultValue: 0),
            new Column(stringColumnName1, DbType.String, defaultValue: "Hello"),
            new Column(binaryColumnName1, DbType.Binary, defaultValue: new byte[] { 12, 32, 34 }),
            new Column(doubleColumnName1, DbType.Double, defaultValue: 84.874596565)
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var dateTimeColumn1 = columns.Single(x => x.Name == dateTimeColumnName1);
        var dateTimeColumn2 = columns.Single(x => x.Name == dateTimeColumnName2);
        var decimalColumn1 = columns.Single(x => x.Name == decimalColumnName1);
        var guidColumn1 = columns.Single(x => x.Name == guidColumnName1);
        var booleanColumn1 = columns.Single(x => x.Name == booleanColumnName1);
        var int32Column1 = columns.Single(x => x.Name == int32ColumnName1);
        var int64Column1 = columns.Single(x => x.Name == int64ColumnName1);
        var int64Column2 = columns.Single(x => x.Name == int64ColumnName2);
        var stringColumn1 = columns.Single(x => x.Name == stringColumnName1);
        var binarycolumn1 = columns.Single(x => x.Name == binaryColumnName1);
        var doubleColumn1 = columns.Single(x => x.Name == doubleColumnName1);

        Assert.That(dateTimeColumn1.DefaultValue, Is.EqualTo(dateTimeDefaultValue));
        Assert.That(dateTimeColumn2.DefaultValue, Is.EqualTo(dateTimeDefaultValue));
        Assert.That(decimalColumn1.DefaultValue, Is.EqualTo(decimalDefaultValue));
        Assert.That(guidColumn1.DefaultValue, Is.EqualTo(guidDefaultValue));
        Assert.That(booleanColumn1.DefaultValue, Is.True);
        Assert.That(int32Column1.DefaultValue, Is.EqualTo(43));
        Assert.That(int64Column1.DefaultValue, Is.EqualTo(88));
        Assert.That(stringColumn1.DefaultValue, Is.EqualTo("Hello"));
        Assert.That(binarycolumn1.DefaultValue, Is.EqualTo(new byte[] { 12, 32, 34 }));
        Assert.That(doubleColumn1.DefaultValue, Is.EqualTo(84.874596565));
    }

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
