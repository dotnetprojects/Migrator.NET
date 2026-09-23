using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests;

public class FluentAuthoringTests
{
    private static IEnumerable<TestCaseData> IncompleteExpressions()
    {
        yield return Case("column table", b => b.Create.Column("Email"));
        yield return Case("column type", b => b.Create.Column("Email").OnTable("Users"));
        yield return Case("table column type", b => b.Create.Table("Other").WithColumn("Email"));
        yield return Case("alter table", b => b.Alter.Column("Email"));
        yield return Case("delete table", b => b.Delete.Column("Email"));
        yield return Case("rename destination", b => b.Rename.Table("Users"));
        yield return Case("rename column destination", b => b.Rename.Column("Email").OnTable("Users"));
        yield return Case("key columns", b => b.Create.PrimaryKey("PK_Users").OnTable("Users"));
        yield return Case("check expression", b => b.Create.CheckConstraint("CK_Id").OnTable("Users"));
        yield return Case("foreign key parent", b => b.Create.ForeignKey("FK_User").FromTable("Users").WithColumns("ParentId"));
        yield return Case("index columns", b => b.Create.Index("IX_Email").OnTable("Users"));
        yield return Case("view fields", b => b.Create.View("Names").FromTable("Users"));
        yield return Case("insert values", b => b.Insert.IntoTable("Users"));
        yield return Case("update values", b => b.Update.Table("Users"));
        yield return Case("update predicate", b => b.Update.Table("Users").Set(new[] { "Email" }, new object[] { "new" }));
        yield return Case("delete predicate", b => b.Delete.FromTable("Users"));
        yield return Case("copy columns", b => b.Execute.CopyDataFromTable("Users").ToTable("Other"));
        yield return Case("update source", b => b.Execute.UpdateTable("Users"));
    }

    private static TestCaseData Case(string name, Action<MigrationBuilder> expression)
        => new TestCaseData(expression).SetName("Incomplete expression rejects all execution: " + name);

    [TestCaseSource(nameof(IncompleteExpressions))]
    public void IncompleteExpressionsRejectBuildPreviewAndApplyBeforeAnyProviderCall(Action<MigrationBuilder> expression)
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("Users").WithColumn("Id").AsInt32();
        expression(builder);
        var provider = Substitute.For<ITransformationProvider>();
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Throws<InvalidOperationException>(() => builder.Preview(new SqlGenerationContext(ProviderTypes.SQLite)));
        Assert.Throws<InvalidOperationException>(() => builder.Apply(provider));
        Assert.That(provider.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public void ColumnHandlesKeepTheirOwnColumnAndBuildReturnsIndependentSnapshots()
    {
        var builder = new MigrationBuilder();
        var table = builder.Create.Table("Users");
        var first = table.WithColumn("Id").AsInt32();
        table.WithColumn("Name").AsString(80);
        first.NotNullable();
        var initial = (CreateTableOperation)builder.Build().Single();
        Assert.That(((Column)initial.Fields[0]).IsNullable, Is.False);
        Assert.That(((Column)initial.Fields[1]).IsNullable, Is.True);
        first.WithDefaultValue(42);
        ((Column)initial.Fields[1]).Name = "Corrupted";
        var latest = (CreateTableOperation)builder.Build().Single();
        Assert.That(((Column)initial.Fields[0]).DefaultValue, Is.Null);
        Assert.That(((Column)latest.Fields[0]).DefaultValue, Is.EqualTo(42));
        Assert.That(((Column)latest.Fields[1]).Name, Is.EqualTo("Name"));
    }

    [Test]
    public void SharedColumnTypesAndOptionsWorkForTableCreateAndAlter()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("Users").WithColumn("Amount").AsDecimal(18, 4).NotNullable().WithDefaultValue(0m);
        builder.Create.Column("Amount").OnTable("Other").AsDecimal(18, 4).NotNullable().WithDefaultValue(0m);
        builder.Alter.Column("Amount").OnTable("Third").AsDecimal(18, 4).NotNullable().WithDefaultValue(0m);
        builder.Create.Column("Created").OnTable("Users").AsDateTime();
        builder.Alter.Column("Modified").OnTable("Users").AsDateTime2();
        builder.Create.Column("Token").OnTable("Users").AsGuid();
        builder.Alter.Column("Active").OnTable("Users").AsBoolean();
        var operations = builder.Build();
        var columns = new[] { (Column)((CreateTableOperation)operations[0]).Fields[0], ((ColumnOperation)operations[1]).Column, ((ColumnOperation)operations[2]).Column };
        foreach (var column in columns)
        {
            Assert.That(column.Type, Is.EqualTo(DbType.Decimal));
            Assert.That(column.Precision, Is.EqualTo(18));
            Assert.That(column.Scale, Is.EqualTo(4));
            Assert.That(column.IsNullable, Is.False);
            Assert.That(column.DefaultValue, Is.EqualTo(0m));
        }
        Assert.That(((ColumnOperation)operations[2]).Alter, Is.True);
        Assert.That(operations.Skip(3).Cast<ColumnOperation>().Select(op => op.Column.Type),
            Is.EqualTo(new[] { DbType.DateTime, DbType.DateTime2, DbType.Guid, DbType.Boolean }));
    }

