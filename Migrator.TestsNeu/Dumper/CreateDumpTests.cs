using NUnit.Framework;
using System;
using System.Configuration;
using System.Data.SqlClient;
using Migrator;
using Migrator.Providers;
using Migrator.Tools;
using System.Collections.Generic;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;

namespace Migrator.TestsNeu.Dumper
{
	[TestFixture]
	internal class CreateDumpTests
	{
		public ConnectionStringSettings GetConnectionStringByName(string name)
		{
			ConnectionStringSettingsCollection connections = ConfigurationManager.ConnectionStrings;

			if (connections.Count != 0)
			{
				foreach (ConnectionStringSettings connection in connections)
				{
					if (connection.Name == name)
						return connection;
				}
			}
			return null;
		}
		string connectionStringSQL = "";
		string dumpString = "";
		[SetUp]
		public void SetUp()
		{
			DbProviderFactories.RegisterFactory("System.Data.SqlClient", () => SqlClientFactory.Instance);

			var cca= ConfigurationManager.ConnectionStrings[1];
			string connectionString = cca.ConnectionString;
			if (String.IsNullOrEmpty(connectionString))
				throw new ArgumentNullException("SqlServerConnectionString", "ConnectionString not found!");
			this.connectionStringSQL = connectionString;

			var dump = new SchemaDumper(ProviderTypes.SqlServer, this.connectionStringSQL, "dbo", null, "visu");
			this.dumpString = dump.GetDump();

		}
		//SqliteServerConnectionString
		//PostgresConnectionString


		[Test]
		public void TestSQLDump()
		{
			Assert.IsTrue(!String.IsNullOrEmpty(this.dumpString));
		}

		[Test]
		public void TestDumpToSQL()
		{
			//var cca = ConfigurationManager.ConnectionStrings[2];
			//string connectionString = cca.ConnectionString;

			//var parameters = new CompilerParameters();
			//parameters.GenerateExecutable = false;
			//parameters.GenerateInMemory=false;
			//var netstandard = Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
			//parameters.ReferencedAssemblies.Add(netstandard.Location);
			//var csc = new CSharpCodeProvider();
			
			//string dump=  @"
			//	public class Abc {
			//	   public string Get() { return ""abc""; }
			//	}
			//";
			
			//CompilerResults results=csc.CompileAssemblyFromSource(parameters,dump);
			
			////string dump=this.dumpString/*.Replace("\r", "").Replace("\t", "").Replace("\n", "")*/;
			////var dump = new SchemaDumper(ProviderTypes.PostgreSQL, this.connectionStringSQL, "abc", null, "visu");
			////this.dumpString = dump.GetDump();
			//string rstring = "";
			//results.Errors.Cast<CompilerError>().ToList().ForEach(error => rstring += error.ErrorText) ;

		}

		[Test]
		public void TestDumpToSQLite()
		{

		}
		[Test]
		public void TestDumpToPostgreSQL()
		{

		}

	}
}
