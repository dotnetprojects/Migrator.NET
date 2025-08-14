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
public class OracleTransformationProvider_GetColumns_Tests : TransformationProvider_GetColumns_GenericTests
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
}
