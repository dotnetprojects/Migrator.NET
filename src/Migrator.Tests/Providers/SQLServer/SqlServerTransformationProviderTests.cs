using System.Data;
using System.Linq;
using System;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using Migrator.Tests.Providers.SQLServer.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SQLServer")]
public class SqlServerTransformationProviderTests : SQLServerTransformationProviderTestBase
{
    [Test]
    public void LegacyDialectKeepsTimeOfDayAndDurationRepresentationsSeparate()
    {
        using var legacy = DotNetProjects.Migrator.ProviderFactory.Create(ProviderTypes.SqlServer2005, Provider.ConnectionString, null);
        var time = new TimeOnly(12, 34, 56, 120);
        legacy.AddTable("LegacyClock", new Column("Id", DbType.Int32), new Column("Moment", DbType.Time, time));
        try
        {
            legacy.Insert("LegacyClock", ["Id"], [1]);
            legacy.Insert("LegacyClock", ["Id", "Moment"], [2, time]);
            foreach (var id in new[] { 1, 2 })
                Assert.That(Convert.ToDateTime(legacy.ExecuteScalar("SELECT Moment FROM LegacyClock WHERE Id=" + id)).TimeOfDay, Is.EqualTo(time.ToTimeSpan()));
            IntervalRegression.Verify(legacy, false);
        }
        finally
        {
            legacy.RemoveTable("LegacyClock");
            if (legacy.TableExists("DurationValues")) legacy.RemoveTable("DurationValues");
        }
    }

    [Test]
    public void NegativeMultiDayIntervalDefaultsAndParametersPersist() => IntervalRegression.Verify(Provider, false);

    [Test]
    public void TimeTypeDefaultAndValueRoundTripThroughMetadata()
    {
        var time = new TimeOnly(12, 34, 56, 789);
        Provider.AddTable("ClockValues", new Column("Moment",DbType.Time,time));
        var column = Provider.ReadLegacyColumns("ClockValues").Single();
        Assert.That(column.Type, Is.EqualTo(DbType.Time));
        Assert.That(column.DefaultValue, Is.EqualTo(time));
        Provider.AddTable("CopiedClock", column);
        Provider.ExecuteNonQuery("INSERT INTO CopiedClock DEFAULT VALUES");
        Assert.That(Provider.ExecuteScalar("SELECT Moment FROM CopiedClock"), Is.EqualTo(time.ToTimeSpan()));
        Provider.Insert("ClockValues", new[] { "Moment" }, new object[] { time });
        Assert.That(Provider.ExecuteScalar("SELECT Moment FROM ClockValues"), Is.EqualTo(time.ToTimeSpan()));
    }

    [Test]
    public void ExplicitScriptSplitsGoWithoutSplittingMultilineValues()
    {
        Provider.ExecuteSqlScript("CREATE TABLE ScriptBatches (Value nvarchar(100));\nGO\nINSERT INTO ScriptBatches VALUES ('before\nGO\nafter');\nGO -- final batch\nINSERT INTO ScriptBatches VALUES ('last');");
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM ScriptBatches")), Is.EqualTo(2));
        Assert.That(Provider.ExecuteScalar("SELECT Value FROM ScriptBatches WHERE Value LIKE 'before%'"), Does.Contain("GO"));
    }

    [Test]
    public void IndependentForeignKeyActionsCascadeUpdateAndSetNullOnDelete()
    {
        Provider.AddTable("ActionParent", new Column("Id",DbType.Int32){IsNullable = false},new PrimaryKeyConstraint("PK_" + "ActionParent", "Id"));
        Provider.AddTable("ActionChild", new Column("ParentId",DbType.Int32));
        ((IForeignKeyActions)Provider).AddForeignKey("ActionForeignKey", "ActionChild", new[] { "ParentId" },
            "ActionParent", new[] { "Id" }, ForeignKeyConstraintType.SetNull, ForeignKeyConstraintType.Cascade);
        Provider.ExecuteNonQuery("INSERT INTO ActionParent VALUES (1); INSERT INTO ActionChild VALUES (1); UPDATE ActionParent SET Id=2 WHERE Id=1");
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT ParentId FROM ActionChild")), Is.EqualTo(2));
        Provider.ExecuteNonQuery("DELETE FROM ActionParent WHERE Id=2");
        Assert.That(Provider.ExecuteNullableScalar<int>("SELECT ParentId FROM ActionChild"), Is.Null);
        Assert.That(Convert.ToInt32(Provider.ExecuteScalar("SELECT COUNT(*) FROM ActionChild")), Is.EqualTo(1));
    }

    [Test]
    public void ByteColumnWillBeCreatedAsBlob()
    {
        Provider.AddColumn("TestTwo", "BlobColumn", DbType.Byte);
        Assert.That(Provider.ColumnExists("TestTwo", "BlobColumn"), Is.True);
    }

    [Test]
    public void InstanceForProvider()
    {
        var localProv = Provider["sqlserver"];
        Assert.That(localProv is SqlServerTransformationProvider, Is.True);

        var localProv2 = Provider["foo"];
        Assert.That(localProv2 is NoOpTransformationProvider, Is.True);
    }

    [Test]
    public void QuoteCreatesProperFormat()
    {
        var dialect = new SqlServerDialect();

        Assert.That("[foo]", Is.EqualTo(dialect.Quote("foo")));
    }

    [Test]
    public void TableExistsShouldWorkWithBracketsAndSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("[dbo].[TestTwo]"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("dbo.TestTwo"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithTableNamesWithBracket()
    {
        Assert.That(Provider.TableExists("[TestTwo]"), Is.True);
    }
}
