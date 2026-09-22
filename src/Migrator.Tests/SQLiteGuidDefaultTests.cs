using System;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using NUnit.Framework;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;

namespace Migrator.Tests;

[Category("SQLite")]
public class SQLiteGuidDefaultTests
{
    private static IDbConnection OpenConnection(bool systemData)
    {
        IDbConnection connection = systemData
            ? new System.Data.SQLite.SQLiteConnection("Data Source=:memory:")
            : new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    [TestCase(false, "00000000-0000-0000-0000-000000000000")]
    [TestCase(true, "00000000-0000-0000-0000-000000000000")]
    [TestCase(false, "00112233-4455-6677-8899-aabbccddeeff")]
    [TestCase(true, "00112233-4455-6677-8899-aabbccddeeff")]
    public void GuidDefaultsMatchInsertedKeysBeforeAndAfterRebuild(bool systemData, string value)
    {
        using var connection = OpenConnection(systemData);
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var id = Guid.Parse(value);
        provider.AddTable("Parents", new Column("Id", DbType.Guid), new PrimaryKeyConstraint("PK_Parents", "Id"));
        provider.Insert("Parents", ["Id"], [id]);
        provider.AddTable("Children", new Column("Id", DbType.Int32),
            new Column("ParentId", DbType.Guid) { DefaultValue = id },
            new ForeignKeyConstraint("FK_Children_Parents", "Parents", ["Id"], "Children", ["ParentId"]));
        provider.Insert("Children", ["Id"], [1]);
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
        Assert.That(provider.ExecuteScalar("SELECT hex(ParentId) FROM Children"), Is.EqualTo(Convert.ToHexString(id.ToByteArray())));
        provider.AddColumn("Children", new Column("Extra", DbType.Int32));
        provider.Insert("Children", ["Id"], [2]);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Children JOIN Parents ON Children.ParentId = Parents.Id"), Is.EqualTo(2));
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AddingGuidColumnBackfillsTheSameRepresentationAsParameters(bool systemData)
    {
        using var connection = OpenConnection(systemData);
        using var provider = (SQLiteTransformationProvider)ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        provider.AddTable("Parents", new Column("Id", DbType.Guid), new PrimaryKeyConstraint("PK_Parents", "Id"));
        provider.Insert("Parents", ["Id"], [id]);
        provider.AddTable("Children", new Column("Id", DbType.Int32));
        provider.Insert("Children", ["Id"], [1]);
        provider.AddColumn("Children", new Column("ParentId", DbType.Guid) { DefaultValue = id, IsNullable = false });
        provider.AddForeignKey("FK_Children_Parents", "Children", "ParentId", "Parents", "Id");
        Assert.That(provider.CheckForeignKeyIntegrity(), Is.True);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Children JOIN Parents ON Children.ParentId = Parents.Id"), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RebuildingLegacyTextGuidDefaultsPreservesTheirStorageRepresentation(bool systemData)
    {
        using var connection = OpenConnection(systemData);
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        const string value = "00112233-4455-6677-8899-aabbccddeeff";
        provider.ExecuteNonQuery("CREATE TABLE Legacy (Id INTEGER, Token UNIQUEIDENTIFIER DEFAULT '" + value + "')");
        provider.Insert("Legacy", ["Id"], [1]);
        provider.AddColumn("Legacy", new Column("Extra", DbType.Int32));
        provider.Insert("Legacy", ["Id"], [2]);
        Assert.That(provider.ExecuteScalar("SELECT COUNT(*) FROM Legacy WHERE typeof(Token) = 'text' AND Token = '" + value + "'"), Is.EqualTo(2));
    }
}
