using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_RemoveAllConstraints : SQLiteTransformationProviderTestBase
{
    [Test]
    public void RemoveColumn_HavingNoCompositeIndexAndNoCompositeUniqueConstraint_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";
        const string propertyName3 = "Color3";
        const string indexName = "MyIndexName";

        Provider.AddTable(testTableName,
            new Column(propertyName1,DbType.Int32){IsNullable = false},
            new Column(propertyName2,DbType.Int32),
            new Column(propertyName3,DbType.Int32),new PrimaryKeyConstraint("PK_" + testTableName, propertyName1),new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_" + testTableName + "_" + propertyName2, propertyName2),new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_" + testTableName + "_" + propertyName3, propertyName3)        );

        Provider.AddIndex(indexName, testTableName, [propertyName1]);
        var tableInfoBefore = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);

        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}, {propertyName2}) VALUES (1, 2)");

        // Act
        Provider.RemoveAllConstraints(testTableName);
        Provider.ExecuteNonQuery($"INSERT INTO {testTableName} ({propertyName1}) VALUES (2)");

        // Assert
        using var command = Provider.GetCommand();
        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {testTableName}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoAfter = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(testTableName);
        var sqlAfter = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(testTableName);

        Assert.That((tableInfoBefore.PrimaryKey?.KeyColumns.Contains(propertyName1) == true), Is.True);
        Assert.That(tableInfoBefore.Uniques.Any(u => u.KeyColumns.Length == 1 && u.KeyColumns[0] == propertyName2), Is.True);
        Assert.That(tableInfoBefore.Uniques.Any(u => u.KeyColumns.Length == 1 && u.KeyColumns[0] == propertyName3), Is.True);

        Assert.That((tableInfoAfter.PrimaryKey?.KeyColumns.Contains(propertyName1) == true), Is.False);
        Assert.That(tableInfoAfter.Uniques.Any(u => u.KeyColumns.Length == 1 && u.KeyColumns[0] == propertyName2), Is.False);
        Assert.That(tableInfoAfter.Uniques.Any(u => u.KeyColumns.Length == 1 && u.KeyColumns[0] == propertyName3), Is.False);

        Assert.That(sqlAfter.Contains("unique", StringComparison.OrdinalIgnoreCase), Is.False);

        var indexAfter = tableInfoAfter.Indexes.Single();

        Assert.That(indexAfter.Name, Is.EqualTo(indexName));
        CollectionAssert.AreEquivalent(indexAfter.KeyColumns, new string[] { propertyName1 });
    }
}