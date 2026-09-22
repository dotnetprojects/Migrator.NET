using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Tests.Providers.SQLite.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProvider_RecreateTableTests : SQLiteTransformationProviderTestBase
{
    [Test]
    public void RecreateTable_HavingACompoundPrimaryKey_Success()
    {
        // Arrange
        Provider.AddTable("Common_Availability_EvRef",
            new Column("EventId",DbType.Int64){IsNullable = false},
            new Column("AvailabilityGroupId",DbType.Guid){IsNullable = false},new PrimaryKeyConstraint("PK_" + "Common_Availability_EvRef", "EventId", "AvailabilityGroupId"));

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo("Common_Availability_EvRef");
        var sql = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Availability_EvRef");

        // Act/Assert
        ((SQLiteTransformationProvider)Provider).RecreateTable(sqliteInfo);
        var sql2 = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Availability_EvRef");


        Assert.That(sql, Does.Contain("PRIMARY KEY (EventId, AvailabilityGroupId)"));

        // The quotes around the table name are added by SQLite on ALTER TABLE in RecreateTable
        Assert.That(sql2, Does.Contain("PRIMARY KEY (EventId, AvailabilityGroupId)"));
    }
}