using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using NUnit.Framework;

namespace Migrator.Tests;

public class OracleColumnAlterationTests
{
    private sealed class RecordingProvider(Column existing)
        : OracleTransformationProvider(new OracleDialect(), (IDbConnection)null, null, "default", null)
    {
        public List<string> Commands { get; } = [];
        public override Column GetColumnByName(string table, string columnName) => existing;
        public override int ExecuteNonQuery(string sql)
        {
            Commands.Add(sql);
            return 0;
        }
    }

    [TestCase(DbType.String, false)]
    [TestCase(DbType.String, true)]
    [TestCase(DbType.AnsiString, false)]
    [TestCase(DbType.AnsiString, true)]
    [TestCase(DbType.Binary, false)]
    [TestCase(DbType.Binary, true)]
    public void UnchangedLobTypeChangesNullabilityWithoutRestatingType(DbType type, bool nullable)
    {
        var existing = new Column("Status", type, int.MaxValue) { IsNullable = !nullable };
        var requested = new Column("Status", type, int.MaxValue) { IsNullable = nullable };
        using var provider = new RecordingProvider(existing);

        provider.ChangeColumn("Nagel_AzureServiceBusIn", requested);

        Assert.That(provider.Commands, Is.EqualTo(new[]
        {
            "ALTER TABLE Nagel_AzureServiceBusIn MODIFY (Status DEFAULT NULL)",
            "ALTER TABLE Nagel_AzureServiceBusIn MODIFY (Status " + (nullable ? "NULL" : "NOT NULL") + ")"
        }));
        Assert.That(existing.IsNullable, Is.EqualTo(!nullable));
        Assert.That(requested.IsNullable, Is.EqualTo(nullable));
        Assert.That(requested.Size, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void UnchangedLobWithUnchangedNullabilityOnlyRemovesDefault()
    {
        using var provider = new RecordingProvider(new Column("Status", DbType.String, int.MaxValue) { IsNullable = false });
        provider.ChangeColumn("Example", new Column("Status", DbType.String, int.MaxValue) { IsNullable = false });
        Assert.That(provider.Commands, Is.EqualTo(new[] { "ALTER TABLE Example MODIFY (Status DEFAULT NULL)" }));
    }

    [Test]
    public void UnchangedLobAppliesDefaultSeparatelyFromNullability()
    {
        using var provider = new RecordingProvider(new Column("Status", DbType.String, int.MaxValue));
        var requested = new Column("Status", DbType.String, int.MaxValue, "pending") { IsNullable = false };
        provider.ChangeColumn("Example", requested);
        Assert.That(provider.Commands, Is.EqualTo(new[]
        {
            "ALTER TABLE Example MODIFY (Status DEFAULT 'pending')",
            "ALTER TABLE Example MODIFY (Status NOT NULL)"
        }));
        Assert.That(requested.DefaultValue, Is.EqualTo("pending"));
    }

    [Test]
    public void TypeConversionAndNullabilityUseSeparateStatements()
    {
        using var provider = new RecordingProvider(new Column("Status", DbType.String, 80));
        provider.ChangeColumn("Example", new Column("Status", DbType.String, int.MaxValue) { IsNullable = false });
        Assert.That(provider.Commands, Is.EqualTo(new[]
        {
            "ALTER TABLE Example MODIFY (Status DEFAULT NULL)",
            "ALTER TABLE Example MODIFY (Status NCLOB)",
            "ALTER TABLE Example MODIFY (Status NOT NULL)"
        }));
    }

    [Test]
    public void ScalarTypeAndDefaultChangesArePreserved()
    {
        using var provider = new RecordingProvider(new Column("Amount", DbType.Int32));
        provider.ChangeColumn("Example", new Column("Amount", DbType.Decimal)
        {
            Precision = 12, Scale = 4, DefaultValue = 29, IsNullable = false
        });
        Assert.That(provider.Commands, Is.EqualTo(new[]
        {
            "ALTER TABLE Example MODIFY (Amount NUMBER(12, 4) DEFAULT 29)",
            "ALTER TABLE Example MODIFY (Amount NOT NULL)"
        }));
    }

    [Test]
    public void QualifiedTableAndQuotedColumnArePreserved()
    {
        using var provider = new RecordingProvider(new Column("select", DbType.String, int.MaxValue));
        provider.ChangeColumn("audit.Example", new Column("select", DbType.String, int.MaxValue) { IsNullable = false });
        Assert.That(provider.Commands, Is.EqualTo(new[]
        {
            "ALTER TABLE audit.Example MODIFY (\"select\" DEFAULT NULL)",
            "ALTER TABLE audit.Example MODIFY (\"select\" NOT NULL)"
        }));
    }
}
