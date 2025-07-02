using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_AddTableTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void AddForeignKey()
    {
        const string tableName = "MyTableName";
        const string columnName = "MyColumnName";

        // Arrange/Act
        _provider.AddTable(tableName, new Column(columnName, System.Data.DbType.Int32, ColumnProperty.Unique));

        // Assert
        var createScript = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript(tableName);
        Assert.That("CREATE TABLE MyTableName (MyColumnName INTEGER UNIQUE)", Is.EqualTo(createScript));

        var sqliteInfo = ((SQLiteTransformationProvider)_provider).GetSQLiteTableInfo(tableName);
        Assert.That(sqliteInfo.Uniques.Single().KeyColumns.Single(), Is.EqualTo(columnName));
    }
}
