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
	internal class CompareDumpToOrigin
	{

		ConnectionStringSettings originalConnectionString;
		[SetUp]
		public void SetUp()
		{

			this.originalConnectionString = Helper.GetConnectionStringByName("SQLServerOriginalConnectionString");
			DbProviderFactories.RegisterFactory("System.Data.SqlClient", () => SqlClientFactory.Instance);
		}
		//SqliteServerConnectionString
		//PostgresConnectionString


		[Test]
		public void TestDumpTablesToOriginal()
		{
			if (this.originalConnectionString==null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");

			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpColumnsToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpColumnsTypeToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpColumnsMaxLengthToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpColumnsPrecisionToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpColumnsIsNullableToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpPKsToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpIsIdentityToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpIsNullableToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpFKsToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}
		[Test]
		public void TestDumpIndexToOriginal()
		{
			if (this.originalConnectionString == null)
				Assert.Warn("ConnectionString not available: SQLServerOriginalConnectionString");
			string query = @"select SCHEMA_NAME(schema_id),*from sys.tables";
		}


	}
}
