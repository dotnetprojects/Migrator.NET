using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumnsDefaultTypeTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void GetColumns_DataTypeResolveSucceeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string dateTimeColumnName1 = "datetimecolumn1";
        const string dateTimeColumnName2 = "datetimecolumn2";
        const string decimalColumnName1 = "decimalcolumn";
        const string guidColumnName1 = "guidcolumn1";
        const string booleanColumnName1 = "booleancolumn1";
        const string int32ColumnName1 = "int32column1";
        const string int64ColumnName1 = "int64column1";
        const string stringColumnName1 = "stringcolumn1";
        const string stringColumnName2 = "stringcolumn2";

        // Should be extended by remaining types
        Provider.AddTable(testTableName,
            new Column(dateTimeColumnName1, DbType.DateTime, new DateTime(2000, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc)),
            new Column(dateTimeColumnName2, DbType.DateTime2, new DateTime(2000, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc))
        // new Column(decimalColumnName1, DbType.Decimal),
        // new Column(guidColumnName1, DbType.Guid),
        // new Column(booleanColumnName1, DbType.Boolean),
        // new Column(int32ColumnName1, DbType.Int32),
        // new Column(int64ColumnName1, DbType.Int64),
        // new Column(stringColumnName1, DbType.String),
        // new Column(stringColumnName2, DbType.String) { Size = 30 }
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        var dateTimeColumn1 = columns.Single(x => x.Name == dateTimeColumnName1);
        // var dateTimeColumn2 = columns.Single(x => x.Name == dateTimeColumnName2);
        // var decimalColumn1 = columns.Single(x => x.Name == decimalColumnName1);
        // var guidColumn1 = columns.Single(x => x.Name == guidColumnName1);
        // var booleanColumn1 = columns.Single(x => x.Name == booleanColumnName1);
        // var int32Column1 = columns.Single(x => x.Name == int32ColumnName1);
        // var int64column1 = columns.Single(x => x.Name == int64ColumnName1);
        // var stringColumn1 = columns.Single(x => x.Name == stringColumnName1);
        // var stringColumn2 = columns.Single(x => x.Name == stringColumnName2);
    }
}
