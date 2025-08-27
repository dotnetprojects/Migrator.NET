using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_AddIndex_Tests : Generic_AddIndexTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }

    [Test]
    public void AddIndex_Unique_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName = "TestColumn";
        const string columnName2 = "TestColumn2";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName, new Column(columnName, DbType.Int32), new Column(columnName2, DbType.String));

        // Act
        Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [columnName],
                Unique = true,
            });

        // Assert
        Provider.Insert(tableName, [columnName, columnName2], [1, "Hello"]);
        var ex = Assert.Throws<OracleException>(() => Provider.Insert(tableName, [columnName, columnName2], [1, "Some other string"]));
        var index = Provider.GetIndexes(tableName).Single();

        Assert.That(index.Unique, Is.True);
        Assert.That(ex.Number, Is.EqualTo(1));
    }

    /// <summary>
    /// This test is located in the dedicated database type folder not in the base class since <see cref="OracleTransformationProvider.GetIndexes"/>
    /// cannot read filter items.
    /// </summary>
    [Test]
    public void AddIndex_FilteredIndexMiscellaneousFilterTypesAndDataTypes_Success()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";
        const string columnName2 = "TestColumn2";
        const string columnName3 = "TestColumn3";
        const string columnName4 = "TestColumn4";
        const string columnName5 = "TestColumn5";
        const string columnName6 = "TestColumn6";
        const string columnName7 = "TestColumn7";
        const string columnName8 = "TestColumn8";
        const string columnName9 = "TestColumn9";
        const string columnName10 = "TestColumn10";
        const string columnName11 = "TestColumn11";
        const string columnName12 = "TestColumn12";
        const string columnName13 = "TestColumn13";

        const string indexName = "TestIndexName";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int16),
            new Column(columnName2, DbType.Int32),
            new Column(columnName3, DbType.Int64),
            new Column(columnName4, DbType.UInt16),
            new Column(columnName5, DbType.UInt32),
            new Column(columnName6, DbType.UInt64),
            new Column(columnName7, DbType.String),
            new Column(columnName8, DbType.Int32),
            new Column(columnName9, DbType.Int32),
            new Column(columnName10, DbType.Int32),
            new Column(columnName11, DbType.Int32),
            new Column(columnName12, DbType.Int32),
            new Column(columnName13, DbType.Int32)
        );

        List<FilterItem> filterItems = [
            new() { Filter = FilterType.EqualTo, ColumnName = columnName1, Value = 1 },
            new() { Filter = FilterType.GreaterThan, ColumnName = columnName2, Value = 2 },
            new() { Filter = FilterType.GreaterThanOrEqualTo, ColumnName = columnName3, Value = 2323 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName4, Value = 3434 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName5, Value = -3434 },
            new() { Filter = FilterType.SmallerThan, ColumnName = columnName6, Value = 3434345345 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName7, Value = "asdf" },
            new() { Filter = FilterType.EqualTo, ColumnName = columnName8, Value = 11 },
            new() { Filter = FilterType.GreaterThan, ColumnName = columnName9, Value = 22 },
            new() { Filter = FilterType.GreaterThanOrEqualTo, ColumnName = columnName10, Value = 33 },
            new() { Filter = FilterType.NotEqualTo, ColumnName = columnName11, Value = 44 },
            new() { Filter = FilterType.SmallerThan, ColumnName = columnName12, Value = 55 },
            new() { Filter = FilterType.SmallerThanOrEqualTo, ColumnName = columnName13, Value = 66 }
        ];

        // Act
        var addIndexSql = Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [
                    columnName1,
                    columnName2,
                    columnName3,
                    columnName4,
                    columnName5,
                    columnName6,
                    columnName7,
                    columnName8,
                    columnName9,
                    columnName10,
                    columnName11,
                    columnName12,
                    columnName13
                ],
                Unique = false,
                FilterItems = filterItems
            });

        Provider.Insert(table: tableName, [columnName1], [1]);

        // Assert
        var indexesFromDatabase = Provider.GetIndexes(table: tableName);

        // In Oracle it seems that functional expressions are stored as column with generated column name. FilterItems are not 
        // implemented in Provider.GetIndexes() for Oracle. No further assert possible at this point in time.
        Assert.That(indexesFromDatabase.Single().KeyColumns.Count, Is.EqualTo(13));


        var expectedSql = "CREATE INDEX TestIndexName ON TestTable (CASE WHEN TestColumn1 = 1 THEN TestColumn1 ELSE NULL END, CASE WHEN TestColumn2 > 2 THEN TestColumn2 ELSE NULL END, CASE WHEN TestColumn3 >= 2323 THEN TestColumn3 ELSE NULL END, CASE WHEN TestColumn4 <> 3434 THEN TestColumn4 ELSE NULL END, CASE WHEN TestColumn5 <> -3434 THEN TestColumn5 ELSE NULL END, CASE WHEN TestColumn6 < 3434345345 THEN TestColumn6 ELSE NULL END, CASE WHEN TestColumn7 <> 'asdf' THEN TestColumn7 ELSE NULL END, CASE WHEN TestColumn8 = 11 THEN TestColumn8 ELSE NULL END, CASE WHEN TestColumn9 > 22 THEN TestColumn9 ELSE NULL END, CASE WHEN TestColumn10 >= 33 THEN TestColumn10 ELSE NULL END, CASE WHEN TestColumn11 <> 44 THEN TestColumn11 ELSE NULL END, CASE WHEN TestColumn12 < 55 THEN TestColumn12 ELSE NULL END, CASE WHEN TestColumn13 <= 66 THEN TestColumn13 ELSE NULL END)";

        Assert.That(addIndexSql, Is.EqualTo(expectedSql));
    }

    /// <summary>
    /// Migrator throws if UNIQUE is used with functional expressions.
    /// </summary>
    [Test]
    public void AddIndex_FilterItemsCombinedWithUnique_Throws()
    {
        // Arrange
        const string tableName = "TestTable";
        const string columnName1 = "TestColumn1";
        const string indexName = "TestIndexName";

        Provider.AddTable(tableName,
            new Column(columnName1, DbType.Int16)
        );

        List<FilterItem> filterItems = [
            new() { Filter = FilterType.EqualTo, ColumnName = columnName1, Value = 1 },
        ];

        // Act/Assert
        Assert.Throws<MigrationException>(() => Provider.AddIndex(tableName,
            new Index
            {
                Name = indexName,
                KeyColumns = [
                    columnName1
                ],
                Unique = true,
                FilterItems = filterItems
            }));
    }
}