using System;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddTableTests : Generic_AddTableTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLiteTransactionAsync();
    }

    [Test]
    public void AddTable_ExplicitUniqueConstraint_IsReturnedInMetadata()
    {
        const string tableName = "MyTableName";
        const string columnName = "MyColumnName";

        // Arrange/Act
        Provider.AddTable(tableName, new Column(columnName,System.Data.DbType.Int32),new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_" + tableName + "_" + columnName, columnName));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That(Provider.GetTableConstraints(tableName).OfType<DotNetProjects.Migrator.Framework.UniqueConstraint>().Single().KeyColumns, Is.EqualTo(new[] { columnName }));

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);

        // It is no named unique so it is not listed in the Uniques list. Unique on column level is marked as obsolete.
        Assert.That(sqliteInfo.Uniques.Single().Name, Is.EqualTo("UQ_" + tableName + "_" + columnName));
    }

    [Test]
    public void AddTable_CompositePrimaryKey_EnforcesNotNull()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1,System.Data.DbType.Int32){IsNullable = false},
            new Column(columnName2,System.Data.DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, columnName1, columnName2)        );

        Provider.Insert(tableName, [columnName1, columnName2], [1, 1]);
        var ex = Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [1, 1]));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That(Provider.GetTableConstraints(tableName).OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { columnName1, columnName2 }));

        var pragmaTableInfos = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);
        Assert.That(pragmaTableInfos.Single(x => x.Name == columnName1).NotNull, Is.True);
        Assert.That(pragmaTableInfos.Single(x => x.Name == columnName2).NotNull, Is.True);

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Columns.First().Name, Is.EqualTo(columnName1));
        Assert.That(sqliteInfo.Columns[1].Name, Is.EqualTo(columnName2));

        // 19 = UNIQUE constraint failed
        Assert.That(ex.ErrorCode, Is.EqualTo(19));
    }

    [Test]
    public void AddTable_SinglePrimaryKey_EnforcesNotNull()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1,System.Data.DbType.Int32){IsNullable = false},
            new Column(columnName2,System.Data.DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, columnName1)        );

        Provider.Insert(tableName, [columnName1, columnName2], [1, 1]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [1, 2]));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);

        // In SQLite an INTEGER PRIMARY KEY column is NOT NULL implicitly (see insert asserts above)
        Assert.That(Provider.GetTableConstraints(tableName).OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { columnName1 }));

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Columns.First().Name, Is.EqualTo(columnName1));
        Assert.That(sqliteInfo.Columns[1].Name, Is.EqualTo(columnName2));
    }

    [Test]
    public void AddTable_MiscellaneousColumns_Succeeds()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1,System.Data.DbType.Int32){IsNullable = false,IsIdentity = true},
            new Column(columnName2,System.Data.DbType.Int32),new PrimaryKeyConstraint("PK_" + tableName, columnName1),new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_" + tableName + "_" + columnName2, columnName2)        );

        Provider.Insert(tableName, [columnName1, columnName2], [1, 1]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [1, 1]));

        // Assert
        var createScript = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(tableName);
        Assert.That(Provider.GetColumns(tableName).Single(c => c.Name == columnName1).IsIdentity, Is.True);
        Assert.That(Provider.GetTableConstraints(tableName).OfType<DotNetProjects.Migrator.Framework.UniqueConstraint>().Single().KeyColumns, Is.EqualTo(new[] { columnName2 }));

        var pragmaTableInfos = ((SQLiteTransformationProvider)Provider).GetPragmaTableInfoItems(tableName);
        Assert.That(pragmaTableInfos.First().NotNull, Is.True);
        Assert.That(pragmaTableInfos[1].NotNull, Is.False);

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Columns.First().Name, Is.EqualTo(columnName1));
        Assert.That(sqliteInfo.Columns[1].Name, Is.EqualTo(columnName2));
    }

    /// <summary>
    /// NOT NULL is implicitly set by SQLite
    /// </summary>
    [Test]
    public void AddTable_GuidPrimaryKeyOneColumnPKImplicitlyUsingNotNull_ThrowsOnNullAndOnDuplicates()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        var guid = Guid.NewGuid();

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1,System.Data.DbType.Guid){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, columnName1)        );

        Provider.Insert(tableName, [columnName1], [guid]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1], [guid]));

        // The migrator sets NotNull on PrimaryKey (non composite) so this line throws.
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1], [null]));
    }

    /// <summary>
    /// Composite PK with Guids
    /// </summary>
    [Test]
    public void AddTable_GuidPrimaryKeyCompositeWithGuid_RejectsNullMembers()
    {
        const string tableName = "MyTableName";
        const string columnName1 = "Column1";
        const string columnName2 = "Column2";
        var guid = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        // Arrange/Act
        Provider.AddTable(tableName,
            new Column(columnName1,System.Data.DbType.Guid){IsNullable = false},
            new Column(columnName2,System.Data.DbType.Guid){IsNullable = false},new PrimaryKeyConstraint("PK_" + tableName, columnName1, columnName2)        );

        // This is a normal SQLite behavior! 
        // NULL != NULL
        // (A, NULL) != (A, NULL)
        // Duplicates! You need to set NotNull if you want to prevent it!
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [guid, null]));
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [guid, null]));

        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [null, guid]));
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [null, guid]));

        Provider.Insert(tableName, [columnName1, columnName2], [guid2, guid2]);
        Assert.Throws<SQLiteException>(() => Provider.Insert(tableName, [columnName1, columnName2], [guid2, guid2]));
    }
}