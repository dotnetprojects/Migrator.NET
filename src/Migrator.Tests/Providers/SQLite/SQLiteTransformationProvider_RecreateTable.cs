using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
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
            new Column("EventId", DbType.Int64, ColumnProperty.NotNull | ColumnProperty.PrimaryKey),
            new Column("AvailabilityGroupId", DbType.Guid, ColumnProperty.NotNull | ColumnProperty.PrimaryKey));

        var sqliteInfo = ((SQLiteTransformationProvider)Provider).GetSQLiteTableInfo("Common_Availability_EvRef");
        var sql = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Availability_EvRef");

        // Act/Assert
        ((SQLiteTransformationProvider)Provider).RecreateTable(sqliteInfo);
        var sql2 = ((SQLiteTransformationProvider)Provider).GetSqlCreateTableScript("Common_Availability_EvRef");


        Assert.That(sql, Is.EqualTo("CREATE TABLE Common_Availability_EvRef (EventId INTEGER NOT NULL, AvailabilityGroupId UNIQUEIDENTIFIER NOT NULL, PRIMARY KEY (EventId, AvailabilityGroupId))"));

        // The quotes around the table name are added by SQLite on ALTER TABLE in RecreateTable
        Assert.That(sql2, Is.EqualTo("CREATE TABLE \"Common_Availability_EvRef\" (EventId INTEGER NOT NULL, AvailabilityGroupId UNIQUEIDENTIFIER NOT NULL, PRIMARY KEY (EventId, AvailabilityGroupId))"));
    }
}