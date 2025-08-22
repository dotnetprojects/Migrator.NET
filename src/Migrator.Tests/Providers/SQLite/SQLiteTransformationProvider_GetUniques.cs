using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetUniquesTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void GetUniques_Success()
    {
        // Arrange
        const string tableNameA = "TableA";
        const string property1 = "Property1";
        const string property2 = "Property2";
        const string property3 = "Property3";
        const string property4 = "Property4";
        const string property5 = "Property5";
        const string uniqueConstraintName1 = "UniqueConstraint1";
        const string uniqueConstraintName2 = "UniqueConstraint2";
        const string uniqueIndexName1 = "IndexUnique1";
        const string nonUniqueIndexName1 = "IndexNonUnique1";

        Provider.AddTable(tableNameA,
            new Column(property1, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(property2, DbType.Int32, ColumnProperty.Unique),
            new Column(property3, DbType.Int32),
            new Column(property4, DbType.Int32),
            new Column(property5, DbType.Int32)
        );

        Provider.AddUniqueConstraint(uniqueConstraintName1, tableNameA, property3);
        Provider.AddUniqueConstraint(uniqueConstraintName2, tableNameA, property4, property5);

        // Add unique index in order to assert there is no interference with unique constraints
        Provider.AddIndex(tableNameA, new Index { Name = nonUniqueIndexName1, KeyColumns = [property4], Unique = false });
        Provider.AddIndex(tableNameA, new Index { Name = uniqueIndexName1, KeyColumns = [property5], Unique = true });

        // Act
        var uniqueConstraints = ((SQLiteTransformationProvider)Provider).GetUniques(tableNameA);
        var indexes = Provider.GetIndexes(tableNameA);

        // Assert
        var sql = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableNameA);

        Assert.That(uniqueConstraints.Count, Is.EqualTo(3));
        Assert.That(uniqueConstraints.Single(x => x.Name == uniqueConstraintName1).KeyColumns, Is.EqualTo([property3]));
        Assert.That(uniqueConstraints.Single(x => x.Name == uniqueConstraintName2).KeyColumns, Is.EqualTo([property4, property5]));

        Assert.That(sql, Does.Contain("CONSTRAINT UniqueConstraint1 UNIQUE (Property3)"));
        Assert.That(sql, Does.Contain("CONSTRAINT UniqueConstraint2 UNIQUE (Property4, Property5)"));
        Assert.That(sql, Does.Contain("CONSTRAINT sqlite_autoindex_TableA_1 UNIQUE (Property2)"));

        var retrievedUniqueIndex1 = indexes.Single(x => x.Name == uniqueIndexName1);

        Assert.That(retrievedUniqueIndex1.Unique, Is.True);
        Assert.That(retrievedUniqueIndex1.Name, Is.EqualTo(uniqueIndexName1));

        var retrievedNonUniqueIndex1 = indexes.Single(x => x.Name == nonUniqueIndexName1);

        Assert.That(retrievedNonUniqueIndex1.Unique, Is.False);
    }
}
