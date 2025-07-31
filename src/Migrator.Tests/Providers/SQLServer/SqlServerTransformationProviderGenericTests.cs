#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System.Data;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Providers;
using Migrator.Providers.SQLite;
using Migrator.Providers.SqlServer;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SqlServer")]
public class SqlServerTransformationProviderGenericTests : TransformationProviderConstraintBase
{
    [SetUp]
    public void SetUp()
    {
        var configReader = new ConfigurationReader();
        var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.SQLServerConnectionConfigId)
            .ConnectionString;

        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", () => Microsoft.Data.SqlClient.SqlClientFactory.Instance);

        Provider = new SqlServerTransformationProvider(new SqlServerDialect(), connectionString, "dbo", "default", "Microsoft.Data.SqlClient");
        Provider.BeginTransaction();

        AddDefaultTable();
    }

    [Test]
    public void ByteColumnWillBeCreatedAsBlob()
    {
        Provider.AddColumn("TestTwo", "BlobColumn", DbType.Byte);
        Assert.That(Provider.ColumnExists("TestTwo", "BlobColumn"), Is.True);
    }

    [Test]
    public void InstanceForProvider()
    {
        var localProv = Provider["sqlserver"];
        Assert.That(localProv is SqlServerTransformationProvider, Is.True);

        var localProv2 = Provider["foo"];
        Assert.That(localProv2 is NoOpTransformationProvider, Is.True);
    }

    [Test]
    public void QuoteCreatesProperFormat()
    {
        var dialect = new SqlServerDialect();

        Assert.That("[foo]", Is.EqualTo(dialect.Quote("foo")));
    }

    [Test]
    public void TableExistsShouldWorkWithBracketsAndSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("[dbo].[TestTwo]"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithSchemaNameAndTableName()
    {
        Assert.That(Provider.TableExists("dbo.TestTwo"), Is.True);
    }

    [Test]
    public void TableExistsShouldWorkWithTableNamesWithBracket()
    {
        Assert.That(Provider.TableExists("[TestTwo]"), Is.True);
    }
}