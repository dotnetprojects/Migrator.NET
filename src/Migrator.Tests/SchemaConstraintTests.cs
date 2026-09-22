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
        Assert.That(first.IsNullable, Is.True, "Creating a key must not mutate caller-owned columns.");
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
        provider.AddTable("IdentityKeys", new Column("Id",DbType.Int32){IsIdentity = true},
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
    public void RebuildPreservesNamedKeyOrderAndColumnOrder()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("RebuiltKeys", new Column("First", DbType.Int32), new Column("Second", DbType.Int32),
            new Column("Label", DbType.String, 20), new PrimaryKeyConstraint("PK ordered", "Second", "First"));
        provider.ExecuteNonQuery("INSERT INTO RebuiltKeys VALUES (1, 2, 'kept')");
        provider.ChangeColumn("RebuiltKeys", new Column("First", DbType.Int64));
        var key = provider.GetTableConstraints("RebuiltKeys").OfType<PrimaryKeyConstraint>().Single();
        Assert.That(key.Name, Is.EqualTo("PK ordered"));
        Assert.That(key.KeyColumns, Is.EqualTo(new[] { "Second", "First" }));
        Assert.That(((DotNetProjects.Migrator.Providers.Impl.SQLite.SQLiteTransformationProvider)provider).GetPragmaTableInfoItems("RebuiltKeys").OrderBy(c => c.Cid).Select(c => c.Name), Is.EqualTo(new[] { "First", "Second", "Label" }));
        Assert.That(provider.ExecuteScalar("SELECT Label FROM RebuiltKeys WHERE First=1 AND Second=2"), Is.EqualTo("kept"));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO RebuiltKeys VALUES (1, 2, 'duplicate')"));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO RebuiltKeys VALUES (NULL, 3, 'null')"));
        Assert.Throws<MigrationException>(() => provider.RemoveColumn("RebuiltKeys", "First"));
        Assert.That(provider.ColumnExists("RebuiltKeys", "First"), Is.True);
        provider.RemovePrimaryKey("RebuiltKeys");
        Assert.That(provider.GetTableConstraints("RebuiltKeys").OfType<PrimaryKeyConstraint>(), Is.Empty);
        provider.ExecuteNonQuery("INSERT INTO RebuiltKeys VALUES (1, 2, 'allowed')");
    }

    [Test]
    public void RebuildPreservesNamedIdentityAndSequenceHighWater()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("RebuiltIdentity", new Column("Id", DbType.Int32) { IsIdentity = true },
            new Column("Value", DbType.String, 20), new PrimaryKeyConstraint("PK identity", "Id"));
        provider.ExecuteNonQuery("INSERT INTO RebuiltIdentity VALUES (40, 'removed')");
        provider.ExecuteNonQuery("DELETE FROM RebuiltIdentity");
        provider.ChangeColumn("RebuiltIdentity", new Column("Value", DbType.String, 40));
        provider.ExecuteNonQuery("INSERT INTO RebuiltIdentity (Value) VALUES ('next')");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM RebuiltIdentity")), Is.EqualTo(41));
        Assert.That(provider.GetTableConstraints("RebuiltIdentity").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("PK identity"));
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
    public void OfflineIdentityPreviewExecutesTheSameSchemaAsImperativeCreation()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("PreviewIdentity").WithColumn("Id").AsInt32().Identity()
            .WithColumn("Value").AsString(20).WithCollation("NOCASE")
            .WithPrimaryKey("PK_PreviewIdentity", "Id")
            .WithUniqueConstraint("UQ_Value", "Value");
        var sql = builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)).Single();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.ExecuteNonQuery(sql);
        provider.ExecuteNonQuery("INSERT INTO PreviewIdentity (Value) VALUES ('Hello')");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT Id FROM PreviewIdentity")), Is.EqualTo(1));
        Assert.Catch(() => provider.ExecuteNonQuery("INSERT INTO PreviewIdentity (Value) VALUES ('HELLO')"));
        Assert.That(provider.GetTableConstraints("PreviewIdentity").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("PK_PreviewIdentity"));
    }

    [Test]
    public void RawDefaultsWorkInBothApisAndSurviveMetadataAndRebuild()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("RawImperative",
            new Column("Id", DbType.Int32),
            new Column("Token", DbType.String, 40) { DefaultValue = RawSql.Insert("lower(hex(randomblob(8)))") },
            new Column("Literal", DbType.String, 40) { DefaultValue = "lower(hex(randomblob(8)))" });
        var builder = new MigrationBuilder();
        builder.Create.Table("RawFluent").WithColumn("Id").AsInt32()
            .WithColumn("Token").AsString(40).WithDefaultValue(RawSql.Insert("lower(hex(randomblob(8)))"))
            .WithColumn("Literal").AsString(40).WithDefaultValue("lower(hex(randomblob(8)))");
        builder.Apply(provider);
        foreach (var table in new[] { "RawImperative", "RawFluent" })
        {
            var defaultExpression = provider.ReadLegacyColumns(table).Single(c => c.Name == "Token").DefaultValue;
            Assert.That(defaultExpression, Is.TypeOf<RawSql>());
            provider.ChangeColumn(table, new Column("Id", DbType.Int64));
            provider.ExecuteNonQuery("INSERT INTO " + table + " (Id) VALUES (1)");
            Assert.That(provider.ExecuteScalar("SELECT length(Token) FROM " + table), Is.EqualTo(16));
            Assert.That(provider.ExecuteScalar("SELECT Literal FROM " + table), Is.EqualTo("lower(hex(randomblob(8)))"));
        }
        var preview = new MigrationBuilder();
        preview.Create.Table("RawPreview").WithColumn("Token").AsString(40)
            .WithDefaultValue(RawSql.Insert("lower(hex(randomblob(8)))"));
        provider.ExecuteNonQuery(preview.Preview(new SqlGenerationContext(ProviderTypes.SQLite)).Single());
        provider.ExecuteNonQuery("INSERT INTO RawPreview DEFAULT VALUES");
        Assert.That(provider.ExecuteScalar("SELECT length(Token) FROM RawPreview"), Is.EqualTo(16));
    }

    [Test]
    public void SemanticCollationDoesNotSilentlyDowngradeUnicodeToAscii()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        Assert.Throws<NotSupportedException>(() => provider.AddTable("UnicodeNames",
            new Column("Name", DbType.String, 40) { Collation = Collation.CaseInsensitive }));
        Assert.That(provider.TableExists("UnicodeNames"), Is.False);
        var builder = new MigrationBuilder();
        builder.Create.Table("AsciiNames").WithColumn("Name").AsString(40).WithCollation(Collation.AsciiIgnoreCase)
            .WithUniqueConstraint("UQ_Ascii", "Name");
        provider.ExecuteNonQuery(builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)).Single());
        provider.Insert("AsciiNames", ["Name"], ["hello"]);
        Assert.Catch(() => provider.Insert("AsciiNames", ["Name"], ["HELLO"]));
        provider.Insert("AsciiNames", ["Name"], ["é"]);
        provider.Insert("AsciiNames", ["Name"], ["É"]);
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(*) FROM AsciiNames")), Is.EqualTo(3));
        provider.ChangeColumn("AsciiNames", new Column("Name", DbType.String, 80) { Collation = Collation.AsciiIgnoreCase });
        Assert.Catch(() => provider.Insert("AsciiNames", ["Name"], ["HELLO"]));
        Assert.That(Convert.ToInt32(provider.ExecuteScalar("SELECT COUNT(*) FROM AsciiNames")), Is.EqualTo(3));
    }

    [Test]
    public void MetadataDoesNotConfuseConcatenatedExpressionsWithStringLiterals()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.AddTable("ConcatDefault", new Column("Id", DbType.Int32),
            new Column("Value", DbType.String, 30) { DefaultValue = RawSql.Insert("'A' || 'B'") });
        Assert.That(provider.ReadLegacyColumns("ConcatDefault").Single(c => c.Name == "Value").DefaultValue, Is.TypeOf<RawSql>());
        provider.ChangeColumn("ConcatDefault", new Column("Id", DbType.Int64));
        provider.ExecuteNonQuery("INSERT INTO ConcatDefault (Id) VALUES (1)");
        Assert.That(provider.ExecuteScalar("SELECT Value FROM ConcatDefault"), Is.EqualTo("AB"));
    }

    [Test]
    public void ConstraintTokenizerRecognizesCommentsAdjacentToKeywords()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        provider.ExecuteNonQuery("CREATE TABLE CommentedKey (Id INTEGER NOT NULL, Label TEXT, CONSTRAINT/*name*/pk PRIMARY/*kind*/KEY(Id))");
        Assert.That(provider.GetTableConstraints("CommentedKey").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("pk"));
        provider.ChangeColumn("CommentedKey", new Column("Label", DbType.String, 40));
        Assert.That(provider.GetTableConstraints("CommentedKey").OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("pk"));
        provider.Insert("CommentedKey", ["Id"], [1]);
        Assert.Catch(() => provider.Insert("CommentedKey", ["Id"], [1]));
    }

    [Test]
    public void InvalidKeyDefinitionsFailBeforeCreatingTheTable()
    {
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null);
        Assert.Throws<MigrationException>(() => provider.AddTable("InvalidKey", new Column("Id", DbType.Int32), new PrimaryKeyConstraint("PK_Invalid", "Missing")));
        Assert.That(provider.TableExists("InvalidKey"), Is.False);
    }
}