    [Test]
    public void DeferredExpressionsRetainStartOrderAndRejectDuplicateCompletion()
    {
        var builder = new MigrationBuilder();
        var rename = builder.Rename.Table("Users");
        builder.Create.Column("Email").OnTable("Members").AsString();
        rename.To("Members");
        Assert.That(builder.Build()[0], Is.EqualTo(new RenameOperation("Users", "Members")));
        Assert.Throws<InvalidOperationException>(() => rename.To("Other"));
        var reverse = builder.Build()[0].Reverse();
        Assert.That(reverse, Is.EqualTo(new RenameOperation("Members", "Users")));
    }

    [Test]
    public void NamedConstraintAndRemovalStepsDispatchToTheSelectedTable()
    {
        var builder = new MigrationBuilder();
        builder.Create.PrimaryKey("PK").OnTable("Users").WithColumns("Tenant", "Id");
        builder.Create.NonClusteredPrimaryKey("PK_NC").OnTable("Other").WithColumns("Id");
        builder.Create.UniqueConstraint("UQ").OnTable("Users").WithColumns("Email");
        builder.Create.CheckConstraint("CK").OnTable("Users").WithExpression("Id > 0");
        builder.Delete.Column("Email").FromTable("Users");
        builder.Delete.PrimaryKey().FromTable("Users");
        builder.Delete.DefaultValue("Id").FromTable("Users");
        builder.Delete.ForeignKey("FK").FromTable("Users");
        builder.Delete.Constraint("CK").FromTable("Users");
        builder.Delete.Index("IX").FromTable("Users");
        builder.Delete.AllIndexes().FromTable("Users");
        builder.Delete.AllConstraints().FromTable("Users");
        builder.Delete.ForeignKeysForColumn("ParentId").FromTable("Users");
        var provider = Substitute.For<ITransformationProvider>();
        builder.Apply(provider);
        provider.Received().AddPrimaryKey("PK", "Users", Arg.Is<string[]>(c => c.SequenceEqual(new[] { "Tenant", "Id" })));
        provider.Received().AddPrimaryKeyNonClustered("PK_NC", "Other", "Id");
        provider.Received().AddUniqueConstraint("UQ", "Users", "Email");
        provider.Received().AddCheckConstraint("CK", "Users", "Id > 0");
        provider.Received().RemoveColumn("Users", "Email");
        provider.Received().RemovePrimaryKey("Users");
        provider.Received().RemoveColumnDefaultValue("Users", "Id");
        provider.Received().RemoveForeignKey("Users", "FK");
        provider.Received().RemoveConstraint("Users", "CK");
        provider.Received().RemoveIndex("Users", "IX");
        provider.Received().RemoveAllIndexes("Users");
        provider.Received().RemoveAllConstraints("Users");
        provider.Received().RemoveAllForeignKeys("Users", "ParentId");
    }

