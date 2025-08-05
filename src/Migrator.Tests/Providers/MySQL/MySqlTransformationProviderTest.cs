// using System;
// using System.Data;
// using Migrator.Framework;
// using Migrator.Providers;
// using Migrator.Providers.Mysql;
// using Migrator.Tests.Settings;
// using Migrator.Tests.Settings.Config;
// using NUnit.Framework;

// namespace Migrator.Tests.Providers.MySQL;

// [TestFixture]
// [Category("MySql")]
// public class MySqlTransformationProviderTest : TransformationProviderConstraintBase
// {
//     [SetUp]
//     public void SetUp()
//     {
//         var configReader = new ConfigurationReader();
//         var connectionString = configReader.GetDatabaseConnectionConfigById(DatabaseConnectionConfigIds.MySQLId)
//             ?.ConnectionString;

//         if (string.IsNullOrEmpty(connectionString))
//         {
//             throw new IgnoreException("No MySQL ConnectionString is Set.");
//         }

//         DbProviderFactories.RegisterFactory("MySql.Data.MySqlClient", () => MySql.Data.MySqlClient.MySqlClientFactory.Instance);

//         Provider = new MySqlTransformationProvider(new MysqlDialect(), connectionString, "default", null);

//         AddDefaultTable();
//     }

//     [TearDown]
//     public override void TearDown()
//     {
//         DropTestTables();
//     }

//     // [Test,Ignore("MySql doesn't support check constraints")]
//     public override void CanAddCheckConstraint()
//     {
//     }

//     [Test]
//     public void AddTableWithMyISAMEngine()
//     {
//         Provider.AddTable("Test", "MyISAM",
//                            new Column("Id", DbType.Int32, ColumnProperty.NotNull),
//                            new Column("name", DbType.String, 50)
//             );
//     }

//     [Test]
//     [Ignore("needs to be fixed")]
//     public override void RemoveForeignKey()
//     {
//         //Foreign Key exists method seems not to return the key, but the ConstraintExists does
//     }
// }
