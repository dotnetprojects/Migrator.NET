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

using System.Linq;
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
            // Arrange
            AddTableWithPrimaryKey();

            // Act
            _provider.AddForeignKey("FK name is not supported by SQLite", parentTable: "Test", parentColumn: "Id", childTable: "TestTwo", childColumn: "TestId", ForeignKeyConstraintType.Cascade);

            // Assert
            var foreignKeyConstraints = ((SQLiteTransformationProvider)_provider).GetForeignKeyConstraints("TestTwo");
            var tableSQLCreateScript = ((SQLiteTransformationProvider)_provider).GetSqlCreateTableScript("TestTwo");

            Assert.That(foreignKeyConstraints.Single().Name, Is.Null);
            Assert.That(foreignKeyConstraints.Single().ChildTable, Is.EqualTo("TestTwo"));
            Assert.That(foreignKeyConstraints.Single().ParentTable, Is.EqualTo("Test"));
            Assert.That(foreignKeyConstraints.Single().ChildColumns.Single(), Is.EqualTo("TestId"));
            Assert.That(foreignKeyConstraints.Single().ParentColumns.Single(), Is.EqualTo("Id"));

            Assert.That(tableSQLCreateScript, Does.Contain("CREATE TABLE \"TestTwo\""));
            Assert.That(tableSQLCreateScript, Does.Contain(", FOREIGN KEY (TestId) REFERENCES Test(Id))"));
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
