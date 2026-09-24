using System;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using Migrator.Tests.Settings;
using NUnit.Framework;

namespace Migrator.Tests;

[TestFixture(ProviderTypes.SqlServer, "SQLServer", Category = "SQLServer")]
[TestFixture(ProviderTypes.PostgreSQL, "PostgreSQL", Category = "PostgreSQL")]
[TestFixture(ProviderTypes.Oracle, "Oracle", Category = "Oracle")]
public class DeleteDuplicateRowsLiveTests(ProviderTypes type, string configurationId)
{
    [TestCase(false, DuplicateNullHandling.Equal, 4)]
    [TestCase(false, DuplicateNullHandling.ExcludeNullKeys, 2)]
    [TestCase(true, DuplicateNullHandling.Equal, 4)]
    [TestCase(true, DuplicateNullHandling.ExcludeNullKeys, 2)]
    public void DeletesOnlyDuplicateCompositeKeys(bool fluent, DuplicateNullHandling nulls, int expected)
    {
        var config = new ConfigurationReader().GetDatabaseConnectionConfigById(configurationId);
        using IDbConnection connection = type switch
        {
            ProviderTypes.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(config.ConnectionString),
            ProviderTypes.PostgreSQL => new Npgsql.NpgsqlConnection(config.ConnectionString),
            _ => new Oracle.ManagedDataAccess.Client.OracleConnection(config.ConnectionString)
        };
        connection.Open();
        using var provider = ProviderFactory.Create(type, connection, config.Schema);
        string table = "Dedup_" + Guid.NewGuid().ToString("N")[..12];
        provider.AddTable(table, new Column("Key One", DbType.Int32), new Column("select", DbType.Int32), new Column("Payload", DbType.String, 20));
        try
        {
            object[][] rows = [[1,1,"one"],[1,1,"two"],[1,1,"three"],[1,2,"different"],
                [null,1,"n1"],[null,1,"n2"],[null,null,"n3"],[null,null,"n4"],[9,9,"unique"]];
            foreach (var row in rows) provider.Insert(table, ["Key One", "select", "Payload"], row);
            if (fluent)
            {
                var builder = new MigrationBuilder();
                builder.Delete.DuplicateRows().FromTable(table).ByColumns("Key One", "select").KeepAny(nulls);
                builder.Apply(provider);
            }
            else Assert.That(provider.DeleteDuplicateRows(table, ["Key One", "select"], DuplicateRowRetention.Any, nulls), Is.EqualTo(expected));
            string quotedTable = provider.QuoteTableNameIfRequired(table);
            Assert.That(Convert.ToInt32(provider.ExecuteScalar($"SELECT COUNT(*) FROM {quotedTable}")), Is.EqualTo(9 - expected));
            Assert.That(Convert.ToString(provider.ExecuteScalar($"SELECT {provider.QuoteColumnNameIfRequired("Payload")} FROM {quotedTable} WHERE {provider.QuoteColumnNameIfRequired("Key One")}=9")), Is.EqualTo("unique"));
            Assert.That(provider.DeleteDuplicateRows(table, ["Key One", "select"], DuplicateRowRetention.Any, nulls), Is.Zero);
        }
        finally { provider.RemoveTable(table); }
    }
}
