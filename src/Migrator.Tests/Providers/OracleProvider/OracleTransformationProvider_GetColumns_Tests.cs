using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_GetColumns_Tests : Generic_GetColumnsTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }

    /// <summary>
    /// Since SQLite does not support binary default values in the generic file a separate test is needed for Oracle
    /// Find the generic test in the base class.
    /// </summary>
    [Test]
    public void GetColumns_Oracle_DefaultValues_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string binaryColumnName1 = "binarycolumn1";

        // Should be extended by remaining types
        Provider.AddTable(testTableName,
            new Column(binaryColumnName1, DbType.Binary, defaultValue: new byte[] { 12, 32, 34 })
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var binarycolumn1 = columns.Single(x => x.Name.Equals(binaryColumnName1, StringComparison.OrdinalIgnoreCase));

        Assert.That(binarycolumn1.DefaultValue, Is.EqualTo(new byte[] { 12, 32, 34 }));
    }

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
            new Column(doubleColumnName1, DbType.Double, defaultValue: 84.874596567) { Precision = 19, Scale = 10 }
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var dateTimeColumn1 = columns.Single(x => x.Name.Equals(dateTimeColumnName1, StringComparison.OrdinalIgnoreCase));
        var dateTimeColumn2 = columns.Single(x => x.Name.Equals(dateTimeColumnName2, StringComparison.OrdinalIgnoreCase));
        var decimalColumn1 = columns.Single(x => x.Name.Equals(decimalColumnName1, StringComparison.OrdinalIgnoreCase));
        var guidColumn1 = columns.Single(x => x.Name.Equals(guidColumnName1, StringComparison.OrdinalIgnoreCase));
        var booleanColumn1 = columns.Single(x => x.Name.Equals(booleanColumnName1, StringComparison.OrdinalIgnoreCase));
        var int32Column1 = columns.Single(x => x.Name.Equals(int32ColumnName1, StringComparison.OrdinalIgnoreCase));
        var int64Column1 = columns.Single(x => x.Name.Equals(int64ColumnName1, StringComparison.OrdinalIgnoreCase));
        var int64Column2 = columns.Single(x => x.Name.Equals(int64ColumnName2, StringComparison.OrdinalIgnoreCase));
        var stringColumn1 = columns.Single(x => x.Name.Equals(stringColumnName1, StringComparison.OrdinalIgnoreCase));
        var binarycolumn1 = columns.Single(x => x.Name.Equals(binaryColumnName1, StringComparison.OrdinalIgnoreCase));
        var doubleColumn1 = columns.Single(x => x.Name.Equals(doubleColumnName1, StringComparison.OrdinalIgnoreCase));

        Assert.That(dateTimeColumn1.DefaultValue, Is.EqualTo(dateTimeDefaultValue));
        Assert.That(dateTimeColumn2.DefaultValue, Is.EqualTo(dateTimeDefaultValue));
        Assert.That(decimalColumn1.DefaultValue, Is.EqualTo(decimalDefaultValue));
        Assert.That(guidColumn1.DefaultValue, Is.EqualTo(guidDefaultValue));
        Assert.That(booleanColumn1.DefaultValue, Is.True);
        Assert.That(int32Column1.DefaultValue, Is.EqualTo(43));
        Assert.That(int64Column1.DefaultValue, Is.EqualTo(88));
        Assert.That(stringColumn1.DefaultValue, Is.EqualTo("Hello"));
        Assert.That(binarycolumn1.DefaultValue, Is.EqualTo(new byte[] { 12, 32, 34 }));
        Assert.That(doubleColumn1.DefaultValue, Is.EqualTo(84.874596567));
    }

    [Test]
    public void GetColumns_GetIdentity_Succeeds()
    {
        // Arrange
        var tableName1 = "Table1";
        var tableName2 = "Table2";
        var tableName3 = "Table3";
        var tableName4 = "Table4";
        var columnName1 = "ColumnName1";

        Provider.ExecuteNonQuery($"CREATE TABLE {tableName1} ({columnName1} NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY)");
        Provider.ExecuteNonQuery($"CREATE TABLE {tableName2} ({columnName1} NUMBER PRIMARY KEY)");

        Provider.AddTable(name: tableName3, new Column(columnName1, DbType.Int32, ColumnProperty.Identity | ColumnProperty.PrimaryKey));
        Provider.AddTable(name: tableName4, new Column(columnName1, DbType.Int32, ColumnProperty.PrimaryKey));

        // Act
        var columnTable1 = Provider.GetColumnByName(table: tableName1, column: columnName1);
        var columnTable2 = Provider.GetColumnByName(table: tableName2, column: columnName1);
        var columnTable3 = Provider.GetColumnByName(table: tableName3, column: columnName1);
        var columnTable4 = Provider.GetColumnByName(table: tableName4, column: columnName1);

        // Assert
        Assert.That(columnTable1.IsIdentity, Is.True);
        Assert.That(columnTable2.IsIdentity, Is.False);
        Assert.That(columnTable3.IsIdentity, Is.True);
        Assert.That(columnTable4.IsIdentity, Is.False);
    }
}
