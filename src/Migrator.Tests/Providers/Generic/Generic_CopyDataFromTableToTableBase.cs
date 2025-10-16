using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Providers.Generic.Models;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_CopyDataFromTableToTableBase : TransformationProviderBase
{
    [Test]
    public void CopyDataFromTableToTable_UsingOrderBy_Success()
    {
        // Arrange
        const string tableNameSource = "SourceTable";
        const string columnName1Source = "SourceColumn1";
        const string columnName2Source = "SourceColumn2";
        const string columnName3Source = "SourceColumn3";

        const string tableNameTarget = "TargetTable";
        const string columnName1Target = "TargetColumn1";
        const string columnName2Target = "TargetColumn2";
        const string columnName3Target = "TargetColumn3";

        Provider.AddTable(tableNameSource,
            new Column(columnName1Source, DbType.Int32),
            new Column(columnName2Source, DbType.String),
            new Column(columnName3Source, DbType.Int32)
        );

        Provider.AddTable(tableNameTarget,
            new Column(columnName1Target, DbType.Int32),
            new Column(columnName2Target, DbType.String),
            new Column(columnName3Target, DbType.Int32)
        );

        Provider.Insert(tableNameSource, [columnName1Source, columnName2Source, columnName3Source], [2, "Hello2", 22]);
        Provider.Insert(tableNameSource, [columnName1Source, columnName2Source, columnName3Source], [1, "Hello1", 11]);

        // Act
        Provider.CopyDataFromTableToTable(
            tableNameSource,
            [columnName1Source, columnName2Source, columnName3Source],
            tableNameTarget,
            [columnName1Target, columnName2Target, columnName3Target],
            [columnName1Source]);

        // Assert
        List<CopyDataFromTableToTableModel> targetRows = [];
        using (var cmd = Provider.CreateCommand())
        using (var reader = Provider.Select(cmd, tableNameTarget, [columnName1Target, columnName2Target, columnName3Target]))
        {
            while (reader.Read())
            {
                targetRows.Add(new CopyDataFromTableToTableModel
                {
                    Column1 = reader.GetInt32(0),
                    Column2 = reader.GetString(1),
                    Column3 = reader.GetInt32(2),
                });
            }
        }

        List<CopyDataFromTableToTableModel> expectedTargetRows = [
            new CopyDataFromTableToTableModel{ Column1 = 1, Column2 = "Hello1", Column3 = 11 },
            new CopyDataFromTableToTableModel{ Column1 = 2, Column2 = "Hello2", Column3 = 22 },
        ];

        Assert.That(targetRows, Is.EquivalentTo(expectedTargetRows).Using<CopyDataFromTableToTableModel>((x, y) =>
            x.Column1 == y.Column1 &&
            x.Column2 == y.Column2 &&
            x.Column3 == y.Column3));
    }

    [Test]
    public void CopyDataFromTableToTable_NotUsingOrderBy_Success()
    {
        // Arrange
        const string tableNameSource = "SourceTable";
        const string columnName1Source = "SourceColumn1";
        const string columnName2Source = "SourceColumn2";
        const string columnName3Source = "SourceColumn3";

        const string tableNameTarget = "TargetTable";
        const string columnName1Target = "TargetColumn1";
        const string columnName2Target = "TargetColumn2";
        const string columnName3Target = "TargetColumn3";

        Provider.AddTable(tableNameSource,
            new Column(columnName1Source, DbType.Int32),
            new Column(columnName2Source, DbType.String),
            new Column(columnName3Source, DbType.Int32)
        );

        Provider.AddTable(tableNameTarget,
            new Column(columnName1Target, DbType.Int32),
            new Column(columnName2Target, DbType.String),
            new Column(columnName3Target, DbType.Int32)
        );

        Provider.Insert(tableNameSource, [columnName1Source, columnName2Source, columnName3Source], [2, "Hello2", 22]);
        Provider.Insert(tableNameSource, [columnName1Source, columnName2Source, columnName3Source], [1, "Hello1", 11]);

        // Act
        Provider.CopyDataFromTableToTable(
            tableNameSource,
            [columnName1Source, columnName2Source, columnName3Source],
            tableNameTarget,
            [columnName1Target, columnName2Target, columnName3Target]);

        // Assert
        List<CopyDataFromTableToTableModel> targetRows = [];
        using (var cmd = Provider.CreateCommand())
        using (var reader = Provider.Select(cmd, tableNameTarget, [columnName1Target, columnName2Target, columnName3Target]))
        {
            while (reader.Read())
            {
                targetRows.Add(new CopyDataFromTableToTableModel
                {
                    Column1 = reader.GetInt32(0),
                    Column2 = reader.GetString(1),
                    Column3 = reader.GetInt32(2),
                });
            }
        }

        List<CopyDataFromTableToTableModel> expectedTargetRows = [
            new CopyDataFromTableToTableModel{ Column1 = 1, Column2 = "Hello1", Column3 = 11 },
            new CopyDataFromTableToTableModel{ Column1 = 2, Column2 = "Hello2", Column3 = 22 },
        ];

        Assert.That(targetRows, Is.EquivalentTo(expectedTargetRows).Using<CopyDataFromTableToTableModel>((x, y) =>
            x.Column1 == y.Column1 &&
            x.Column2 == y.Column2 &&
            x.Column3 == y.Column3));
    }
}