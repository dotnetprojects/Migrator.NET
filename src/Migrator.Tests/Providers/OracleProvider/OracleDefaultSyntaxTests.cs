using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using Migrator.Tests.Settings;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleDefaultSyntaxTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ElementColumnDefaultCanBeRemoved(bool throughChangeColumn)
    {
        var connectionString = new ConfigurationReader().GetDatabaseConnectionConfigById("Oracle")?.ConnectionString;
        if (string.IsNullOrEmpty(connectionString)) Assert.Ignore("No Oracle connection configured.");
        using var connection = new OracleConnection(connectionString);
        connection.Open();
        using var provider = new OracleTransformationProvider(new OracleDialect(), connection, null, "default", "Oracle.ManagedDataAccess.Client");
        var table = "Default_" + Guid.NewGuid().ToString("N")[..12];
        provider.AddTable(table, new Column("Id", DbType.Int32),
            new Column("Element", DbType.String, 32) { DefaultValue = "fallback" });
        try
        {
            provider.Insert(table, ["Id"], [1]);
            if (throughChangeColumn)
                provider.ChangeColumn(table, new Column("Element", DbType.String, 64));
            else
                provider.RemoveColumnDefaultValue(table, "Element");
            provider.Insert(table, ["Id"], [2]);
            Assert.That(provider.ExecuteScalar("SELECT Element FROM " + table + " WHERE Id=1"), Is.EqualTo("fallback"));
            Assert.That(provider.ExecuteScalar("SELECT Element FROM " + table + " WHERE Id=2"), Is.EqualTo(DBNull.Value));
        }
        finally
        {
            provider.RemoveTable(table);
        }
    }
}
