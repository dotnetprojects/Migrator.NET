//using System;
//using System.Configuration;
//using Migrator.Providers;
//using Migrator.Tools;
//using NUnit.Framework;

//namespace Migrator.Tests.Tools;

//[TestFixture]
//[Category("MySql")]
//public class SchemaDumperTest
//{
//    [Test]
//    public void Dump()
//    {
//        var constr = ConfigurationManager.AppSettings["MySqlConnectionString"];

//        if (constr == null)
//        {
//            throw new ArgumentNullException("MySqlConnectionString", "No config file");
//        }

//        var dumper = new SchemaDumper(ProviderTypes.Mysql, constr, null);
//        var output = dumper.GetDump();

//        Assert.That(output, Is.Not.Null);
//    }
//}

//[TestFixture, Category("SqlServer2005")]
//public class SchemaDumperSqlServerTest
//{
//    [Test]
//    public void Dump()
//    {
//        var constr = ConfigurationManager.AppSettings["SqlServerConnectionString"];

//        if (constr == null)
//        {
//            throw new ArgumentNullException("SqlServerConnectionString", "No config file");
//        }

//        var dumper = new SchemaDumper(ProviderTypes.SqlServer, constr, "");
//        var output = dumper.GetDump();

//        Assert.That(output, Is.Not.Null);
//    }
//}
