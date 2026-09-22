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
    [Test] public void TableIsOneCompleteOperationAndDoesNotMutateInput()
    {
        var column = new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey);
        var builder = new MigrationBuilder(); builder.Create.Table("Example").WithFields(column);
        column.Name = "Changed";
        var operation = (CreateTableOperation)builder.Build().Single();
        Assert.That(((Column)operation.Fields.Single()).Name, Is.EqualTo("Id"));
        var provider = Substitute.For<ITransformationProvider>();
        builder.Apply(provider);
        provider.Received(1).AddTable("Example", (string)null, Arg.Is<IDbField[]>(x => x.Length == 1));
        provider.DidNotReceiveWithAnyArgs().AddColumn(default, default(Column));
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
