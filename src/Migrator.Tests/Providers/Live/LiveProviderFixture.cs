using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Threading.Tasks;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;
using Sap.Data.Hana;

namespace Migrator.Tests.Providers.Live;

public abstract class LiveProviderFixture(string database, ProviderTypes providerType) : TransformationProviderBase
{
    protected LiveDatabaseTests live;
    private HanaConnection hana;
    private DbConnection ingres;
    private string schema;

    [SetUp]
    public async Task SetUp()
    {
        switch (database)
        {
            case "SQLite": await BeginSQLiteTransactionAsync(); break;
            case "SQLServer": await BeginSQLServerTransactionAsync(); break;
            case "PostgreSQL": await BeginPostgreSQLTransactionAsync(); break;
            case "Oracle": await BeginOracleTransactionAsync(); break;
            case "Ingres":
                var driver = Environment.GetEnvironmentVariable("MIGRATOR_INGRES_DRIVER")
                    ?? throw new InvalidOperationException("Set MIGRATOR_INGRES_DRIVER to a .NET-compatible Actian driver assembly path.");
                var connectionString = Environment.GetEnvironmentVariable("MIGRATOR_INGRES")
                    ?? throw new InvalidOperationException("Set MIGRATOR_INGRES to a disposable Ingres database connection string.");
                var connectionType = Assembly.LoadFrom(driver).GetTypes().Single(t => t.IsPublic && !t.IsAbstract && typeof(DbConnection).IsAssignableFrom(t));
                ingres = (DbConnection)Activator.CreateInstance(connectionType);
                ingres.ConnectionString = connectionString;
                ingres.Open();
                Provider = ProviderFactory.Create(providerType, ingres, null, "namespace-tests");
                if (Provider.GetTables().Length != 0) throw new InvalidOperationException("Ingres namespace tests require an empty, disposable owner namespace.");
                Provider.BeginTransaction();
                break;
            case "Hana":
                hana = new HanaConnection(Environment.GetEnvironmentVariable("MIGRATOR_HANA")
                    ?? "Server=localhost:39041;UserID=SYSTEM;Password=MgT9ci7Q4xZ2");
                hana.Open();
                var name = "BOUNDARY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
                using (var command = hana.CreateCommand())
                {
                    command.CommandText = "CREATE SCHEMA " + name;
                    command.ExecuteNonQuery();
                    schema = name;
                    command.CommandText = "SET SCHEMA " + schema;
                    command.ExecuteNonQuery();
                }
                Provider = ProviderFactory.Create(providerType, hana, schema, "boundary-tests");
                break;
            default:
                live = new LiveDatabaseTests(database, providerType);
                live.SetUp();
                Provider = live.Provider;
                break;
        }
        // A too-long value must not silently truncate on engines with configurable modes.
        if (database is "MySQL" or "MariaDB") Provider.ExecuteNonQuery("SET SESSION sql_mode='STRICT_ALL_TABLES'");
        if (database == "Sybase")
        {
            Provider.ExecuteNonQuery("SET STRING_RTRUNCATION ON");
            Provider.ExecuteNonQuery("SET TEXTSIZE 2147483647");
        }
    }

    [TearDown]
    public override void TearDown()
    {
        try
        {
            if (live != null) live.TearDown();
            else if (ingres != null) Provider?.Rollback();
            else if (hana != null)
            {
                Provider?.Dispose();
                if (schema != null && hana.State == ConnectionState.Open)
                {
                    using var command = hana.CreateCommand();
                    command.CommandText = "DROP SCHEMA " + schema + " CASCADE";
                    command.ExecuteNonQuery();
                }
            }
            else base.TearDown();
        }
        finally
        {
            if (live == null) Provider?.Dispose();
            hana?.Dispose();
            ingres?.Dispose();
            Provider = null;
            live = null;
            hana = null;
            ingres = null;
            schema = null;
        }
    }

}
