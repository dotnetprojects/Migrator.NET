using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_RenameColumnTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void RenameColumn_HavingASingleForeignKeyPointingToTheTargetColumn_SingleColumnForeignKeyIsRemoved()
    {
        // Arrange
        const string tableNameLevel1 = "Level1";
        const string tableNameLevel2 = "Level2";
        const string tableNameLevel3 = "Level3";
        const string propertyId = "Id";
        const string propertyIdRenamed = "IdRenamed";
        const string propertyLevel1Id = "Level1Id";
        const string propertyLevel1IdRenamed = "Level1IdRenamed";
        const string propertyLevel2Id = "Level2Id";

        Provider.AddTable(tableNameLevel1, new Column(propertyId, DbType.Int32, ColumnProperty.PrimaryKey));

        Provider.AddTable(tableNameLevel2,
            new Column(propertyId, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyLevel1Id, DbType.Int32, ColumnProperty.Unique)
        );

        Provider.AddTable(tableNameLevel3,
            new Column(propertyId, DbType.Int32, ColumnProperty.PrimaryKey),
            new Column(propertyLevel2Id, DbType.Int32)
        );

        Provider.AddForeignKey("Level2ToLevel1", tableNameLevel1, propertyId, tableNameLevel2, propertyLevel1Id);
        Provider.AddForeignKey("Level3ToLevel2", tableNameLevel2, propertyId, tableNameLevel3, propertyLevel2Id);

        var script = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableNameLevel2);

        var tableInfoLevel2Before = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel2);
        var tableInfoLevel3Before = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel3);

        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel1} ({propertyId}) VALUES (1)");
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel1} ({propertyId}) VALUES (2)");
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel2} ({propertyId}, {propertyLevel1Id}) VALUES (1, 1)");
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel3} ({propertyId}, {propertyLevel2Id}) VALUES (1, 1)");

        // Act
        Provider.RenameColumn(tableNameLevel2, propertyId, propertyIdRenamed);
        Provider.RenameColumn(tableNameLevel2, propertyLevel1Id, propertyLevel1IdRenamed);

        // Assert
        Provider.ExecuteNonQuery($"INSERT INTO {tableNameLevel2} ({propertyIdRenamed}, {propertyLevel1IdRenamed}) VALUES (2,2)");
        using var command = Provider.GetCommand();

        using var reader = Provider.ExecuteQuery(command, $"SELECT COUNT(*) as Count from {tableNameLevel2}");
        reader.Read();
        var count = reader.GetInt32(reader.GetOrdinal("Count"));
        Assert.That(count, Is.EqualTo(2));

        var tableInfoLevel2After = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableNameLevel2);

        Assert.That(tableInfoLevel2Before.Columns.Single(x => x.Name == propertyId).ColumnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.True);
        Assert.That(tableInfoLevel2Before.Columns.Single(x => x.Name == propertyLevel1Id).ColumnProperty.HasFlag(ColumnProperty.Unique), Is.True);
        Assert.That(tableInfoLevel2Before.ForeignKeys.Single().ChildColumns.Single(), Is.EqualTo(propertyLevel1Id));

        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyId), Is.Null);
        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyLevel1Id), Is.Null);
        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyIdRenamed), Is.Not.Null);
        Assert.That(tableInfoLevel2After.Columns.FirstOrDefault(x => x.Name == propertyLevel1IdRenamed), Is.Not.Null);
        Assert.That(tableInfoLevel2After.ForeignKeys.Single().ChildColumns.Single(), Is.EqualTo(propertyLevel1IdRenamed));

        var valid = ((SQLiteTransformationProvider)Provider).CheckForeignKeyIntegrity();
        Assert.That(valid, Is.True);
    }
}