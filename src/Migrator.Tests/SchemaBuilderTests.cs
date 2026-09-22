using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.SchemaBuilder;
using NSubstitute;
using NUnit.Framework;
namespace Migrator.Tests;

[TestFixture]
public class SchemaBuilderTests
{
    [Test]
    public void TableExecutesOnceWithCompletedColumnDefinitions()
    {
        var builder = new SchemaBuilder();
        builder.AddTable("Users").AddColumn("Id").OfType(DbType.Int32).WithProperty(ColumnProperty.PrimaryKey);
        builder.AddColumn("Name").OfType(DbType.String).WithSize(100).WithDefaultValue("guest");
        var provider = Substitute.For<ITransformationProvider>();
        foreach (var expression in builder.Expressions) expression.Create(provider);
        provider.Received(1).AddTable("Users", Arg.Is<IDbField[]>(fields => fields.Length == 2
            && ((Column)fields[0]).Name == "Id" && ((Column)fields[0]).Type == DbType.Int32
            && ((Column)fields[0]).IsPrimaryKey && ((Column)fields[1]).Name == "Name"
            && ((Column)fields[1]).Type == DbType.String && ((Column)fields[1]).Size == 100
            && (string)((Column)fields[1]).DefaultValue == "guest"));
        Assert.That(provider.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "AddTable"), Is.EqualTo(1));
        Assert.That(provider.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "AddColumn"), Is.False);
    }

    [Test]
    public void ForeignKeyExecutesAfterCompletedChildTableWithCorrectDirectionAndAction()
    {
        var builder = new SchemaBuilder();
        builder.AddTable("Child").AddColumn("ParentId").OfType(DbType.Int32)
            .AsForeignKey().ReferencedTo("Parent", "Id").WithConstraint(ForeignKeyConstraintType.Cascade);
        var provider = Substitute.For<ITransformationProvider>();
        foreach (var expression in builder.Expressions) expression.Create(provider);
        Received.InOrder(() =>
        {
            provider.AddTable("Child", Arg.Is<IDbField[]>(fields => fields.Length == 1 && ((Column)fields[0]).Name == "ParentId"));
            provider.AddForeignKey("FK_Child_ParentId_Parent_Id", "Child", Arg.Is<string[]>(names => names.SequenceEqual(new[] { "ParentId" })),
                "Parent", Arg.Is<string[]>(names => names.SequenceEqual(new[] { "Id" })), ForeignKeyConstraintType.Cascade);
        });
    }

    [Test]
    public void ExistingTableColumnUsesAddColumnWithAuthoredOptions()
    {
        var builder = new SchemaBuilder();
        builder.WithTable("Existing").AddColumn("Name").OfType(DbType.String).WithSize(80).WithDefaultValue("guest");
        var provider = Substitute.For<ITransformationProvider>();
        foreach (var expression in builder.Expressions) expression.Create(provider);
        provider.Received(1).AddColumn("Existing", "Name", DbType.String, 80, ColumnProperty.None, "guest");
        Assert.That(provider.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "AddTable"), Is.False);
    }
}
