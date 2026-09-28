using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using Microsoft.Data.Sqlite;
using NSubstitute;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests;

public class FilteredIndexTests
{
    private static Column[] Columns() => [new("IpaUserIdentifier", DbType.String, 80), new("Archive", DbType.Int32), new("select", DbType.String, 80)];
    private static Index Definition() => new()
    {
        Name = "UX_ActiveUsers", Unique = true, KeyColumns = ["IpaUserIdentifier"],
        FilterItems = [
            new() { ColumnName = "IpaUserIdentifier", Filter = FilterType.NotEqualTo, Value = null },
            new() { ColumnName = "Archive", Filter = FilterType.EqualTo, Value = 0 }]
    };

    private sealed class SqlServerProvider(IDbConnection connection, Dialect dialect)
        : SqlServerTransformationProvider(dialect, connection, "dbo", "default", null)
    {
        public override Column[] GetColumns(string table) => Columns();
        public override bool TableExists(string table) => true;
        public override bool IndexExists(string table, string name) => false;
    }

    private sealed class PostgresProvider(IDbConnection connection)
        : PostgreSQLTransformationProvider(new PostgreSQLDialect(), connection, "public", "default", null)
    {
        public override Column[] GetColumns(string table) => Columns();
        public override bool TableExists(string table) => true;
        public override bool IndexExists(string table, string name) => false;
    }

    private sealed class OracleProvider(IDbConnection connection)
        : OracleTransformationProvider(new OracleDialect(), connection, null, "default", null)
    {
        public override Column[] GetColumns(string table) => Columns();
        public override bool TableExists(string table) => true;
        public override bool IndexExists(string table, string name) => false;
    }

