using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.PostgreSQL.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.PostgreSQL;

public class PostgreSQLTransformationProvider_MetadataScopeTests : PostgreSQLTransformationProviderTestBase
{
    [Test]
    public void QualifiedMetadataDoesNotMixSameNamedTablesOrConstraints()
    {
        Provider.ExecuteNonQuery("CREATE SCHEMA metadata_a; CREATE SCHEMA metadata_b");
        Provider.ExecuteNonQuery("CREATE TABLE metadata_a.sample (id integer CONSTRAINT same_name UNIQUE); CREATE TABLE metadata_b.sample (value text)");
        Provider.ExecuteNonQuery("CREATE VIEW metadata_b.sample_view AS SELECT value FROM metadata_b.sample");
        Assert.That(Provider.TableExists("metadata_a.sample"), Is.True);
        Assert.That(Provider.TableExists("metadata_b.missing"), Is.False);
        Assert.That(Provider.ViewExists("metadata_b.sample_view"), Is.True);
        Assert.That(Provider.TableExists("metadata_b.sample_view"), Is.False);
        Assert.That(Provider.ColumnExists("metadata_a.sample", "id"), Is.True);
        Assert.That(Provider.ColumnExists("metadata_b.sample", "id"), Is.False);
        Assert.That(Provider.ConstraintExists("metadata_a.sample", "same_name"), Is.True);
        Assert.That(Provider.ConstraintExists("metadata_b.sample", "same_name"), Is.False);
        Assert.That(Provider.GetTableConstraints("metadata_a.sample").OfType<DotNetProjects.Migrator.Framework.UniqueConstraint>().Any(), Is.True);
        Assert.That(Provider.GetColumns("metadata_b.sample").Single().MigratorDbType, Is.EqualTo(MigratorDbType.String));
        Provider.ExecuteNonQuery("SET LOCAL search_path TO metadata_b");
        Assert.That(Provider.GetColumns("sample").Single().Name, Is.EqualTo("value"));
    }

    [Test]
    public void QuotedCatalogNamesRemainExactAndAreParameterized()
    {
        Provider.ExecuteNonQuery("CREATE TABLE \"Meta'Table\" (id integer CONSTRAINT \"Key'Name\" UNIQUE)");
        Assert.That(Provider.TableExists("\"Meta'Table\""), Is.True);
        Assert.That(Provider.ConstraintExists("\"Meta'Table\"", "Key'Name"), Is.True);
        Assert.That(Provider.GetTableConstraints("\"Meta'Table\"").OfType<DotNetProjects.Migrator.Framework.UniqueConstraint>().Any(), Is.True);
    }

    [Test]
    public void NativeTimeRoundTripsThroughMetadataDefaultsAndParameters()
    {
        var value = new TimeSpan(0, 12, 34, 56, 789);
        Provider.AddTable("NativeTimeRoundTrip", new Column("Value", DbType.Time, value));
        var column = Provider.GetColumns("NativeTimeRoundTrip").Single();
        Assert.That(column.MigratorDbType, Is.EqualTo(MigratorDbType.Time));
        Assert.That(column.DefaultValue, Is.EqualTo(value));
        Provider.Insert("NativeTimeRoundTrip", new[] { "Value" }, new object[] { value });
        Assert.That(Provider.ExecuteScalar("SELECT * FROM NativeTimeRoundTrip"), Is.EqualTo(value));
    }
}
