using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("Postgre")]
public class PostgreSQLTransformationProvider_GetColumnsTypeTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void AddTableWithPrimaryKeyIdentity_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";

        Provider.AddTable(testTableName,
            new Column(propertyName2, DbType.Decimal)
        );

        // Act
        var column = Provider.GetColumns(testTableName).Single(x => x.Name.Equals(propertyName1, StringComparison.OrdinalIgnoreCase));
        Provider.Insert(testTableName, [propertyName2], [3.448484]);

        throw new NotImplementedException();
    }
}