    private static IDbConnection Connection(out IDbCommand command)
    {
        var connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open);
        command = Substitute.For<IDbCommand>();
        command.CreateParameter().Returns(_ => Substitute.For<IDbDataParameter>());
        command.Parameters.Returns(Substitute.For<IDataParameterCollection>());
        connection.CreateCommand().Returns(command);
        return connection;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeProvidersGenerateIssue199WithIncludedColumns(bool postgres)
    {
        var connection = Connection(out var command);
        using TransformationProvider provider = postgres ? new PostgresProvider(connection) : new SqlServerProvider(connection, new SqlServerDialect());
        var index = Definition();
        index.IncludeColumns = ["select"];
        index.UnsupportedFilterBehavior = UnsupportedIndexFilterBehavior.Ignore;
        var sql = provider.AddIndex("Users", index);
        Assert.That(sql, Does.Contain("IS NOT NULL").And.Contain(" = 0").And.Contain("WHERE").And.Contain("INCLUDE"));
        Assert.That(sql.IndexOf("INCLUDE", StringComparison.Ordinal), Is.LessThan(sql.IndexOf("WHERE", StringComparison.Ordinal)));
        Assert.That(sql, Does.Contain(postgres ? "\"select\"" : "[select]"));
        command.Received(1).ExecuteNonQuery();
        Assert.That(index.KeyColumns, Is.EqualTo(new[] { "IpaUserIdentifier" }));
        Assert.That(index.FilterItems.Count, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidFiltersStillFailInIgnoreMode(bool missingColumn)
    {
        using var provider = new SqlServerProvider(Connection(out var command), new SqlServerDialect());
        var index = Definition();
        index.UnsupportedFilterBehavior = UnsupportedIndexFilterBehavior.Ignore;
        if (missingColumn) index.FilterItems[0].ColumnName = "Missing";
        else index.FilterItems[0].Filter = FilterType.GreaterThan;
        if (missingColumn) Assert.Throws<MigrationException>(() => provider.AddIndex("Users", index));
        else Assert.Throws<ArgumentException>(() => provider.AddIndex("Users", index));
        command.DidNotReceive().ExecuteNonQuery();
    }

    [TestCase(ProviderTypes.Mysql)]
    [TestCase(ProviderTypes.MariaDB)]
    [TestCase(ProviderTypes.Firebird)]
    [TestCase(ProviderTypes.IBM_DB2)]
    [TestCase(ProviderTypes.IBM_Informix)]
    [TestCase(ProviderTypes.Sybase)]
    [TestCase(ProviderTypes.Hana)]
    [TestCase(ProviderTypes.SqlServer2005)]
    [TestCase(ProviderTypes.Oracle)]
    public void UnsupportedFiltersThrowByDefaultAndCanBeIgnoredThroughFluent(ProviderTypes type)
    {
        var connection = Connection(out var command);
        using var provider = type switch
        {
            ProviderTypes.SqlServer2005 => new SqlServerProvider(connection, new SqlServer2005Dialect()),
            ProviderTypes.Oracle => new OracleProvider(connection),
            _ => ProviderFactory.Create(type, connection, null)
        };
        var index = Definition();
        Assert.Throws<NotSupportedException>(() => provider.AddIndex("Users", index));
        command.DidNotReceive().ExecuteNonQuery();
        var builder = new MigrationBuilder();
        builder.Create.Index(index.Name).OnTable("Users").WithColumns(index.KeyColumns).Unique()
            .WithFilter(index.FilterItems.ToArray()).OnUnsupportedFilter(UnsupportedIndexFilterBehavior.Ignore);
        builder.Apply(provider);
        command.Received(1).ExecuteNonQuery();
        Assert.That(command.CommandText, Does.Contain("UNIQUE").And.Not.Contain("WHERE"));
        Assert.That(index.FilterItems.Count, Is.EqualTo(2));
        command.ClearReceivedCalls();
        index.UnsupportedFilterBehavior = UnsupportedIndexFilterBehavior.Ignore;
        index.IncludeColumns = ["select"];
        if (type == ProviderTypes.SqlServer2005) return; // INCLUDE is supported by this dialect.
        Assert.Throws<NotSupportedException>(() => provider.AddIndex("Users", index));
        command.DidNotReceive().ExecuteNonQuery();
    }

    [Test]
    public void FluentDefinitionAndBuildSnapshotsPreserveThePolicyAndNullFilters()
    {
        var definition = Definition();
        definition.UnsupportedFilterBehavior = UnsupportedIndexFilterBehavior.Ignore;
        var builder = new MigrationBuilder();
        builder.Create.Index(definition).OnTable("Users");
        definition.UnsupportedFilterBehavior = UnsupportedIndexFilterBehavior.Throw;
        definition.FilterItems.Clear();
        var built = (IndexOperation)builder.Build().Single();
        Assert.That(built.Index.UnsupportedFilterBehavior, Is.EqualTo(UnsupportedIndexFilterBehavior.Ignore));
        Assert.That(built.Index.FilterItems.Count, Is.EqualTo(2));
        var provider = Substitute.For<ITransformationProvider>();
        built.Apply(provider);
        provider.Received().AddIndex("Users", Arg.Is<Index>(i => i.UnsupportedFilterBehavior == UnsupportedIndexFilterBehavior.Ignore && i.FilterItems[0].Value == null));
        Assert.That(((RemoveOperation)built.Reverse()).Name, Is.EqualTo(definition.Name));
    }

    [Test]
    public void OracleEmulationDoesNotMutateCallerKeysOrFilters()
    {
        using var provider = new OracleProvider(Connection(out var command));
        var index = Definition();
        index.Unique = false;
        index.KeyColumns = ["IpaUserIdentifier", "Archive"];
        var sql = provider.AddIndex("Users", index);
        Assert.That(sql, Does.Contain("CASE WHEN").And.Contain("IS NOT NULL"));
        Assert.That(index.KeyColumns, Is.EqualTo(new[] { "IpaUserIdentifier", "Archive" }));
        Assert.That(index.FilterItems.Count, Is.EqualTo(2));
        command.Received(1).ExecuteNonQuery();
    }

    [Test]
    public void FluentCanReuseCatalogDefinitionsWithNullIncludedColumns()
    {
        var index = Definition();
        index.IncludeColumns = null;
        var builder = new MigrationBuilder();
        builder.Create.Index(index).OnTable("Users");
        var snapshot = ((IndexOperation)builder.Build().Single()).Index;
        Assert.That(snapshot.IncludeColumns, Is.Empty);
        Assert.That(snapshot.FilterItems.Count, Is.EqualTo(2));
    }

    [TestCase("([IpaUserIdentifier] IS NOT NULL AND [Archive]=(0))")]
    [TestCase("((ipauseridentifier IS NOT NULL) AND (archive = 0))")]
    [TestCase("IpaUserIdentifier IS NOT NULL AND Archive = 0")]
    public void ReadsNativeCatalogNullAndNonKeyPredicates(string sql)
    {
        var filters = IndexFilterSql.Parse(sql, Columns());
        Assert.That(filters.Select(f => f.ColumnName), Is.EqualTo(new[] { "IpaUserIdentifier", "Archive" }));
        Assert.That(filters.Select(f => f.Filter), Is.EqualTo(new[] { FilterType.NotEqualTo, FilterType.EqualTo }));
        Assert.That(filters.Select(f => f.Value), Is.EqualTo(new object[] { null, 0 }));
    }

    [TestCase("([select]=N'O''Brien AND (friends)' AND [Archive]=(0))")]
    [TestCase("(((\"select\")::text = 'O''Brien AND (friends)'::text) AND (archive = 0))")]
    [TestCase("\"select\" = 'O''Brien AND (friends)' AND Archive = 0")]
    public void ReadsEscapedStringsWithoutSplittingTheirContents(string sql)
    {
        var filters = IndexFilterSql.Parse(sql, Columns());
        Assert.That(filters.Count, Is.EqualTo(2));
        Assert.That(filters[0].ColumnName, Is.EqualTo("select"));
        Assert.That(filters[0].Value, Is.EqualTo("O'Brien AND (friends)"));
    }

    [TestCase(FilterType.EqualTo, "IS NULL")]
    [TestCase(FilterType.NotEqualTo, "IS NOT NULL")]
    public void NullAndDbNullHaveTheSameSql(FilterType type, string expected)
    {
        foreach (var value in new[] { null, DBNull.Value })
            Assert.That(IndexFilterSql.Format(new SqlServerDialect(), new FilterItem { ColumnName = "select", Filter = type, Value = value }, true), Is.EqualTo("[select] " + expected));
    }

    [Test]
    public void SqlServerGetIndexesReturnsAllDefinitionFieldsFromCatalog()
    {
        using var data = new DataTable();
        foreach (var name in new[] { "SchemaName", "TableName", "IndexName", "IndexType", "ColumnName", "FilterDefinition" }) data.Columns.Add(name);
        data.Columns.Add("ColumnOrder", typeof(int));
        foreach (var name in new[] { "IsUnique", "IsPrimaryKey", "IsUniqueConstraint", "IsDescending", "IsIncludedColumn", "IsFilteredIndex" }) data.Columns.Add(name, typeof(bool));
        foreach (var (name, order, included) in new[] { ("Archive", 2, false), ("select", 3, true), ("IpaUserIdentifier", 1, false) })
        {
            var row = data.NewRow();
            row["SchemaName"] = "audit"; row["TableName"] = "Users"; row["IndexName"] = "UX_ActiveUsers";
            row["IndexType"] = "NONCLUSTERED"; row["ColumnName"] = name; row["ColumnOrder"] = order;
            row["FilterDefinition"] = "([IpaUserIdentifier] IS NOT NULL AND [Archive]=(0))";
            row["IsUnique"] = true; row["IsPrimaryKey"] = false; row["IsUniqueConstraint"] = false;
            row["IsDescending"] = false; row["IsIncludedColumn"] = included; row["IsFilteredIndex"] = true;
            data.Rows.Add(row);
        }
        using var provider = new SqlServerProvider(Connection(out var command), new SqlServerDialect());
        command.ExecuteReader().Returns(_ => data.CreateDataReader());
        AssertCatalogIndex(provider.GetIndexes("audit.Users").Single());
        Assert.That(command.CommandText, Does.Contain("'audit'"));
    }

    [Test]
    public void PostgresGetIndexesReturnsAllDefinitionFieldsFromCatalog()
    {
        using var data = new DataTable();
        foreach (var name in new[] { "schema_name", "table_name", "index_name", "index_definition", "index_columns", "include_columns", "partial_filter" }) data.Columns.Add(name);
        foreach (var name in new[] { "is_unique", "is_clustered", "is_unique_constraint", "is_primary_constraint" }) data.Columns.Add(name, typeof(bool));
        var row = data.NewRow();
        row["schema_name"] = "audit"; row["table_name"] = "Users"; row["index_name"] = "UX_ActiveUsers";
        row["index_definition"] = "CREATE UNIQUE INDEX ...";
        row["index_columns"] = "IpaUserIdentifier, Archive"; row["include_columns"] = "select";
        row["partial_filter"] = "((ipauseridentifier IS NOT NULL) AND (archive = 0))";
        row["is_unique"] = true; row["is_clustered"] = false; row["is_unique_constraint"] = false; row["is_primary_constraint"] = false;
        data.Rows.Add(row);
        using var provider = new PostgresProvider(Connection(out var command));
        command.ExecuteReader().Returns(_ => data.CreateDataReader());
        AssertCatalogIndex(provider.GetIndexes("audit.Users").Single());
        Assert.That(command.CommandText, Does.Contain("to_regclass(@relation)"));
        command.Parameters.Received().Add(Arg.Is<IDbDataParameter>(p => p.ParameterName == "relation" && p.Value.ToString().Contains("audit")));
    }

    private static void AssertCatalogIndex(Index index)
    {
        Assert.That(index.Name, Is.EqualTo("UX_ActiveUsers"));
        Assert.That(index.KeyColumns, Is.EqualTo(new[] { "IpaUserIdentifier", "Archive" }));
        Assert.That(index.IncludeColumns, Is.EqualTo(new[] { "select" }));
        Assert.That(index.Unique, Is.True);
        Assert.That(index.Clustered || index.PrimaryKey || index.UniqueConstraint, Is.False);
        Assert.That(index.FilterItems.Select(f => f.ColumnName), Is.EqualTo(new[] { "IpaUserIdentifier", "Archive" }));
        Assert.That(index.FilterItems.Select(f => f.Filter), Is.EqualTo(new[] { FilterType.NotEqualTo, FilterType.EqualTo }));
        Assert.That(index.FilterItems.Select(f => f.Value), Is.EqualTo(new object[] { null, 0 }));
    }

    [TestCase("Archive = 0 OR IpaUserIdentifier IS NULL")]
    [TestCase("\"select\" = 'x'::text || 'y'")]
    public void UnrepresentablePredicatesFailRatherThanReturnIncompleteMetadata(string sql)
        => Assert.Throws<NotSupportedException>(() => IndexFilterSql.Parse(sql, Columns()));

    [TestCase(false)]
    [TestCase(true)]
    [Category("SQLite")]
    public void SQLiteReadsBackAndRebuildsTheCompleteIndex(bool fluent)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        VerifyRoundTrip(provider, fluent, includeColumns: false, rebuild: true);
    }

    [Test, Category("SQLite")]
    public void SQLiteReadsEachIndexWithEscapedStringsAndQuotedFilterColumns()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
        provider.AddTable("FilteredUsers", Columns());
        var first = Definition();
        first.Name = "IX_Filter_Longer";
        first.FilterItems = [new() { ColumnName = "select", Filter = FilterType.EqualTo, Value = "O'Brien AND (friends)" }];
        provider.AddIndex("FilteredUsers", first);
        var second = Definition();
        second.Name = "IX_Filter";
        second.FilterItems = [new() { ColumnName = "select", Filter = FilterType.EqualTo, Value = null }];
        provider.AddIndex("FilteredUsers", second);
        var indexes = provider.GetIndexes("FilteredUsers");
        Assert.That(indexes.Single(i => i.Name == first.Name).FilterItems.Single().Value, Is.EqualTo("O'Brien AND (friends)"));
        var nullFilter = indexes.Single(i => i.Name == second.Name).FilterItems.Single();
        Assert.That(nullFilter.ColumnName, Is.EqualTo("select"));
        Assert.That(nullFilter.Filter, Is.EqualTo(FilterType.EqualTo));
        Assert.That(nullFilter.Value, Is.Null);
    }

