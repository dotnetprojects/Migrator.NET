using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;
using NUnit.Framework;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace Migrator.Tests.Providers.Live;

[TestFixture("SQLite", ProviderTypes.SQLite, Category = "SQLite")]
[TestFixture("SQLServer", ProviderTypes.SqlServer, Category = "SQLServer")]
[TestFixture("PostgreSQL", ProviderTypes.PostgreSQL, Category = "PostgreSQL")]
[NonParallelizable]
public class NamespaceFilteredIndexTests(string database, ProviderTypes providerType) : LiveProviderFixture(database, providerType)
{
    [Test]
    public void SameNamedFilteredIndexesKeepTheirOwnPredicatesAndColumns()
    {
        var provider = (TransformationProvider)Provider;
        if (database == "SQLite") Provider.Rollback();
        var schemas = new[] { "filters_" + Guid.NewGuid().ToString("N"), "filters_" + Guid.NewGuid().ToString("N") };
        string Table(int i) => Provider.Dialect.QuoteIdentifier(schemas[i]) + ".items";
        try
        {
            for (var i = 0; i < schemas.Length; i++)
            {
                Provider.ExecuteNonQuery((database == "SQLite" ? "ATTACH ':memory:' AS " : "CREATE SCHEMA ") + Provider.Dialect.QuoteIdentifier(schemas[i]));
                Provider.AddTable(Table(i), new Column("id", DbType.Int32) { IsNullable = false }, new Column("payload", DbType.String, 20));
                Provider.AddIndex(Table(i), new Index { Name = "ix_same", KeyColumns = ["payload"],
                    FilterItems = [new() { ColumnName = "id", Filter = FilterType.EqualTo, Value = i + 1 }] });
            }
            provider.SetDefaultSchema(Provider.Dialect.QuoteIdentifier(schemas[1]));
            void Verify(string table, int value)
            {
                var index = Provider.GetIndexes(table).Single(i => i.Name == "ix_same");
                Assert.That(index.KeyColumns, Is.EqualTo(new[] { "payload" }));
                var filter = index.FilterItems.Single();
                Assert.That(filter.ColumnName, Is.EqualTo("id"));
                Assert.That(filter.Filter, Is.EqualTo(FilterType.EqualTo));
                Assert.That(Convert.ToInt32(filter.Value), Is.EqualTo(value));
            }
            Verify(Table(0), 1);
            Verify("items", 2);
            // SQLite reconstructs the attached table and must preserve its predicate.
            if (database == "SQLite") Provider.ChangeColumn(Table(0), new Column("payload", DbType.String, 40));
            Verify(Table(0), 1);
            Verify("items", 2);
            Provider.RemoveIndex(Table(0), "ix_same");
            Assert.That(Provider.IndexExists(Table(0), "ix_same"), Is.False);
            Verify("items", 2);
        }
        finally
        {
            provider.SetDefaultSchema(null);
            // Fixture teardown rolls back server schemas and closes SQLite attachments.
        }
    }
}
