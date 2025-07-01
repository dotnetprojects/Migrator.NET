using System;
using System.Collections.Generic;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Framework;
using Migrator.Providers.SQLite;
using Migrator.Tests.Settings;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite.Base;

[TestFixture]
[Category("SQLite")]
public class SQLiteTransformationProviderTestBase : TransformationProviderBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById("SQLiteConnectionString")
            .ConnectionString;

        _provider = new SQLiteTransformationProvider(new SQLiteDialect(), connectionString, "default", null);
        _provider.BeginTransaction();

        AddDefaultTable();
    }
}
