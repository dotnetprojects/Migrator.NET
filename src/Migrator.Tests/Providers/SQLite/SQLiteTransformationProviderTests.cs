using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProviderTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void GetTables()
    {
        var tables = _provider.GetTables();

        Assert.That("TestTwo", Is.EqualTo(tables.Single()));
    }

    [Test]
    public void CanParseColumnDefForNotNull()
    {
        const string nullString = "bar TEXT";
        const string notNullString = "baz INTEGER NOT NULL";

        Assert.That(((SQLiteTransformationProvider)_provider).IsNullable(nullString), Is.True);
        Assert.That(((SQLiteTransformationProvider)_provider).IsNullable(notNullString), Is.False);
    }

    [Test]
    public void RemoveDefaultValue_Success()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";
        var columnName = "Bla";

        _provider.AddTable(testTableName, new Column(columnName, DbType.Int32, (object)55));
        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);
        var createScriptBefore = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript(testTableName);

        // Act
        _provider.RemoveColumnDefaultValue(testTableName, columnName);

        // Assert
        var tableInfoAfter = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);
        var createScriptAfter = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript(testTableName);
        var tableNames = ((SQLiteTransformationProvider)_provider).GetTables();

        Assert.That(tableInfoBefore.Columns.Single().DefaultValue, Is.EqualTo(55));
        Assert.That(tableInfoAfter.Columns.Single().DefaultValue, Is.Null);
        Assert.That(createScriptBefore, Does.Contain("DEFAULT 55"));
        Assert.That(createScriptAfter, Does.Not.Contain("DEFAULT"));

        // Check for intermediate table residues.
        Assert.That(tableNames.Where(x => x.Contains(testTableName)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddPrimaryKey_CompositePrimaryKey_Success()
    {
        // Arrange
        var testTableName = "MyDefaultTestTable";

        _provider.AddTable(testTableName,
            new Column("Id", DbType.Int32),
            new Column("Color", DbType.Int32),
            new Column("NotAPrimaryKey", DbType.Int32)
        );

        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        // Act
        _provider.AddPrimaryKey("MyPrimaryKeyName", testTableName, "Id", "Color");

        // Assert
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "Id").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "Color").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "NotAPrimaryKey").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        var tableInfoAfter = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);
        var tableNames = ((SQLiteTransformationProvider)_provider).GetTables();

        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "Id").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "Color").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "NotAPrimaryKey").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        // Check for intermediate table residues.
        Assert.That(tableNames.Where(x => x.Contains(testTableName)), Has.Exactly(1).Items);
    }

    [Test]
    public void AddUnique_Success()
    {
        // TODO 

        // Arrange
        var testTableName = "MyDefaultTestTable";

        _provider.AddTable(testTableName,
            new Column("Color", DbType.Int32, ColumnProperty.Unique)
        );

        var tableInfoBefore = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);

        // // Act
        // _provider.AddPrimaryKey("MyPrimaryKeyName", testTableName, "Id", "Color");

        // // Assert
        // Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "Id").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        // Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "Color").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
        // Assert.That(tableInfoBefore.Columns.Single(x => x.Name == "NotAPrimaryKey").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        // var tableInfoAfter = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(testTableName);
        // var tableNames = ((SQLiteTransformationProvider)_provider).GetTables();

        // Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "Id").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        // Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "Color").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        // Assert.That(tableInfoAfter.Columns.Single(x => x.Name == "NotAPrimaryKey").ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);

        // // Check for intermediate table residues.
        // Assert.That(tableNames.Where(x => x.Contains(testTableName)), Has.Exactly(1).Items);
    }
}
