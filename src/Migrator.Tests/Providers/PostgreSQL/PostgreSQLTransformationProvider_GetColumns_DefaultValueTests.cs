using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumns_DefaultValuesTests : TransformationProviderBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginPostgreSQLTransactionAsync();
    }

    /// <summary>
    /// More tests for GetColumns  <see cref="ITransformationProvider.GetColumns"/> in <see cref="TransformationProviderBase"/>
    /// </summary>
    [Test]
    public void GetColumns_Postgres_DefaultValues_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string intervalColumnName1 = "intervalcolumn1";
        const string intervalColumnName2 = "intervalcolumn2";
        const string binaryColumnName1 = "binarycolumn1";

        // Should be extended by remaining types
        Provider.AddTable(testTableName,
            new Column(intervalColumnName1, MigratorDbType.Interval, defaultValue: new TimeSpan(100000, 3, 4, 5, 666)),
            new Column(intervalColumnName2, MigratorDbType.Interval, defaultValue: new TimeSpan(0, 0, 0, 0, 666)),
            new Column(binaryColumnName1, DbType.Binary, defaultValue: new byte[] { 12, 32, 34 })
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var intervalColumn1 = columns.Single(x => x.Name == intervalColumnName1);
        var intervalColumn2 = columns.Single(x => x.Name == intervalColumnName2);
        var binarycolumn1 = columns.Single(x => x.Name.Equals(binaryColumnName1, StringComparison.OrdinalIgnoreCase));

        Assert.That(intervalColumn1.DefaultValue, Is.EqualTo(new TimeSpan(100000, 3, 4, 5, 666)));
        Assert.That(intervalColumn2.DefaultValue, Is.EqualTo(new TimeSpan(0, 0, 0, 0, 666)));
        Assert.That(binarycolumn1.DefaultValue, Is.EqualTo(new byte[] { 12, 32, 34 }));
    }

    [Test]
    public void GetColumns_DefaultValues_Succeeds()
    {
        // Arrange
        var dateTimeDefaultValue = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var dateTimeOffsetDefaultValue = new DateTimeOffset(2022, 2, 2, 3, 3, 4, 4, TimeSpan.FromHours(1));
        var guidDefaultValue = Guid.NewGuid();
        var decimalDefaultValue = 14.56565m;

        const string testTableName = "MyDefaultTestTable";

        const string dateTimeColumnName1 = "datetimecolumn1";
        const string dateTimeColumnName2 = "datetimecolumn2";
        const string dateTimeOffsetColumnName1 = "datetimeoffset1";
        const string decimalColumnName1 = "decimalcolumn";
        const string guidColumnName1 = "guidcolumn1";
        const string booleanColumnName1 = "booleancolumn1";
        const string int32ColumnName1 = "int32column1";
        const string int64ColumnName1 = "int64column1";
        const string int64ColumnName2 = "int64column2";
        const string int64ColumnName3 = "int64column3";
        const string stringColumnName1 = "stringcolumn1";
        const string binaryColumnName1 = "binarycolumn1";
        const string doubleColumnName1 = "doublecolumn1";

        // Should be extended by remaining types
        Provider.AddTable(testTableName,
            new Column(dateTimeColumnName1, DbType.DateTime, dateTimeDefaultValue),
            new Column(dateTimeColumnName2, DbType.DateTime2, dateTimeDefaultValue),
            new Column(dateTimeOffsetColumnName1, DbType.DateTimeOffset, dateTimeOffsetDefaultValue),
            new Column(decimalColumnName1, DbType.Decimal, decimalDefaultValue),
            new Column(guidColumnName1, DbType.Guid, guidDefaultValue),

            // other boolean default values are tested in another test
            new Column(booleanColumnName1, DbType.Boolean, true),

            new Column(int32ColumnName1, DbType.Int32, defaultValue: 43),
            new Column(int64ColumnName1, DbType.Int64, defaultValue: 88),
            new Column(int64ColumnName2, DbType.Int64, defaultValue: 0),
            // converted in postgre to ''0'::bigint'
            new Column(int64ColumnName3, DbType.Int64, defaultValue: "0"),
            new Column(stringColumnName1, DbType.String, defaultValue: "Hello"),
            new Column(binaryColumnName1, DbType.Binary, defaultValue: new byte[] { 12, 32, 34 }),
            new Column(doubleColumnName1, DbType.Double, defaultValue: 84.874596567) { Precision = 19, Scale = 10 }
        );

        // Act
        var columns = Provider.GetColumns(testTableName);

        // Assert
        var dateTimeColumn1 = columns.Single(x => x.Name.Equals(dateTimeColumnName1, StringComparison.OrdinalIgnoreCase));
        var dateTimeColumn2 = columns.Single(x => x.Name.Equals(dateTimeColumnName2, StringComparison.OrdinalIgnoreCase));
        var dateTimeOffsetColumn1 = columns.Single(x => x.Name.Equals(dateTimeOffsetColumnName1, StringComparison.OrdinalIgnoreCase));
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
        Assert.That(dateTimeOffsetColumn1.DefaultValue, Is.EqualTo(dateTimeOffsetDefaultValue));
        Assert.That(decimalColumn1.DefaultValue, Is.EqualTo(decimalDefaultValue));
        Assert.That(guidColumn1.DefaultValue, Is.EqualTo(guidDefaultValue));
        Assert.That(booleanColumn1.DefaultValue, Is.True);
        Assert.That(int32Column1.DefaultValue, Is.EqualTo(43));
        Assert.That(int64Column1.DefaultValue, Is.EqualTo(88));
        Assert.That(stringColumn1.DefaultValue, Is.EqualTo("Hello"));
        Assert.That(binarycolumn1.DefaultValue, Is.EqualTo(new byte[] { 12, 32, 34 }));
        Assert.That(doubleColumn1.DefaultValue, Is.EqualTo(84.874596567));
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
