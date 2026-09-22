using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
namespace Migrator.Tests;
public class FluentOperationsTests
{
    [Test] public void FileAndEmbeddedScriptsUseScriptOperations()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(path, "SELECT 1;\nGO\nSELECT 2;");
            var builder = new MigrationBuilder();
            builder.Execute.Script(path);
            builder.Execute.EmbeddedScript(typeof(ScriptTests).Assembly, "Migrator.Tests.ScriptResource.sql");
            Assert.That(builder.Build().All(x => x is ScriptOperation), Is.True);
            var script = builder.Build().First().ToSql(new SqlGenerationContext(ProviderTypes.SqlServer));
            Assert.That(script, Does.Contain("GO"));
            Assert.That(script.TrimEnd(), Does.EndWith("SELECT 2;"));
        }
        finally { System.IO.File.Delete(path); }
    }
    [Test] public void TableIsOneCompleteOperationAndDoesNotMutateInput()
    {
        var column = new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey);
        var builder = new MigrationBuilder(); builder.Create.Table("Example").WithFields(column);
        column.Name = "Changed";
        var operation = (CreateTableOperation)builder.Build().Single();
        Assert.That(((Column)operation.Fields.Single()).Name, Is.EqualTo("Id"));
        var provider = Substitute.For<ITransformationProvider>();
        builder.Apply(provider);
        provider.Received(1).AddTable("Example", Arg.Is<IDbField[]>(x => x.Length == 1));
        provider.DidNotReceiveWithAnyArgs().AddColumn(default, default(Column));
    }
    [Test] public void InvalidDataModifiersFailBeforeExecution()
    {
        var b = new MigrationBuilder();
        Assert.Throws<InvalidOperationException>(() => b.Update.Table("Example").IfNotExists(new[] { "Id" }, new object[] { 1 }));
        Assert.Throws<InvalidOperationException>(() => b.Delete.FromTable("Example").IfNotExists(new[] { "Id" }, new object[] { 1 }));
        Assert.Throws<NotSupportedException>(() => b.Delete.FromTable("Example").WhereSql("Id = 1"));
        Assert.Throws<InvalidOperationException>(() => b.Delete.FromTable("Example").Row(new[] { "Id" }, new object[] { 1 }));
        Assert.Throws<InvalidOperationException>(() => b.Delete.FromTable("Example").Set(new[] { "Id" }, new object[] { 1 }));
        var p = Substitute.For<ITransformationProvider>();
        Assert.Throws<InvalidOperationException>(() => new DataOperation(DataKind.Delete, "Example", new[] { "Id" }, new object[] { 1 }).Apply(p));
        Assert.Throws<NotSupportedException>(() => new DataOperation(DataKind.Delete, "Example", null, null, WhereSql: "Id=1").Apply(p));
        p.DidNotReceiveWithAnyArgs().Delete(default, default(string[]), default(object[]));
    }
    [TestCase(ProviderTypes.PostgreSQL)]
    [TestCase(ProviderTypes.PostgreSQL82)]
    public void PostgreSqlPreviewUsesProviderIdentifierAndBooleanSemantics(ProviderTypes provider)
    {
        var c = new SqlGenerationContext(provider);
        Assert.That(c.Table("Example"), Is.EqualTo("Example"));
        Assert.That(c.Quote("Id"), Is.EqualTo("Id"));
        Assert.That(c.Literal(true), Is.EqualTo("TRUE"));
    }
    [TestCase(ProviderTypes.Oracle)]
    [TestCase(ProviderTypes.MsOracle)]
    public void OracleGuidPreviewUsesRawConversion(ProviderTypes provider)
    {
        var c = new SqlGenerationContext(provider);
        Assert.That(c.Literal(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")), Is.EqualTo("HEXTORAW('00112233445566778899AABBCCDDEEFF')"));
    }
    [Test] public void InactiveProviderBranchCanBeAutomaticallyReversed()
    {
        var p = Substitute.For<ITransformationProvider>();
        p.IsThisProvider("oracle").Returns(false);
        var operation = new ConditionalOperation("oracle", new CallbackOperation("irreversible", _ => throw new Exception()));
        Assert.DoesNotThrow(() => operation.ValidateReverse(p));
        Assert.DoesNotThrow(() => operation.Reverse().Apply(p));
        p.IsThisProvider("oracle").Returns(true);
        Assert.Throws<IrreversibleMigrationException>(() => operation.ValidateReverse(p));
    }
    [Test] public void PreviewRejectsStructuredDependenciesAfterRawSql()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("Example").WithColumn("Id").AsInt32();
        builder.Execute.Sql("DROP TABLE Example");
        builder.Insert.IntoTable("Example").Row(new[] { "Id" }, new object[] { 1 });
        Assert.Throws<NotSupportedException>(() => builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
    }
    [Test] public void CopyOperationsSnapshotMutableDefinitions()
    {
        var pairs = new[] { new DotNetProjects.Migrator.Framework.Models.ColumnPair { ColumnNameSource = "Old", ColumnNameTarget = "New" } };
        var builder = new MigrationBuilder(); builder.Execute.UpdateFrom("Source", "Target", pairs, pairs);
        pairs[0].ColumnNameSource = "Mutated";
        var operation = (UpdateFromOperation)builder.Build().Single();
        Assert.That(operation.Copy[0].ColumnNameSource, Is.EqualTo("Old"));
        Assert.That(operation.Match[0], Is.Not.SameAs(pairs[0]));
    }
    [Test, Category("SQLite")] public void FluentDataChangesAndSchemaReadsPersistExpectedRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var builder = new MigrationBuilder();
        builder.Create.Table("ValuesTable").WithColumn("Id").AsInt32().WithColumn("Name").AsString();
        builder.Insert.IntoTable("ValuesTable").Row(new[] { "Id", "Name" }, new object[] { 1, "first" });
        builder.Insert.IntoTable("ValuesTable").Row(new[] { "Id", "Name" }, new object[] { 1, "duplicate" }).IfNotExists(new[] { "Id" }, new object[] { 1 });
        builder.Insert.IntoTable("ValuesTable").Row(new[] { "Id", "Name" }, new object[] { 2, "remove" });
        builder.Update.Table("ValuesTable").Set(new[] { "Name" }, new object[] { "updated" }).Where(new[] { "Id" }, new object[] { 1 });
        builder.Delete.FromTable("ValuesTable").Where(new[] { "Id" }, new object[] { 2 });
        builder.Apply(provider);
        var schema = new SchemaInspector(provider);
        Assert.That(schema.Table("ValuesTable").ColumnExists("Name"), Is.True);
        schema.Select("ValuesTable", new[] { "Name" }, reader =>
        {
            Assert.That(reader.Read(), Is.True); Assert.That(reader.GetString(0), Is.EqualTo("updated")); Assert.That(reader.Read(), Is.False);
        });
    }
    [Test] public void OfflinePreviewTracksCreatedThenRenamedTable()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("First").WithColumn("Id").AsInt32();
        builder.Rename.Table("First", "Second");
        builder.Create.Column("Name", "Second").AsString();
        var sql = builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite));
        Assert.That(sql.Count, Is.EqualTo(3));
        Assert.That(sql[2], Does.Contain("Second"));
        var unknown = new MigrationBuilder(); unknown.Create.Column("Id", "Missing");
        Assert.Throws<MigrationException>(() => unknown.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
    }
    [Test] public void CallbacksCannotBePreviewedOrAutomaticallyReversed()
    {
        var called = false; var builder = new MigrationBuilder(); builder.Execute.WithProvider(_ => called = true);
        Assert.Throws<NotSupportedException>(() => builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
        Assert.Throws<IrreversibleMigrationException>(() => builder.Build().Single().Reverse());
        Assert.That(called, Is.False);
    }
    [Test, Category("SQLite")] public void FluentAndPreviewProduceEquivalentDataAndAutomaticDownRemovesTable()
    {
        var builder = new MigrationBuilder(); builder.Create.Table("Example").WithColumn("Id").AsInt32().PrimaryKey().WithColumn("Name").AsString();
        builder.Insert.IntoTable("Example").Row(new[] { "Id", "Name" }, new object[] { 1, "O'Brien" });
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        builder.Apply(provider);
        Assert.That(provider.ExecuteScalar("SELECT Name FROM Example"), Is.EqualTo("O'Brien"));
        provider.RemoveTable("Example");
        foreach (var sql in builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite))) provider.ExecuteNonQuery(sql);
        Assert.That(provider.ExecuteScalar("SELECT Name FROM Example"), Is.EqualTo("O'Brien"));
        builder.Build()[0].Reverse().Apply(provider);
        Assert.That(provider.TableExists("Example"), Is.False);
    }
    [Test, Category("SQLite")] public void LegacyBuilderRetainsForeignKeyAction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"); connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.AddTable("Parent", new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey));
        var builder = new DotNetProjects.Migrator.Framework.SchemaBuilder.SchemaBuilder();
        builder.AddTable("Child").AddColumn("ParentId").OfType(DbType.Int32).AsForeignKey().ReferencedTo("Parent", "Id").WithConstraint(ForeignKeyConstraintType.Cascade);
        provider.ExecuteSchemaBuilder(builder);
        provider.ExecuteNonQuery("INSERT INTO Parent VALUES (1); INSERT INTO Child VALUES (1); DELETE FROM Parent WHERE Id=1");
        Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT COUNT(*) FROM Child")), Is.Zero);
    }
    [Test, Category("SQLite")] public void LegacyBuilderCreatesCompleteTable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var builder = new DotNetProjects.Migrator.Framework.SchemaBuilder.SchemaBuilder();
        builder.AddTable("Example").AddColumn("Id").OfType(DbType.Int32);
        provider.ExecuteSchemaBuilder(builder);
        Assert.That(provider.ColumnExists("Example", "Id"), Is.True);
    }
}
