using System;
using System.Data;
using Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers
{
	/// <summary>
	/// Base class for Provider tests for all non-constraint oriented tests.
	/// </summary>
	public class TransformationProviderBase
	{
		protected ITransformationProvider _provider;

		[TearDown]
		public virtual void TearDown()
		{
			DropTestTables();

			_provider.Rollback();
		}

		protected void DropTestTables()
		{
			// Because MySql doesn't support schema transaction
			// we got to remove the tables manually... sad...
			try
			{
				_provider.RemoveTable("TestTwo");
			}
			catch (Exception)
			{
			}
			try
			{
				_provider.RemoveTable("Test");
			}
			catch (Exception)
			{
			}
			try
			{
				_provider.RemoveTable("SchemaInfo");
			}
			catch (Exception)
			{
			}
		}

		public void AddDefaultTable()
		{
			_provider.AddTable("TestTwo",
							   new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
							   new Column("TestId", DbType.Int32, ColumnProperty.ForeignKey)
				);
		}

		public void AddTable()
		{
			_provider.AddTable("Test",
							   new Column("Id", DbType.Int32, ColumnProperty.NotNull),
							   new Column("Title", DbType.String, 100, ColumnProperty.Null),
							   new Column("name", DbType.String, 50, ColumnProperty.Null),
							   new Column("blobVal", DbType.Binary, ColumnProperty.Null),
							   new Column("boolVal", DbType.Boolean, ColumnProperty.Null),
							   new Column("bigstring", DbType.String, 50000, ColumnProperty.Null)
				);
		}

		public void AddTableWithPrimaryKey()
		{
			_provider.AddTable("Test",
							   new Column("Id", DbType.Int32, ColumnProperty.PrimaryKeyWithIdentity),
							   new Column("Title", DbType.String, 100, ColumnProperty.Null),
							   new Column("name", DbType.String, 50, ColumnProperty.NotNull),
							   new Column("blobVal", DbType.Binary),
							   new Column("boolVal", DbType.Boolean),
							   new Column("bigstring", DbType.String, 50000)
				);
		}

		[Test]
		public void TableExistsWorks()
		{
			Assert.That(_provider.TableExists("gadadadadseeqwe"), Is.False);
			Assert.That(_provider.TableExists("TestTwo"), Is.True);
		}

		[Test]
		public void ColumnExistsWorks()
		{
			Assert.That(_provider.ColumnExists("gadadadadseeqwe", "eqweqeq"), Is.False);
			Assert.That(_provider.ColumnExists("TestTwo", "eqweqeq"), Is.False);
			Assert.That(_provider.ColumnExists("TestTwo", "Id"),Is.True );
		}

		[Test]
		public void CanExecuteBadSqlForNonCurrentProvider()
		{
			_provider["foo"].ExecuteNonQuery("select foo from bar 123");
		}

		[Test]
		public void TableCanBeAdded()
		{
			AddTable();
			Assert.That(_provider.TableExists("Test"), Is.True);
		}

		[Test]
		public void GetTablesWorks()
		{
			foreach (string name in _provider.GetTables())
			{
				_provider.Logger.Log("Table: {0}", name);
			}
			Assert.That(1, Is.EqualTo(_provider.GetTables().Length));
			AddTable();
			Assert.That(2, Is.EqualTo(_provider.GetTables().Length));
		}

		[Test]
		public void GetColumnsReturnsProperCount()
		{
			AddTable();
			Column[] cols = _provider.GetColumns("Test");

			Assert.That(cols, Is.Not.Null);
			Assert.That(6, Is.EqualTo(cols.Length));
		}

		[Test]
		public void GetColumnsContainsProperNullInformation()
		{
			AddTableWithPrimaryKey();
			Column[] cols = _provider.GetColumns("Test");
			Assert.That(cols, Is.Not.Null);

			foreach (Column column in cols)
			{
				if (column.Name == "name")
					Assert.That((column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull, Is.True);
				else if (column.Name == "Title")
				{
					Assert.That((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null, Is.True);
				}
			}
		}

		[Test]
		public void CanAddTableWithPrimaryKey()
		{
			AddTableWithPrimaryKey();
			Assert.That(_provider.TableExists("Test"), Is.True);
		}

		[Test]
		public void RemoveTable()
		{
			AddTable();
			_provider.RemoveTable("Test");
			Assert.That(_provider.TableExists("Test"), Is.False);
		}

		[Test]
		public virtual void RenameTableThatExists()
		{
			AddTable();
			_provider.RenameTable("Test", "Test_Rename");

			Assert.That(_provider.TableExists("Test_Rename"), Is.True);
			Assert.That(_provider.TableExists("Test"), Is.False);
			_provider.RemoveTable("Test_Rename");
		}

		[Test]
		public void RenameTableToExistingTable()
		{
			AddTable();
			Assert.Throws<MigrationException>(() =>
			{
				_provider.RenameTable("Test", "TestTwo");
			});
		}

		[Test]
		public void RenameColumnThatExists()
		{
			AddTable();
			_provider.RenameColumn("Test", "name", "name_rename");

			Assert.That(_provider.ColumnExists("Test", "name_rename"), Is.True);
			Assert.That(_provider.ColumnExists("Test", "name"), Is.False);
		}

		[Test]
		public void RenameColumnToExistingColumn()
		{
			AddTable();
			Assert.Throws<MigrationException>(() =>
			{
				_provider.RenameColumn("Test", "Title", "name");
			});
		}

		[Test]
		public void RemoveUnexistingTable()
		{
			_provider.RemoveTable("abc");
		}

		[Test]
		public void AddColumn()
		{
			_provider.AddColumn("TestTwo", "Test", DbType.String, 50);
			Assert.That(_provider.ColumnExists("TestTwo", "Test"), Is.True);
		}

		[Test]
		public void ChangeColumn()
		{
			_provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50));
			Assert.That(_provider.ColumnExists("TestTwo", "TestId"), Is.True);
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 1, "Not an Int val." });
		}

		[Test]
		public void ChangeColumn_FromNullToNull()
		{
			_provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
			_provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
			_provider.ChangeColumn("TestTwo", new Column("TestId", DbType.String, 50, ColumnProperty.Null));
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 2, "Not an Int val." });
		}

		[Test]
		public void AddDecimalColumn()
		{
			_provider.AddColumn("TestTwo", "TestDecimal", DbType.Decimal, 38);
			Assert.That(_provider.ColumnExists("TestTwo", "TestDecimal"), Is.True);
		}

		[Test]
		public void AddColumnWithDefault()
		{
			_provider.AddColumn("TestTwo", "TestWithDefault", DbType.Int32, 50, 0, 10);
			Assert.That(_provider.ColumnExists("TestTwo", "TestWithDefault"), Is.True);
		}

		[Test]
		public void AddColumnWithDefaultButNoSize()
		{
			_provider.AddColumn("TestTwo", "TestWithDefault", DbType.Int32, 10);
			Assert.That(_provider.ColumnExists("TestTwo", "TestWithDefault"), Is.True);

			_provider.AddColumn("TestTwo", "TestWithDefaultString", DbType.String, "'foo'");
			Assert.That(_provider.ColumnExists("TestTwo", "TestWithDefaultString"), Is.True);
		}

		[Test]
		public void AddBooleanColumnWithDefault()
		{
			_provider.AddColumn("TestTwo", "TestBoolean", DbType.Boolean, 0, 0, false);
			Assert.That(_provider.ColumnExists("TestTwo", "TestBoolean"), Is.True);
		}

		[Test]
		public void CanGetNullableFromProvider()
		{
			_provider.AddColumn("TestTwo", "NullableColumn", DbType.String, 30, ColumnProperty.Null);
			Column[] columns = _provider.GetColumns("TestTwo");

			foreach (Column column in columns)
			{
				if (column.Name == "NullableColumn")
				{
					Assert.That((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null, Is.True);
				}
			}
		}

		[Test]
		public void RemoveColumn()
		{
			AddColumn();
			_provider.RemoveColumn("TestTwo", "Test");
			Assert.That(_provider.ColumnExists("TestTwo", "Test"), Is.False);
		}

		[Test]
		public void RemoveColumnWithDefault()
		{
			AddColumnWithDefault();
			_provider.RemoveColumn("TestTwo", "TestWithDefault");
			Assert.That(_provider.ColumnExists("TestTwo", "TestWithDefault"), Is.False);
		}

		[Test]
		public void RemoveUnexistingColumn()
		{
			_provider.RemoveColumn("TestTwo", "abc");
			_provider.RemoveColumn("abc", "abc");
		}

		/// <summary>
		/// Supprimer une colonne bit causait une erreur à cause
		/// de la valeur par défaut.
		/// </summary>
		[Test]
		public void RemoveBoolColumn()
		{
			AddTable();
			_provider.AddColumn("Test", "Inactif", DbType.Boolean);
			Assert.That(_provider.ColumnExists("Test", "Inactif"), Is.True);

			_provider.RemoveColumn("Test", "Inactif");
			Assert.That(_provider.ColumnExists("Test", "Inactif"), Is.False);
		}

		[Test]
		public void HasColumn()
		{
			AddColumn();
			Assert.That(_provider.ColumnExists("TestTwo", "Test"), Is.True);
			Assert.That(_provider.ColumnExists("TestTwo", "TestPasLa"), Is.False);
		}

		[Test]
		public void HasTable()
		{
			Assert.That(_provider.TableExists("TestTwo"), Is.True);
		}

		[Test]
		public void AppliedMigrations()
		{
			Assert.That(_provider.TableExists("SchemaInfo"), Is.False);

			// Check that a "get" call works on the first run.
			Assert.That(0,Is.EqualTo( _provider.AppliedMigrations.Count));
			Assert.That(_provider.TableExists("SchemaInfo"),Is.True, "No SchemaInfo table created");

			// Check that a "set" called after the first run works.
			_provider.MigrationApplied(1, null);
			Assert.That(1, Is.EqualTo(_provider.AppliedMigrations[0]));

			_provider.RemoveTable("SchemaInfo");
			// Check that a "set" call works on the first run.
			_provider.MigrationApplied(1, null);
			Assert.That(1, Is.EqualTo(_provider.AppliedMigrations[0]));
			Assert.That(_provider.TableExists("SchemaInfo"), Is.True, "No SchemaInfo table created");
		}

		/// <summary>
		/// Reproduce bug reported by Luke Melia & Daniel Berlinger :
		/// http://macournoyer.wordpress.com/2006/10/15/migrate-nant-task/#comment-113
		/// </summary>
		[Test]
		public void CommitTwice()
		{
			_provider.Commit();
			Assert.That(0, Is.EqualTo(_provider.AppliedMigrations.Count));
			_provider.Commit();
		}

		[Test]
		public void InsertData()
		{
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 1, "1" });
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 2, "2" });

			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "TestId", "TestTwo"))
			{
				int[] vals = GetVals(reader);

				Assert.That(Array.Exists(vals, delegate (int val) { return val == 1; }), Is.True);
				Assert.That(Array.Exists(vals, delegate (int val) { return val == 2; }), Is.True);
			}
		}

		[Test]
		public void CanInsertNullData()
		{
			AddTable();

			_provider.Insert("Test", new[] { "Id", "Title" }, new[] { "1", "foo" });
			_provider.Insert("Test", new[] { "Id", "Title" }, new[] { "2", null });

			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "Title", "Test"))
			{
				string[] vals = GetStringVals(reader);

				Assert.That(Array.Exists(vals, delegate (string val) { return val == "foo"; }), Is.True);
				Assert.That(Array.Exists(vals, delegate (string val) { return val == null; }), Is.True);
			}
		}

		[Test]
		public void CanInsertDataWithSingleQuotes()
		{
			AddTable();
			_provider.Insert("Test", new[] { "Id", "Title" }, new[] { "1", "Muad'Dib" });
			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "Title", "Test"))
			{
				Assert.That(reader.Read(), Is.True);
				Assert.That("Muad'Dib", Is.EqualTo(reader.GetString(0)));
				Assert.That(reader.Read(), Is.False);
			}
		}

		[Test]
		public void DeleteData()
		{
			InsertData();
			_provider.Delete("TestTwo", "TestId", "1");
			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "TestId", "TestTwo"))
			{
				Assert.That(reader.Read(), Is.True);
				Assert.That(2, Is.EqualTo(Convert.ToInt32(reader[0])));
				Assert.That(reader.Read(), Is.False);
			}
		}

		[Test]
		public void DeleteDataWithArrays()
		{
			InsertData();
			_provider.Delete("TestTwo", new[] { "TestId" }, new[] { "1" });
			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "TestId", "TestTwo"))
			{
				Assert.That(reader.Read(),  Is.True);
				Assert.That(2, Is.EqualTo(Convert.ToInt32(reader[0])));
				Assert.That(reader.Read(), Is.False);
			}
		}

		[Test]
		public void UpdateData()
		{
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 20, "1" });
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 21, "2" });

			_provider.Update("TestTwo", new[] { "TestId" }, new[] { "3" });
			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "TestId", "TestTwo"))
			{
				int[] vals = GetVals(reader);

				Assert.That(Array.Exists(vals, delegate (int val) { return val == 3; }), Is.True);
				Assert.That(Array.Exists(vals, delegate (int val) { return val == 1; }), Is.False);
				Assert.That(Array.Exists(vals, delegate (int val) { return val == 2; }), Is.False);
			}
		}

		[Test]
		public void CanUpdateWithNullData()
		{
			AddTable();
			_provider.Insert("Test", new[] { "Id", "Title" }, new[] { "1", "foo" });
			_provider.Insert("Test", new[] { "Id", "Title" }, new[] { "2", null });

			_provider.Update("Test", new[] { "Title" }, new string[] { null });
			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "Title", "Test"))
			{
				string[] vals = GetStringVals(reader);

				Assert.That(vals[0], Is.Null);
				Assert.That(vals[1], Is.Null);
			}
		}

		[Test]
		public void UpdateDataWithWhere()
		{
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 10, "1" });
			_provider.Insert("TestTwo", new[] { "Id", "TestId" }, new object[] { 11, "2" });

			_provider.Update("TestTwo", new[] { "TestId" }, new[] { "3" }, "TestId='1'");
			using (var cmd = _provider.CreateCommand())
			using (IDataReader reader = _provider.Select(cmd, "TestId", "TestTwo"))
			{
				int[] vals = GetVals(reader);

				Assert.That(Array.Exists(vals, delegate (int val) { return val == 3; }), Is.True);
				Assert.That(Array.Exists(vals, delegate (int val) { return val == 2; }), Is.True);
				Assert.That(Array.Exists(vals, delegate (int val) { return val == 1; }), Is.False);
			}
		}

		[Test]
		public void AddIndex()
		{
			string indexName = "test_index";

			Assert.That(_provider.IndexExists("TestTwo", indexName), Is.False);
			_provider.AddIndex(indexName, "TestTwo", "Id", "TestId");
			Assert.That(_provider.IndexExists("TestTwo", indexName), Is.True);
		}

		[Test]
		public void RemoveIndex()
		{
			string indexName = "test_index";

			Assert.That(_provider.IndexExists("TestTwo", indexName), Is.False);
			_provider.AddIndex(indexName, "TestTwo", "Id", "TestId");
			_provider.RemoveIndex("TestTwo", indexName);
			Assert.That(_provider.IndexExists("TestTwo", indexName), Is.False);
		}


		int[] GetVals(IDataReader reader)
		{
			var vals = new int[2];
			Assert.That(reader.Read(), Is.True);
			vals[0] = Convert.ToInt32(reader[0]);
			Assert.That(reader.Read(), Is.True);
			vals[1] = Convert.ToInt32(reader[0]);
			return vals;
		}

		string[] GetStringVals(IDataReader reader)
		{
			var vals = new string[2];
			Assert.That(reader.Read(), Is.True);
			vals[0] = reader[0] as string;
			Assert.That(reader.Read(), Is.True);
			vals[1] = reader[0] as string;
			return vals;
		}
	}
}
