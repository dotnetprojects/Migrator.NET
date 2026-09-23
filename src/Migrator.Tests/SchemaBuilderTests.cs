using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using NSubstitute;
using NUnit.Framework;
namespace Migrator.Tests;

public class SchemaBuilderTests
{
    [Test]
    public void TableExecutesOnceWithCompletedColumnAndConstraintDefinitions()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("Users").WithColumn("Id").AsInt32()
            .WithColumn("Name").AsString(100).WithDefaultValue("guest")
            .WithPrimaryKey("PK_Users", "Id");
        var provider = Substitute.For<ITransformationProvider, IForeignKeyActions>();
        builder.Apply(provider);
        provider.Received(1).AddTable("Users", Arg.Is<IDbField[]>(fields => fields.Length == 3
            && ((Column)fields[0]).Name == "Id" && ((Column)fields[0]).Type == DbType.Int32
            && ((Column)fields[1]).Size == 100 && (string)((Column)fields[1]).DefaultValue == "guest"
            && ((PrimaryKeyConstraint)fields[2]).KeyColumns.SequenceEqual(new[] { "Id" })));
        Assert.That(provider.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "AddColumn"), Is.False);
    }
    [Test]
    public void ForeignKeyExecutesAfterCompletedTableWithIndependentActions()
    {
        var builder = new MigrationBuilder();
        builder.Create.Table("Child").WithColumn("ParentId").AsInt32();
        builder.Create.ForeignKey("FK_Child").FromTable("Child").WithColumns("ParentId")
            .ToTable("Parent").WithColumns("Id")
            .OnDelete(ForeignKeyConstraintType.Cascade).OnUpdate(ForeignKeyConstraintType.Restrict);
        var provider = Substitute.For<ITransformationProvider, IForeignKeyActions>();
        builder.Apply(provider);
        Received.InOrder(() => {
            provider.AddTable("Child", Arg.Any<IDbField[]>());
            ((IForeignKeyActions)provider).AddForeignKey("FK_Child", "Child", Arg.Is<string[]>(c => c.SequenceEqual(new[] { "ParentId" })),
                "Parent", Arg.Is<string[]>(c => c.SequenceEqual(new[] { "Id" })), ForeignKeyConstraintType.Cascade, ForeignKeyConstraintType.Restrict);
        });
    }
    [Test]
    public void ExistingTableColumnRetainsAuthoredOptions()
    {
        var builder = new MigrationBuilder();
        builder.Create.Column("Name").OnTable("Existing").AsString(80).NotNullable().WithDefaultValue("guest");
        var provider = Substitute.For<ITransformationProvider, IForeignKeyActions>();
        builder.Apply(provider);
        provider.Received(1).AddColumn("Existing", Arg.Is<Column>(c => c.Name == "Name" && c.Size == 80
            && !c.IsNullable && (string)c.DefaultValue == "guest"));
        Assert.That(provider.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "AddTable"), Is.False);
    }
}
