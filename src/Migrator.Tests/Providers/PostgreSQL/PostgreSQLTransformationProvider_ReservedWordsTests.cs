using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.PostgreSQL.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

[TestFixture]
[Category("PostgreSQL")]
public class PostgreSQLTransformationProvider_ReservedWordsTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void AddIndex_IncludeColumnsWithReservedWord_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Host";

        Provider.AddTable(testTableName,
            new Column(propertyName1,DbType.Int32){IsNullable = false,IsIdentity = true},
            new Column(propertyName2,DbType.Int32){IsUnsigned = true},new PrimaryKeyConstraint("PK_" + testTableName, propertyName1)        );

        // Act/Assert
        Provider.AddIndex(testTableName, new Index
        {
            Name = "IX_WMS_OTO_Sta_OT_Pri_OTOPos",
            Unique = false,
            Clustered = false,
            KeyColumns = [propertyName1],
            IncludeColumns = [propertyName2]
        });
    }
}
