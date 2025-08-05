using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.SchemaBuilder;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class SchemaBuilderTests
{
    private SchemaBuilder _schemaBuilder;

    [SetUp]
    public void Setup()
    {
        _schemaBuilder = new SchemaBuilder();
        _schemaBuilder.AddTable("SomeTable");
    }

    [Test]
    public void Can_AddTable()
    {
        _schemaBuilder.AddTable("MyUserTable");
        //Assert.That("MyUserTable", _schemaBuilder.Expressions.ElementAt(0));
    }

    [Test]
    public void Can_AddColumn()
    {
        var columnName = "MyUserId";

        _schemaBuilder
            .AddColumn(columnName);

        //Assert.IsTrue(_schemaBuilder.Columns.Count == 1);
        //Assert.That(columnName, _schemaBuilder.Columns[0].Name);
    }

    [Test]
    public void Can_chain_AddColumn_OfType()
    {
        _schemaBuilder
            .AddColumn("SomeColumn")
            .OfType(DbType.Int32);

        //Assert.That(DbType.Int32, _schemaBuilder.Columns[0].Type, "Column.Type was not as expected");
    }

    [Test]
    public void Can_chain_AddColumn_WithProperty()
    {
        _schemaBuilder
            .AddColumn("MyColumn")
            .OfType(DbType.Int32)
            .WithProperty(ColumnProperty.PrimaryKey);

        //Assert.IsTrue(_schemaBuilder.Columns[0].IsPrimaryKey);
    }

    [Test]
    public void Can_chain_AddColumn_WithSize()
    {
        _schemaBuilder
            .AddColumn("column")
            .WithSize(100);

        //Assert.That(100, _schemaBuilder.Columns[0].Size);
    }

    [Test]
    public void Can_chain_AddColumn_WithDefaultValue()
    {
        _schemaBuilder
            .AddColumn("something")
            .OfType(DbType.Int32)
            .WithDefaultValue("default value");

        //Assert.That("default value", _schemaBuilder.Columns[0].DefaultValue);
    }

    [Test]
    public void Can_chain_AddTable_WithForeignKey()
    {
        _schemaBuilder
            .AddColumn("MyColumnThatIsForeignKey")
            .AsForeignKey().ReferencedTo("PrimaryKeyTable", "PrimaryKeyColumn").WithConstraint(ForeignKeyConstraintType.NoAction);

        //Assert.IsTrue(_schemaBuilder.Columns[0].ColumnProperty == ColumnProperty.ForeignKey);
    }
}

