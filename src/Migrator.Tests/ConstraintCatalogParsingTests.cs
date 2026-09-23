using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;

namespace Migrator.Tests;

public class ConstraintCatalogParsingTests
{
    private MySqlTransformationProvider provider;
    private IDbCommand command;
    private DataTable rows;
    private DataTableReader reader;

    [SetUp]
    public void SetUp()
    {
        var connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open);
        command = Substitute.For<IDbCommand>();
        connection.CreateCommand().Returns(command);
        command.CreateParameter().Returns(_ => Substitute.For<IDbDataParameter>());
        command.Parameters.Returns(Substitute.For<IDataParameterCollection>());
        provider = Substitute.ForPartsOf<MySqlTransformationProvider>(new MysqlDialect(), connection, "default", null);
        rows = new DataTable();
        foreach (var name in new[] { "name", "type", "column", "ordinal", "expression" }) rows.Columns.Add(name, typeof(object));
        provider.Configure().GetForeignKeyConstraints(Arg.Any<string>()).Returns(_ =>
        {
            Assert.That(reader.IsClosed, Is.True, "Close the catalog reader before querying foreign keys.");
            return new[] { new ForeignKeyConstraint("FK_Parent", "Parent", new[] { "Id" }, "Items", new[] { "ParentId" }) };
        });
    }

    [TearDown]
    public void TearDown() { reader?.Dispose(); rows.Dispose(); provider.Dispose(); }

    private TableConstraint[] Read()
    {
        reader = rows.CreateDataReader();
        provider.Configure().ExecuteQuery(Arg.Any<IDbCommand>(), Arg.Any<string>()).Returns(reader);
        return ConstraintMetadataReader.Read(provider, "Items");
    }

    [TestCase("P")][TestCase("PK")][TestCase(" primary key ")][TestCase("PN")]
    [TestCase("U")][TestCase("UQ")][TestCase(" unique ")]
    public void CompositeKeysKeepCatalogOrderAndConstraintBoundaries(string type)
    {
        rows.Rows.Add("First", type, "Z", 1, DBNull.Value);
        rows.Rows.Add("First", type, "A", 2, DBNull.Value);
        rows.Rows.Add("Second", "U", "Other", 1, DBNull.Value);
        var result = Read();
        var first = result[0];
        var columns = first is PrimaryKeyConstraint pk ? pk.KeyColumns : ((UniqueConstraint)first).KeyColumns;
        Assert.That(columns, Is.EqualTo(new[] { "Z", "A" }));
        Assert.That(first.Name, Is.EqualTo("First"));
        if (type.Trim().StartsWith("P", StringComparison.OrdinalIgnoreCase))
            Assert.That(((PrimaryKeyConstraint)first).NonClustered, Is.EqualTo(type == "PN"));
        else Assert.That(first, Is.TypeOf<UniqueConstraint>());
        Assert.That(((UniqueConstraint)result[1]).KeyColumns, Is.EqualTo(new[] { "Other" }));
        Assert.That(result[2].Name, Is.EqualTo("FK_Parent"));
        Assert.That(result, Has.Length.EqualTo(3));
        command.Received(1).Dispose();
    }

    [TestCase("C", "CHECK (Amount > 0)", "Amount > 0")]
    [TestCase("K", "Amount > 0", "Amount > 0")]
    [TestCase(" check ", null, null)]
    public void ChecksPreserveExpressionsIncludingMissingCatalogText(string type, string expression, string expected)
    {
        rows.Rows.Add("Positive", type, DBNull.Value, 0, (object)expression ?? DBNull.Value);
        Assert.That(((CheckConstraint)Read()[0]).CheckConstraintString, Is.EqualTo(expected));
    }

    [Test]
    public void EmptyCatalogStillIncludesForeignKeys()
        => Assert.That(Read().Select(c => c.Name), Is.EqualTo(new[] { "FK_Parent" }));

    [Test]
    public void UnknownCatalogTypeFailsAndDisposesResources()
    {
        rows.Rows.Add("Unknown", "unexpected", "Id", 1, DBNull.Value);
        Assert.Throws<MigrationException>(() => Read());
        Assert.That(reader.IsClosed, Is.True);
        command.Received(1).Dispose();
        provider.DidNotReceive().GetForeignKeyConstraints(Arg.Any<string>());
    }
}