    [Test]
    public void IndexAndTypedColumnDefinitionsSnapshotInputsAndBuildResults()
    {
        var keys = new[] { "Email" };
        var filter = new FilterItem { ColumnName = "Id", Value = 1 };
        var definition = new Column("Name", DbType.String, 80);
        var index = new Index { Name = "IX_Typed", KeyColumns = keys };
        var builder = new MigrationBuilder();
        builder.Create.Index("IX_Fluent").OnTable("Users").WithColumns(keys).Unique().Clustered().IncludeColumns("Id").WithFilter(filter);
        builder.Create.Index(index).OnTable("Other");
        builder.Alter.Column(definition).OnTable("Users");
        keys[0] = "Mutated"; filter.ColumnName = "Mutated"; definition.Name = "Mutated";
        var first = builder.Build();
        var fluentIndex = ((IndexOperation)first[0]).Index;
        Assert.That(fluentIndex.KeyColumns, Is.EqualTo(new[] { "Email" }));
        Assert.That(fluentIndex.Unique && fluentIndex.Clustered, Is.True);
        Assert.That(fluentIndex.IncludeColumns, Is.EqualTo(new[] { "Id" }));
        Assert.That(fluentIndex.FilterItems[0].ColumnName, Is.EqualTo("Id"));
        Assert.That(((IndexOperation)first[1]).Index.KeyColumns, Is.EqualTo(new[] { "Email" }));
        Assert.That(((ColumnOperation)first[2]).Column.Name, Is.EqualTo("Name"));
        fluentIndex.KeyColumns[0] = "Corrupted";
        Assert.That(((IndexOperation)builder.Build()[0]).Index.KeyColumns, Is.EqualTo(new[] { "Email" }));
    }

    [Test]
    public void CompositeForeignKeyRetainsColumnOrderActionsAndSnapshots()
    {
        var child = new[] { "Tenant", "UserId" };
        var parent = new[] { "Tenant", "Id" };
        var builder = new MigrationBuilder();
        builder.Create.ForeignKey("FK").FromTable("Orders").WithColumns(child)
            .ToTable("Users").WithColumns(parent).OnDelete(ForeignKeyConstraintType.Cascade).OnUpdate(ForeignKeyConstraintType.Restrict);
        child[0] = "Changed"; parent[0] = "Changed";
        var operation = (ConstraintOperation)builder.Build().Single();
        Assert.That(operation.Columns, Is.EqualTo(new[] { "Tenant", "UserId" }));
        Assert.That(operation.ParentColumns, Is.EqualTo(new[] { "Tenant", "Id" }));
        Assert.That(operation.OnDelete, Is.EqualTo(ForeignKeyConstraintType.Cascade));
        Assert.That(operation.OnUpdate, Is.EqualTo(ForeignKeyConstraintType.Restrict));
        Assert.That(operation.Reverse(), Is.EqualTo(new RemoveOperation(RemoveKind.ForeignKey, "Orders", "FK")));
        Assert.Throws<ArgumentException>(() => new MigrationBuilder().Create.ForeignKey("Invalid")
            .FromTable("Orders").WithColumns("Tenant", "UserId").ToTable("Users").WithColumns("Id"));
    }

    [Test]
    public void EmptyPredicatesAndMismatchedValuesAreRejected()
    {
        var builder = new MigrationBuilder();
        Assert.Throws<ArgumentException>(() => builder.Insert.IntoTable("Users").Row(new[] { "Id" }, Array.Empty<object>()));
        Assert.Throws<ArgumentException>(() => builder.Delete.FromTable("Users").Where(Array.Empty<string>(), Array.Empty<object>()));
        Assert.Throws<ArgumentException>(() => builder.Update.Table("Users").Set(new[] { "Id" }, new object[] { 1 }).WhereSql(" "));
        Assert.Throws<ArgumentException>(() => builder.Create.Column("Id").OnTable(" "));
        Assert.Throws<ArgumentException>(() => builder.Execute.CopyDataFromTable("Users").ToTable("Other")
            .WithColumns(new[] { "Id" }, new[] { "Id", "Name" }));
    }

