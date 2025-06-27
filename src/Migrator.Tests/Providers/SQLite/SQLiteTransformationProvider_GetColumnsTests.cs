using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_GetColumnsTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void CheckForeignKeyIntegrity_IntegrityViolated_ReturnsFalse()
    {
        const string tableName = "GetColumnsTest";

        // Arrange
        _provider.AddTable(tableName, new Column("Id", System.Data.DbType.Int32, ColumnProperty.Unique));

        // Act
        var columns = _provider.GetColumns(tableName);

        // Assert

    }
}
