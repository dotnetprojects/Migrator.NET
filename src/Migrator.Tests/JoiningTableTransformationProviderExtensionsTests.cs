using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Framework;
using NUnit.Framework;
using Rhino.Mocks;

namespace Migrator.Tests;

[TestFixture]
public class JoiningTableTransformationProviderExtensionsTests
{
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        _provider = MockRepository.GenerateStub<ITransformationProvider>();
    }

    #endregion

    private ITransformationProvider _provider;

    [Test]
    public void AddManyToManyJoiningTable_AddsPrimaryKey()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddPrimaryKey(null, null, null))[0];

        Assert.That("PK_TestScenarioVersions", Is.EqualTo(args[0]));
        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[1]));

        var columns = (string[])args[2];

        Assert.That("TestScenarioId", Does.Contain(columns));
        Assert.That("VersionId", Does.Contain(columns));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesLeftHandSideColumn_WithCorrectName()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddTable(null, (Column[])null))[0];

        var lhsColumn = ((IDbField[])args[1])[0] as Column;

        Assert.That(lhsColumn.Name, Is.EqualTo("TestScenarioId"));
        Assert.That(DbType.Guid, Is.EqualTo(lhsColumn.Type));
        Assert.That(ColumnProperty.NotNull, Is.EqualTo(lhsColumn.ColumnProperty));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesLeftHandSideForeignKey_WithCorrectAttributes()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[0];

        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[1]));
        Assert.That("TestScenarioId", Is.EqualTo(args[2]));
        Assert.That("dbo.TestScenarios", Is.EqualTo(args[3]));
        Assert.That("Id", Is.EqualTo(args[4]));
        Assert.That(ForeignKeyConstraintType.NoAction, Is.EqualTo(args[5]));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesLeftHandSideForeignKey_WithCorrectName()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[0];

        Assert.That("FK_Scenarios_ScenarioVersions", Is.EqualTo(args[0]));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesRightHandSideColumn_WithCorrectName()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddTable(null, (Column[])null))[0];

        var rhsColumn = ((IDbField[])args[1])[1] as Column;

        Assert.That(rhsColumn.Name, Is.EqualTo("VersionId"));
        Assert.That(DbType.Guid, Is.EqualTo(rhsColumn.Type));
        Assert.That(ColumnProperty.NotNull, Is.EqualTo(rhsColumn.ColumnProperty));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesRightHandSideForeignKey_WithCorrectAttributes()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[1];

        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[1]));
        Assert.That("VersionId", Is.EqualTo(args[2]));
        Assert.That("dbo.Versions", Is.EqualTo(args[3]));
        Assert.That("Id", Is.EqualTo(args[4]));
        Assert.That(ForeignKeyConstraintType.NoAction, Is.EqualTo(args[5]));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesRightHandSideForeignKey_WithCorrectName()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[1];

        Assert.That("FK_Versions_ScenarioVersions", Is.EqualTo(args[0]));
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesTableWithCorrectName()
    {
        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.AddTable(null, (Column[])null))[0];

        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[0]));
    }

    [Test]
    public void RemoveManyToManyJoiningTable_RemovesLhsForeignKey()
    {
        _provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.RemoveForeignKey(null, null))[0];

        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[0]));
        Assert.That("FK_Scenarios_ScenarioVersions", Is.EqualTo(args[1]));
    }

    [Test]
    public void RemoveManyToManyJoiningTable_RemovesRhsForeignKey()
    {
        _provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.RemoveForeignKey(null, null))[1];

        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[0]));
        Assert.That("FK_Versions_ScenarioVersions", Is.EqualTo(args[1]));
    }

    [Test]
    public void RemoveManyToManyJoiningTable_RemovesTable()
    {
        _provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");

        var args = _provider.GetArgumentsForCallsMadeOn(stub => stub.RemoveTable(null))[0];

        Assert.That("dbo.TestScenarioVersions", Is.EqualTo(args[0]));
    }
}