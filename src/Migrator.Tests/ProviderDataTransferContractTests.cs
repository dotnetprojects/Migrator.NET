using System;
using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework.Models;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;

namespace Migrator.Tests;

// These exercise provider validation and generated SQL without a server. Live suites
// remain responsible for executing each dialect against its database engine.
[TestFixture("SqlServer")]
[TestFixture("Oracle")]
[TestFixture("PostgreSQL")]
[TestFixture("SQLite")]
public class ProviderDataTransferContractTests(string database)
{
    private TransformationProvider provider;

    [SetUp]
    public void SetUp()
    {
        var connection = Substitute.For<IDbConnection>();
        provider = database switch
        {
            "SqlServer" => Substitute.ForPartsOf<SqlServerTransformationProvider>(new SqlServerDialect(), connection, null, "default", null),
            "Oracle" => Substitute.ForPartsOf<OracleTransformationProvider>(new OracleDialect(), connection, null, "default", null),
            "PostgreSQL" => Substitute.ForPartsOf<PostgreSQLTransformationProvider>(new PostgreSQLDialect(), connection, null, "default", null),
            _ => Substitute.ForPartsOf<SQLiteTransformationProvider>(new SQLiteDialect(), connection, "default", null)
        };
        provider.Configure().TableExists(Arg.Any<string>()).Returns(c => (string)c[0] is "SourceTable" or "TargetTable");
        provider.Configure().ColumnExists(Arg.Any<string>(), Arg.Any<string>()).Returns(c => (string)c[1] is "Id" or "Value" or "Label" or "Other");
        provider.Configure().ExecuteNonQuery(Arg.Any<string>()).Returns(1);
    }

    [TearDown]
    public void TearDown() => provider.Dispose();

    [TestCase("source-table", "Source")]
    [TestCase("target-table", "Target")]
    [TestCase("source-column", "source")]
    [TestCase("target-column", "target")]
    [TestCase("order-column", "source")]
    [TestCase("order-not-copied", "orderBySourceColumns")]
    public void CopyRejectsInvalidMetadataBeforeWriting(string invalid, string message)
    {
        var source = invalid == "source-table" ? "Missing" : "SourceTable";
        var target = invalid == "target-table" ? "Missing" : "TargetTable";
        var sourceColumns = new List<string> { invalid == "source-column" ? "Missing" : "Value" };
        var targetColumns = new List<string> { invalid == "target-column" ? "Missing" : "Label" };
        var order = invalid == "order-column" ? new List<string> { "Missing" }
            : invalid == "order-not-copied" ? new List<string> { "Other" } : null;
        var error = Assert.Catch(() => provider.CopyDataFromTableToTable(source, sourceColumns, target, targetColumns, order));
        Assert.That(error.Message, Does.Contain(message));
        provider.DidNotReceiveWithAnyArgs().ExecuteNonQuery(default(string));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CopyKeepsColumnMappingAndOptionalSortOrder(bool ordered)
    {
        provider.CopyDataFromTableToTable("SourceTable", new List<string> { "Id", "Value" }, "TargetTable", new List<string> { "Id", "Label" }, ordered ? new List<string> { "Id" } : null);
        var expected = database switch
        {
            "SqlServer" => "INSERT INTO [TargetTable] ([Id], [Label]) SELECT [Id], [Value] FROM [SourceTable]" + (ordered ? " ORDER BY [Id]" : ""),
            "Oracle" => "INSERT INTO TargetTable (Id, \"Label\") SELECT Id, \"Value\" FROM SourceTable" + (ordered ? " ORDER BY Id" : ""),
            _ => "INSERT INTO TargetTable (Id, Label) SELECT Id, Value FROM SourceTable" + (ordered ? " ORDER BY Id" : "")
        };
        provider.Received(1).ExecuteNonQuery(expected);
    }

    [TestCase("source-table", "tableSourceNotQuoted")]
    [TestCase("target-table", "tableTargetNotQuoted")]
    [TestCase("empty-set", "fromSourceToTargetColumnPairs")]
    [TestCase("blank-set-source", "fromSourceToTargetColumnPairs")]
    [TestCase("blank-set-target", "fromSourceToTargetColumnPairs")]
    [TestCase("empty-match", "conditionColumnPairs")]
    [TestCase("blank-match-source", "conditionColumnPairs")]
    [TestCase("blank-match-target", "conditionColumnPairs")]
    public void UpdateRejectsMissingPredicatesAndMappingsBeforeWriting(string invalid, string message)
    {
        var set = new[] { new ColumnPair { ColumnNameSource = "Value", ColumnNameTarget = "Label" } };
        var match = new[] { new ColumnPair { ColumnNameSource = "Id", ColumnNameTarget = "Id" } };
        if (invalid == "empty-set") set = Array.Empty<ColumnPair>();
        if (invalid == "empty-match") match = Array.Empty<ColumnPair>();
        if (invalid == "blank-set-source") set[0].ColumnNameSource = " ";
        if (invalid == "blank-set-target") set[0].ColumnNameTarget = null;
        if (invalid == "blank-match-source") match[0].ColumnNameSource = " ";
        if (invalid == "blank-match-target") match[0].ColumnNameTarget = null;
        var error = Assert.Catch(() => provider.UpdateTargetFromSource(invalid == "source-table" ? "Missing" : "SourceTable",
            invalid == "target-table" ? "Missing" : "TargetTable", set, match));
        Assert.That(error.Message, Does.Contain(message));
        provider.DidNotReceiveWithAnyArgs().ExecuteNonQuery(default(string));
    }
}
