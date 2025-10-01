using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Providers.Generic.Models;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_UpdateFromTableToTableTestsBase : TransformationProviderBase
{
    [Test]
    public void UpdateFromTableToTable_Success()
    {
        // Arrange
        const string tableNameSource = "TableSource";
        const string tableNameTarget = "TableTarget";
        const string columnName1Source = "ColumnName1Source";
        const string columnName2Source = "ColumnName2Source";
        const string columnName3Source = "ColumnName3Source";
        const string columnName4Source = "ColumnName4Source";
        const string columnName5Source = "ColumnName5Source";

        const string columnName1Target = "ColumnName1Target";
        const string columnName2Target = "ColumnName2Target";
        const string columnName3Target = "ColumnName3Target";
        const string columnName4Target = "ColumnName4Target";
        const string columnName5Target = "ColumnName5Target";


        Provider.AddTable(tableNameSource,
            new Column(columnName1Source, DbType.Int32, ColumnProperty.NotNull),
            new Column(columnName2Source, DbType.Int32, ColumnProperty.NotNull),
            new Column(columnName3Source, DbType.String),
            new Column(columnName4Source, DbType.String),
            new Column(columnName5Source, DbType.String)
        );

        Provider.AddPrimaryKey("PK_Source", tableNameSource, [columnName1Source, columnName2Source]);

        Provider.AddTable(tableNameTarget,
            new Column(columnName1Target, DbType.Int32, ColumnProperty.NotNull),
            new Column(columnName2Target, DbType.Int32, ColumnProperty.NotNull),
            new Column(columnName3Target, DbType.String),
            new Column(columnName4Target, DbType.String),
            new Column(columnName5Target, DbType.String)
       );

        Provider.AddPrimaryKey("PK_Target", tableNameTarget, [columnName1Target, columnName2Target]);

        Provider.Insert(tableNameSource, [columnName1Source, columnName2Source, columnName3Source, columnName4Source, columnName5Source], [1, 2, "source 1", "source 2", "source 3"]);
        Provider.Insert(tableNameSource, [columnName1Source, columnName2Source, columnName3Source, columnName4Source, columnName5Source], [2, 3, "source 11", "source 22", "source 33"]);

        Provider.Insert(tableNameTarget, [columnName1Target, columnName2Target, columnName3Target, columnName4Target, columnName5Target], [1, 2, "target 1", "target 2", "target 3"]);
        Provider.Insert(tableNameTarget, [columnName1Target, columnName2Target, columnName3Target, columnName4Target, columnName5Target], [1, 3, "target no update", "target no update", "target no update"]);

        // Act
        Provider.UpdateTargetFromSource(
            tableNameSource,
            tableNameTarget,
            [
                new () { ColumnNameSource = columnName3Source, ColumnNameTarget = columnName3Target },
                new () { ColumnNameSource = columnName4Source, ColumnNameTarget = columnName4Target },
                new () { ColumnNameSource = columnName5Source, ColumnNameTarget = columnName5Target }
            ],
            [
                new () { ColumnNameSource = columnName1Source, ColumnNameTarget = columnName1Target },
                new () { ColumnNameSource = columnName2Source, ColumnNameTarget = columnName2Target }
            ]);

        // Assert
        List<UpdateFromTableToTableModel> targetRows = [];
        using (var cmd = Provider.CreateCommand())
        using (var reader = Provider.Select(cmd, tableNameTarget, [columnName1Target, columnName2Target, columnName3Target, columnName4Target, columnName5Target]))
        {
            while (reader.Read())
            {
                targetRows.Add(new UpdateFromTableToTableModel
                {
                    Column1 = reader.GetInt32(0),
                    Column2 = reader.GetInt32(1),
                    Column3 = reader.GetString(2),
                    Column4 = reader.GetString(3),
                    Column5 = reader.GetString(4)
                });
            }
        }

        List<UpdateFromTableToTableModel> expectedTargetRows = [
            new UpdateFromTableToTableModel{ Column1 = 1, Column2 = 2, Column3 = "source 1", Column4 = "source 2", Column5 = "source 3"},
            new UpdateFromTableToTableModel{ Column1 = 1, Column2 = 3, Column3 = "target no update", Column4 = "target no update", Column5 = "target no update"},
        ];

        Assert.That(targetRows, Is.EquivalentTo(expectedTargetRows).Using<UpdateFromTableToTableModel>((x, y) =>
            x.Column1 == y.Column1 &&
            x.Column2 == y.Column2 &&
            x.Column3 == y.Column3 &&
            x.Column4 == y.Column4 &&
            x.Column5 == y.Column5));
    }
}