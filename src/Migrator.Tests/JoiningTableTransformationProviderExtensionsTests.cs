using System.Data;
using DotNetProjects.Migrator.Framework;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture]
public class JoiningTableTransformationProviderExtensionsTests
{
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        _provider = Substitute.For<ITransformationProvider>();
    }

    #endregion

    private ITransformationProvider _provider;

    [Test]
    public void AddManyToManyJoiningTable_AddsPrimaryKey()
    {
        _provider
            .When(x => x.AddPrimaryKey(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>()))
            .Do(callInfo =>
            {
                var capturedName = callInfo[0] as string;
                var capturedTable = callInfo[1] as string;
                var columns = callInfo[2] as string[];
                Assert.That(capturedName, Is.EqualTo("PK_TestScenarioVersions"));
                Assert.That(capturedTable, Is.EqualTo("dbo.TestScenarioVersions"));
                Assert.That(columns, Does.Contain("TestScenarioId"));
                Assert.That(columns, Does.Contain("VersionId"));
            });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesLeftHandSideColumn_WithCorrectName()
    {
        _provider
           .When(x => x.AddTable(Arg.Any<string>(), Arg.Any<Column[]>()))
           .Do(callInfo =>
           {
               var lhsColumn = ((IDbField[])callInfo[1])[0] as Column;

               Assert.That(lhsColumn.Name, Is.EqualTo("TestScenarioId"));
               Assert.That(lhsColumn.Type, Is.EqualTo(DbType.Guid));
               Assert.That(ColumnProperty.NotNull, Is.EqualTo(lhsColumn.ColumnProperty));
           });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesLeftHandSideForeignKey_WithCorrectAttributes()
    {
        _provider
           .When(x => x.AddForeignKey(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
           .Do(callInfo =>
           {
               var lhsColumn = ((IDbField[])callInfo[1])[0] as Column;

               Assert.That(callInfo[1] as string, Is.EqualTo("dbo.TestScenarioVersions"));
               Assert.That(callInfo[2] as string, Is.EqualTo("TestScenarioId"));
               Assert.That(callInfo[3] as string, Is.EqualTo("dbo.TestScenarios"));
               Assert.That(callInfo[4] as string, Is.EqualTo("Id"));
               Assert.That((ForeignKeyConstraintType)callInfo[5], Is.EqualTo(ForeignKeyConstraintType.NoAction));
           });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesLeftHandSideForeignKey_WithCorrectName()
    {
        _provider
           .When(x => x.AddForeignKey(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
           .Do(callInfo =>
           {
               var lhsColumn = ((IDbField[])callInfo[1])[0] as Column;

               Assert.That(callInfo[0] as string, Is.EqualTo("FK_Scenarios_ScenarioVersions"));
           });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesRightHandSideColumn_WithCorrectName()
    {
        _provider
          .When(x => x.AddTable(Arg.Any<string>(), Arg.Any<Column[]>()))
          .Do(callInfo =>
          {
              var rhsColumn = ((IDbField[])callInfo[1])[0] as Column;

              Assert.That(rhsColumn.Name, Is.EqualTo("VersionId"));
              Assert.That(DbType.Guid, Is.EqualTo(rhsColumn.Type));
              Assert.That(ColumnProperty.NotNull, Is.EqualTo(rhsColumn.ColumnProperty));
          });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesRightHandSideForeignKey_WithCorrectAttributes()
    {
        _provider
          .When(x => x.AddTable(Arg.Any<string>(), Arg.Any<Column[]>()))
          .Do(callInfo =>
          {
              var rhsColumn = ((IDbField[])callInfo[1])[0] as Column;

              Assert.That(rhsColumn.Name, Is.EqualTo("VersionId"));
              Assert.That(DbType.Guid, Is.EqualTo(rhsColumn.Type));
              Assert.That(ColumnProperty.NotNull, Is.EqualTo(rhsColumn.ColumnProperty));

              Assert.That(callInfo[1] as string, Is.EqualTo("dbo.TestScenarioVersions"));
              Assert.That(callInfo[2] as string, Is.EqualTo("VersionId"));
              Assert.That(callInfo[3] as string, Is.EqualTo("dbo.Versions"));
              Assert.That(callInfo[4] as string, Is.EqualTo("Id"));
              Assert.That((ForeignKeyConstraintType)callInfo[5], Is.EqualTo(ForeignKeyConstraintType.NoAction));
          });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesRightHandSideForeignKey_WithCorrectName()
    {
        _provider
         .When(x => x.AddForeignKey(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
         .Do(callInfo =>
         {
             var lhsColumn = ((IDbField[])callInfo[1])[0] as Column;

             Assert.That(callInfo[0] as string, Is.EqualTo("FK_Scenarios_ScenarioVersions"));
         });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void AddManyToManyJoiningTable_CreatesTableWithCorrectName()
    {
        _provider
          .When(x => x.AddTable(Arg.Any<string>(), Arg.Any<Column[]>()))
          .Do(callInfo =>
          {
              var rhsColumn = ((IDbField[])callInfo[1])[0] as Column;

              Assert.That(callInfo[1] as string, Is.EqualTo("dbo.TestScenarioVersions"));
          });

        _provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");
    }

    [Test]
    public void RemoveManyToManyJoiningTable_RemovesLhsForeignKey()
    {
        var callCount = 0;

        _provider
        .When(x => x.RemoveForeignKey(Arg.Any<string>(), Arg.Any<string>()))
        .Do(callInfo =>
        {
            callCount++;
            if (callCount == 1)
            {
                Assert.That(callInfo[0] as string, Is.EqualTo("dbo.TestScenarioVersions"));
                Assert.That(callInfo[1] as string, Is.EqualTo("FK_Scenarios_ScenarioVersions"));
            }
        });

        _provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");
    }

    [Test]
    public void RemoveManyToManyJoiningTable_RemovesRhsForeignKey()
    {
        var callCount = 0;

        _provider
        .When(x => x.RemoveForeignKey(Arg.Any<string>(), Arg.Any<string>()))
        .Do(callInfo =>
        {
            callCount++;
            if (callCount == 2)
            {
                Assert.That(callInfo[0] as string, Is.EqualTo("dbo.TestScenarioVersions"));
                Assert.That(callInfo[1] as string, Is.EqualTo("FK_Versions_ScenarioVersions"));
            }
        });

        _provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");
    }

    [Test]
    public void RemoveManyToManyJoiningTable_RemovesTable()
    {
        _provider
        .When(x => x.RemoveTable(Arg.Any<string>()))
        .Do(callInfo =>
        {
            Assert.That(callInfo[0] as string, Is.EqualTo("dbo.TestScenarioVersions"));
        });

        _provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");
    }
}
