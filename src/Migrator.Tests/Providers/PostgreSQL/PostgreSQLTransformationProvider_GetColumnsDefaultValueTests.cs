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
    private const decimal DecimalDefaultValue = 14.56565m;

    [Test]
    public void GetColumns_DefaultValues_Succeeds()
    {
        // Arrange
        var dateTimeDefaultValue = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var guidDefaultValue = Guid.NewGuid();

        const string testTableName = "MyDefaultTestTable";

        const string dateTimeColumnName1 = "datetimecolumn1";
        const string dateTimeColumnName2 = "datetimecolumn2";
        const string decimalColumnName1 = "decimalcolumn";
        const string guidColumnName1 = "guidcolumn1";
        const string booleanColumnName1 = "booleancolumn1";
        const string int32ColumnName1 = "int32column1";
        const string int64ColumnName1 = "int64column1";
        const string stringColumnName1 = "stringcolumn1";
        const string binaryColumnName1 = "binarycolumn1";

        // Should be extended by remaining types
        Provider.AddTable(testTableName,
            new Column(dateTimeColumnName1, DbType.DateTime, dateTimeDefaultValue),
            new Column(dateTimeColumnName2, DbType.DateTime2, dateTimeDefaultValue),
            new Column(decimalColumnName1, DbType.Decimal, DecimalDefaultValue),
            new Column(guidColumnName1, DbType.Guid, guidDefaultValue),

            // other boolean default values are tested in another test
            new Column(booleanColumnName1, DbType.Boolean, true),

            new Column(int32ColumnName1, DbType.Int32, defaultValue: 43),
            new Column(int64ColumnName1, DbType.Int64, defaultValue: 88),
            new Column(stringColumnName1, DbType.String, defaultValue: "Hello"),
            new Column(binaryColumnName1, DbType.Binary, defaultValue: new byte[] { 12, 32, 34 })
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var dateTimeColumn1 = columns.Single(x => x.Name == dateTimeColumnName1);
        var dateTimeColumn2 = columns.Single(x => x.Name == dateTimeColumnName2);
        var decimalColumn1 = columns.Single(x => x.Name == decimalColumnName1);
        var guidColumn1 = columns.Single(x => x.Name == guidColumnName1);
        var booleanColumn1 = columns.Single(x => x.Name == booleanColumnName1);
        var int32Column1 = columns.Single(x => x.Name == int32ColumnName1);
        var int64Column1 = columns.Single(x => x.Name == int64ColumnName1);
        var stringColumn1 = columns.Single(x => x.Name == stringColumnName1);
        var binarycolumn1 = columns.Single(x => x.Name == binaryColumnName1);

        Assert.That(dateTimeColumn1.DefaultValue, Is.EqualTo(dateTimeDefaultValue));
        Assert.That(dateTimeColumn2.DefaultValue, Is.EqualTo(dateTimeDefaultValue));
        Assert.That(decimalColumn1.DefaultValue, Is.EqualTo(DecimalDefaultValue));
        Assert.That(guidColumn1.DefaultValue, Is.EqualTo(guidDefaultValue));
        Assert.That(booleanColumn1.DefaultValue, Is.True);
        Assert.That(int32Column1.DefaultValue, Is.EqualTo(43));
        Assert.That(int64Column1.DefaultValue, Is.EqualTo(88));
        Assert.That(stringColumn1.DefaultValue, Is.EqualTo("Hello"));
        Assert.That(binarycolumn1.DefaultValue, Is.EqualTo(new byte[] { 12, 32, 34 }));
    }

    // 1 will coerce to true on inserts but not for default values in Postgre SQL - same for 0 to false
    // so we do not test it here
    [TestCase("true", true)]
    [TestCase("TRUE", true)]
    [TestCase("t", true)]
    [TestCase("T", true)]
    [TestCase("yes", true)]
    [TestCase("YES", true)]
    [TestCase("y", true)]
    [TestCase("Y", true)]
    [TestCase("on", true)]
    [TestCase("ON", true)]
    [TestCase("false", false)]
    [TestCase("FALSE", false)]
    [TestCase("f", false)]
    [TestCase("F", false)]
    [TestCase("false", false)]
    [TestCase("FALSE", false)]
    [TestCase("n", false)]
    [TestCase("N", false)]
    [TestCase("off", false)]
    [TestCase("OFF", false)]
    public void GetColumns_DefaultValueBooleanValues_Succeeds(object inboundBooleanDefaultValue, bool outboundBooleanDefaultValue)
    {
        // Arrange
        var dateTimeDefaultValue = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var guidDefaultValue = Guid.NewGuid();

        const string testTableName = "MyDefaultTestTable";
        const string booleanColumnName1 = "booleancolumn1";

        Provider.AddTable(testTableName,
            new Column(booleanColumnName1, DbType.Boolean) { DefaultValue = inboundBooleanDefaultValue }
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var booleanColumn1 = columns.Single(x => x.Name == booleanColumnName1);

        Assert.That(booleanColumn1.DefaultValue, Is.EqualTo(outboundBooleanDefaultValue));
    }
}