    internal static void VerifyRoundTrip(ITransformationProvider provider, bool fluent, bool includeColumns, bool rebuild = false)
    {
        provider.AddTable("FilteredUsers", Columns());
        var definition = Definition();
        if (includeColumns) definition.IncludeColumns = ["select"];
        if (fluent)
        {
            var builder = new MigrationBuilder();
            var options = builder.Create.Index(definition.Name).OnTable("FilteredUsers").WithColumns(definition.KeyColumns).Unique()
                .WithFilter(definition.FilterItems.ToArray()).OnUnsupportedFilter(UnsupportedIndexFilterBehavior.Throw);
            if (includeColumns) options.IncludeColumns(definition.IncludeColumns);
            builder.Apply(provider);
        }
        else provider.AddIndex("FilteredUsers", definition);
        if (rebuild) provider.ChangeColumn("FilteredUsers", new Column("select", DbType.String, 120));
        var actual = provider.GetIndexes("FilteredUsers").Single(i => i.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
        Assert.That(actual.Name, Is.EqualTo(definition.Name).IgnoreCase);
        Assert.That(actual.KeyColumns, Is.EqualTo(definition.KeyColumns).IgnoreCase);
        Assert.That(actual.IncludeColumns ?? [], Is.EqualTo(definition.IncludeColumns).IgnoreCase);
        Assert.That(actual.Unique, Is.True);
        Assert.That(actual.Clustered, Is.False);
        Assert.That(actual.PrimaryKey, Is.False);
        Assert.That(actual.UniqueConstraint, Is.False);
        Assert.That(actual.FilterItems.Select(f => f.ColumnName), Is.EquivalentTo(definition.FilterItems.Select(f => f.ColumnName)).IgnoreCase);
        foreach (var filter in definition.FilterItems)
        {
            var read = actual.FilterItems.Single(f => f.ColumnName.Equals(filter.ColumnName, StringComparison.OrdinalIgnoreCase));
            Assert.That(read.Filter, Is.EqualTo(filter.Filter));
            Assert.That(read.Value, Is.EqualTo(filter.Value));
        }
        // Recreate using metadata to prove it is usable, then verify filtered uniqueness.
        provider.RemoveIndex("FilteredUsers", actual.Name);
        if (fluent)
        {
            var builder = new MigrationBuilder();
            builder.Create.Index(actual).OnTable("FilteredUsers");
            builder.Apply(provider);
        }
        else provider.AddIndex("FilteredUsers", actual);
        provider.Insert("FilteredUsers", ["IpaUserIdentifier", "Archive"], [null, 0]);
        provider.Insert("FilteredUsers", ["IpaUserIdentifier", "Archive"], [null, 0]);
        provider.Insert("FilteredUsers", ["IpaUserIdentifier", "Archive"], ["same", 1]);
        provider.Insert("FilteredUsers", ["IpaUserIdentifier", "Archive"], ["same", 1]);
        provider.Insert("FilteredUsers", ["IpaUserIdentifier", "Archive"], ["same", 0]);
        Assert.Catch<DbException>(() => provider.Insert("FilteredUsers", ["IpaUserIdentifier", "Archive"], ["same", 0]));
    }
}
