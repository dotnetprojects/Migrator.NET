using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumns_DefaultTypeTests : TransformationProvider_GetColumns_GenericTests
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
