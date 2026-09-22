using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_PropertyColumnIdentityTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void AddPrimaryIdentity_Succeeds()
    {
        // Arrange
        const string testTableName = "MyDefaultTestTable";
        const string propertyName1 = "Color1";
        const string propertyName2 = "Color2";

        Provider.AddTable(testTableName,
            new Column(propertyName1,DbType.Int32){IsNullable = false,IsIdentity = true},
            new Column(propertyName2,DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + testTableName, propertyName1)        );

        var sql = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(testTableName);

        // NOT NULL implicitly set in SQLite
        Assert.That(Provider.ReadLegacyColumn(testTableName, "Color1").IsIdentity, Is.True);
        Assert.That(Provider.ReadLegacyColumn(testTableName, "Color1").IsNullable, Is.False);
    }
}