    [Test, Category("SQLite")]
    public void TableReadsAndUnfilteredDataOperationsQuoteTableNames()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var builder = new MigrationBuilder();
        builder.Create.Table("User Records").WithColumn("Id").AsInt32();
        builder.Insert.IntoTable("User Records").Row(new[] { "Id" }, new object[] { 1 });
        builder.Insert.IntoTable("User Records").Row(new[] { "Id" }, new object[] { 2 });
        builder.Update.Table("User Records").Set(new[] { "Id" }, new object[] { 3 }).WhereSql("Id = 2");
        builder.Apply(provider);
        var table = new SchemaInspector(provider).Table("User Records");
        Assert.That(Convert.ToInt32(table.SelectScalar("Id", "Id = 3")), Is.EqualTo(3));
        var all = new MigrationBuilder();
        all.Update.Table("User Records").Set(new[] { "Id" }, new object[] { 4 }).AllRows();
        all.Apply(provider);
        Assert.That(Convert.ToInt32(table.SelectScalar("COUNT(*)", "Id = 4")), Is.EqualTo(2));
        var delete = new MigrationBuilder();
        delete.Delete.FromTable("User Records").AllRows();
        delete.Apply(provider);
        Assert.That(Convert.ToInt32(table.SelectScalar("COUNT(*)")), Is.Zero);
    }

    [Test]
    public void ViewFormsAndDataValuesSnapshotTheirInputs()
    {
        var field = new ViewField("Name");
        var element = new ViewColumn("u", "Name");
        var value = new byte[] { 1, 2 };
        var builder = new MigrationBuilder();
        builder.Create.View("Names").FromTable("Users").WithFields(field);
        builder.Create.View("NamesWithAlias").FromTable("Users").WithElements(element);
        builder.Insert.IntoTable("Users").Row(new[] { "Data" }, new object[] { value });
        value[0] = 9;
        var first = builder.Build();
        Assert.That(((ViewOperation)first[0]).Fields[0], Is.Not.SameAs(field));
        Assert.That(((ViewOperation)first[1]).Elements[0], Is.Not.SameAs(element));
        Assert.That(((DataOperation)first[2]).Values[0], Is.EqualTo(new byte[] { 1, 2 }));
        ((byte[])((DataOperation)first[2]).Values[0])[0] = 8;
        var second = builder.Build();
        Assert.That(((ViewOperation)second[0]).Fields[0], Is.Not.SameAs(((ViewOperation)first[0]).Fields[0]));
        Assert.That(((DataOperation)second[2]).Values[0], Is.EqualTo(new byte[] { 1, 2 }));
    }

    [Test, Category("SQLite")]
    public void ExplicitTablesRunThroughCreateAlterRenameIndexViewCopyAndAllRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        var builder = new MigrationBuilder();
        builder.Create.Table("Users").WithColumn("Id").AsInt32().WithColumn("Name").AsString();
        builder.Create.Column("Email").OnTable("Users").AsString(100);
        builder.Insert.IntoTable("Users").Row(new[] { "Id", "Name" }, new object[] { 1, "Ada" });
        builder.Alter.Column("Email").OnTable("Users").AsString(320);
        builder.Rename.Column("Name").OnTable("Users").To("DisplayName");
        builder.Rename.Table("Users").To("Members");
        builder.Create.Index("IX_Name").OnTable("Members").WithColumns("DisplayName").Unique();
        builder.Create.View("Names").FromTable("Members").WithFields(new ViewField("DisplayName"));
        builder.Create.Table("Archive").WithColumn("Name").AsString();
        builder.Execute.CopyDataFromTable("Members").ToTable("Archive").WithColumns(new[] { "DisplayName" }, new[] { "Name" }).OrderBy("DisplayName");
        builder.Update.Table("Members").Set(new[] { "Email" }, new object[] { "ada@example.org" }).AllRows();
        builder.Apply(provider);
        var schema = new SchemaInspector(provider);
        Assert.That(schema.Table("Members").SelectScalar("Email"), Is.EqualTo("ada@example.org"));
        Assert.That(schema.Table("Archive").SelectScalar("Name"), Is.EqualTo("Ada"));
        Assert.That(schema.Table("Names").SelectScalar("DisplayName"), Is.EqualTo("Ada"));
        Assert.That(schema.Table("Members").IndexExists("IX_Name"), Is.True);
        var delete = new MigrationBuilder();
        delete.Delete.FromTable("Members").AllRows();
        delete.Delete.Index("IX_Name").FromTable("Members");
        delete.Delete.Column("Email").FromTable("Members");
        delete.Apply(provider);
        Assert.That(Convert.ToInt32(schema.Table("Members").SelectScalar("COUNT(*)")), Is.Zero);
        Assert.That(schema.Table("Members").ColumnExists("Email"), Is.False);
    }
}
