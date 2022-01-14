using System.Data;
using Migrator.Framework;
using NUnit.Framework;
using Rhino.Mocks;

namespace Migrator.TestsNeu
{
	[TestFixture]
	public class JoiningTableTransformationProviderExtensionsTests
	{
		#region Setup/Teardown

		[SetUp]
		public void SetUp()
		{
			provider = MockRepository.GenerateStub<ITransformationProvider>();
		}

		#endregion

		ITransformationProvider provider;

		[Test]
		public void AddManyToManyJoiningTable_AddsPrimaryKey()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddPrimaryKey(null, null, null))[0];

			Assert.AreEqual("PK_TestScenarioVersions", args[0]);
			Assert.AreEqual("dbo.TestScenarioVersions", args[1]);

			var columns = (string[]) args[2];

			Assert.Contains("TestScenarioId", columns);
			Assert.Contains("VersionId", columns);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesLeftHandSideColumn_WithCorrectName()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddTable(null, (Column[]) null))[0];

            Column lhsColumn = ((IDbField[]) args[1])[0] as Column;

			Assert.AreEqual(lhsColumn.Name, "TestScenarioId");
			Assert.AreEqual(DbType.Guid, lhsColumn.Type);
			Assert.AreEqual(ColumnProperty.NotNull, lhsColumn.ColumnProperty);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesLeftHandSideForeignKey_WithCorrectAttributes()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[0];

			Assert.AreEqual("dbo.TestScenarioVersions", args[1]);
			Assert.AreEqual("TestScenarioId", args[2]);
			Assert.AreEqual("dbo.TestScenarios", args[3]);
			Assert.AreEqual("Id", args[4]);
			Assert.AreEqual(ForeignKeyConstraintType.NoAction, args[5]);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesLeftHandSideForeignKey_WithCorrectName()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[0];

			Assert.AreEqual("FK_Scenarios_ScenarioVersions", args[0]);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesRightHandSideColumn_WithCorrectName()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddTable(null, (Column[]) null))[0];

			Column rhsColumn = ((IDbField[]) args[1])[1] as Column;

			Assert.AreEqual(rhsColumn.Name, "VersionId");
			Assert.AreEqual(DbType.Guid, rhsColumn.Type);
			Assert.AreEqual(ColumnProperty.NotNull, rhsColumn.ColumnProperty);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesRightHandSideForeignKey_WithCorrectAttributes()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[1];

			Assert.AreEqual("dbo.TestScenarioVersions", args[1]);
			Assert.AreEqual("VersionId", args[2]);
			Assert.AreEqual("dbo.Versions", args[3]);
			Assert.AreEqual("Id", args[4]);
			Assert.AreEqual(ForeignKeyConstraintType.NoAction, args[5]);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesRightHandSideForeignKey_WithCorrectName()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddForeignKey(null, null, "", null, null, ForeignKeyConstraintType.NoAction))[1];

			Assert.AreEqual("FK_Versions_ScenarioVersions", args[0]);
		}

		[Test]
		public void AddManyToManyJoiningTable_CreatesTableWithCorrectName()
		{
			provider.AddManyToManyJoiningTable("dbo", "TestScenarios", "Id", "Versions", "Id");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.AddTable(null, (Column[]) null))[0];

			Assert.AreEqual("dbo.TestScenarioVersions", args[0]);
		}

		[Test]
		public void RemoveManyToManyJoiningTable_RemovesLhsForeignKey()
		{
			provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.RemoveForeignKey(null, null))[0];

			Assert.AreEqual("dbo.TestScenarioVersions", args[0]);
			Assert.AreEqual("FK_Scenarios_ScenarioVersions", args[1]);
		}

		[Test]
		public void RemoveManyToManyJoiningTable_RemovesRhsForeignKey()
		{
			provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.RemoveForeignKey(null, null))[1];

			Assert.AreEqual("dbo.TestScenarioVersions", args[0]);
			Assert.AreEqual("FK_Versions_ScenarioVersions", args[1]);
		}

		[Test]
		public void RemoveManyToManyJoiningTable_RemovesTable()
		{
			provider.RemoveManyToManyJoiningTable("dbo", "TestScenarios", "Versions");

			object[] args = provider.GetArgumentsForCallsMadeOn(stub => stub.RemoveTable(null))[0];

			Assert.AreEqual("dbo.TestScenarioVersions", args[0]);
		}
	}
}
