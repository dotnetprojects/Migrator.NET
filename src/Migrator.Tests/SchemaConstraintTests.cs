using System;
using DotNetProjects.Migrator;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;

namespace Migrator.Tests;

[Category("SQLite")]
public class SchemaConstraintTests
{
    [Test]
    public void NamedCompositeConstraintsPreserveOrderAndEnforceWholeKeys()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        var first = new Column("First", DbType.Int32);
        provider.AddTable("OrderedKeys", first, new Column("Second", DbType.Int32), new Column("Label", DbType.String),
            new PrimaryKeyConstraint("Primary key", "Second", "First"),
            new UniqueConstraint("Unique pair", "First", "Label"),
            new CheckConstraint("Check label", "length(Label) > 0 AND instr(Label, ',') = 0"));
        var constraints = provider.GetTableConstraints("OrderedKeys");
        Assert.That(constraints.OfType<PrimaryKeyConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Second", "First" }));
        Assert.That(constraints.OfType<UniqueConstraint>().Single().Name, Is.EqualTo("Unique pair"));
        Assert.That(constraints.OfType<CheckConstraint>().Single().CheckConstraintString, Does.Contain("instr(Label, ',')"));
        Assert.That(first.ColumnProperty, Is.EqualTo(ColumnProperty.None), "Creating a key must not mutate caller-owned columns.");
        provider.ExecuteNonQuery("INSERT INTO OrderedKeys VALUES (1, 2, 'a'), (1, 3, 'b')");
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO OrderedKeys VALUES (1, 2, 'c')"));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO OrderedKeys VALUES (1, 4, 'a')"));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO OrderedKeys VALUES (NULL, 4, 'x')"));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO OrderedKeys VALUES (9, 9, 'a,b')"));
    }

    [Test]
    public void NamedIdentityKeyAndQuotedNamesRoundTrip()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("IdentityKeys", new Column("Id", DbType.Int32, ColumnProperty.Identity),
            new PrimaryKeyConstraint("PK \"quoted\"", "Id"));
        provider.ExecuteNonQuery("INSERT INTO IdentityKeys DEFAULT VALUES");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM IdentityKeys")), Is.EqualTo(1));
        Assert.That(provider.GetTableConstraints("IdentityKeys").Single().Name, Is.EqualTo("PK \"quoted\""));
    }

    [Test]
    public void UnnamedLegacyConstraintsHaveNoInventedNamesAndUniqueIndexesStaySeparate()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.ExecuteNonQuery("CREATE TABLE \"Old'Table\" (Id INTEGER PRIMARY KEY, Value TEXT UNIQUE CHECK (length(Value) > 0))");
        provider.ExecuteNonQuery("CREATE UNIQUE INDEX ExtraIndex ON \"Old'Table\"(Value)");
        var constraints = provider.GetTableConstraints("Old'Table");
        Assert.That(constraints.Length, Is.EqualTo(3));
        Assert.That(constraints.All(c => c.Name == null), Is.True);
        Assert.That(constraints.OfType<UniqueConstraint>().Single().KeyColumns, Is.EqualTo(new[] { "Value" }));
    }

    [Test]
    public void FluentNamedDefinitionsAreCompleteBeforeExecution()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        var builder = new MigrationBuilder();
        builder.Create.Table("FluentKeys").WithColumn("Id").AsInt32()
            .WithPrimaryKey("PK_FluentKeys", "Id").WithUniqueConstraint("UQ_FluentKeys", "Id");
        Assert.That(builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)).Single(), Does.Contain("CONSTRAINT \"PK_FluentKeys\" PRIMARY KEY"));
        builder.Apply(provider);
        Assert.That(provider.GetTableConstraints("FluentKeys").Length, Is.EqualTo(2));
    }

    [Test]
    public void InvalidKeyDefinitionsFailBeforeCreatingTheTable()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        Assert.Throws<MigrationException>(() => provider.AddTable("InvalidKey", new Column("Id", DbType.Int32), new PrimaryKeyConstraint("PK_Invalid", "Missing")));
        Assert.That(provider.TableExists("InvalidKey"), Is.False);
    }
}
