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

using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Migrator.Providers.SQLite;
using Migrator.Tests.Settings;
using NUnit.Framework;

namespace Migrator.Tests.Providers
{
    [TestFixture]
    [Category("SQLite")]
    public class SQLiteTransformationProviderTest : TransformationProviderBase
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

        [Test]
        public void AddForeignKey()
        {
            AddTableWithPrimaryKey();
            _provider.AddForeignKey("Will not be used by SQLite", "Test", "Id", "TestTwo", "TestId", ForeignKeyConstraintType.SetDefault);
        }


        [Test]
        public void CanParseColumnDefForName()
        {
            //const string nullString = "bar TEXT";
            //const string notNullString = "baz INTEGER NOT NULL";
            //Assert.That("bar", ((SQLiteTransformationProvider) _provider).ExtractNameFromColumnDef(nullString));
            //Assert.That("baz", ((SQLiteTransformationProvider) _provider).ExtractNameFromColumnDef(notNullString));
        }

        [Test]
        public void CanParseColumnDefForNotNull()
        {
            const string nullString = "bar TEXT";
            const string notNullString = "baz INTEGER NOT NULL";
            Assert.That(((SQLiteTransformationProvider)_provider).IsNullable(nullString), Is.True);
            Assert.That(((SQLiteTransformationProvider)_provider).IsNullable(notNullString), Is.False);
        }

        [Test]
        public void CanParseSqlDefinitions()
        {
            //const string testSql = "CREATE TABLE bar ( id INTEGER PRIMARY KEY AUTOINCREMENT, bar TEXT, baz INTEGER NOT NULL )";
            //string[] columns = ((SQLiteTransformationProvider) _provider).ParseSqlColumnDefs(testSql);
            //Assert.IsNotNull(columns);
            //Assert.That(3, columns.Length);
            //Assert.That("id INTEGER PRIMARY KEY AUTOINCREMENT", columns[0]);
            //Assert.That("bar TEXT", columns[1]);
            //Assert.That("baz INTEGER NOT NULL", columns[2]);
        }

        [Test]
        public void CanParseSqlDefinitionsForColumnNames()
        {
            //const string testSql = "CREATE TABLE bar ( id INTEGER PRIMARY KEY AUTOINCREMENT, bar TEXT, baz INTEGER NOT NULL )";
            //string[] columns = ((SQLiteTransformationProvider) _provider).ParseSqlForColumnNames(testSql);
            //Assert.IsNotNull(columns);
            //Assert.That(3, columns.Length);
            //Assert.That("id", columns[0]);
            //Assert.That("bar", columns[1]);
            //Assert.That("baz", columns[2]);
        }
    }
}
