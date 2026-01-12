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
            new Column(propertyName1, DbType.Int32, ColumnProperty.PrimaryKey | ColumnProperty.Identity),
            new Column(propertyName2, DbType.Int32, ColumnProperty.NotNull)
        );

        var sql = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript(testTableName);

        // NOT NULL implicitly set in SQLite
        Assert.That(sql, Does.Contain("Color1 INTEGER NOT NULL PRIMARY KEY"));
    }
}
