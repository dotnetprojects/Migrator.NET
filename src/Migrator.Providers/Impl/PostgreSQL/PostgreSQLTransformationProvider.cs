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

using System;
using System.Collections.Generic;
using System.Data;
using Migrator.Framework;
using Npgsql;

namespace Migrator.Providers.PostgreSQL
{
	/// <summary>
	/// Migration transformations provider for PostgreSql (using NPGSql .Net driver)
	/// </summary>
	public class PostgreSQLTransformationProvider : TransformationProvider
	{
		public PostgreSQLTransformationProvider(Dialect dialect, string connectionString, string defaultSchema)
			: base(dialect, connectionString, defaultSchema)
		{
			_connection = new NpgsqlConnection();
			_connection.ConnectionString = _connectionString;
			_connection.Open();
		}

		public override void RemoveTable(string name)
		{
            if (!TableExists(name))
            {
                throw new MigrationException(String.Format("Table with name '{0}' does not exist to rename", name));
            }

			ExecuteNonQuery(String.Format("DROP TABLE IF EXISTS {0} CASCADE", name));
		}

		public override bool ConstraintExists(string table, string name)
		{
			using (IDataReader reader =
				ExecuteQuery(string.Format("SELECT constraint_name FROM information_schema.table_constraints WHERE table_schema = 'public' AND constraint_name = lower('{0}')", name)))
			{
				return reader.Read();
			}
		}

		public override bool ColumnExists(string table, string column)
		{
			if (!TableExists(table))
				return false;

			using (IDataReader reader =
				ExecuteQuery(String.Format("SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = lower('{0}') AND (column_name = lower('{1}') OR column_name = '{1}')", table, column)))
			{
				return reader.Read();
			}
		}

		public override bool TableExists(string table)
		{
			using (IDataReader reader =
				ExecuteQuery(String.Format("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name = lower('{0}')", table)))
			{
				return reader.Read();
			}
		}

		public override void ChangeColumn(string table, Column column)
		{
			if (!ColumnExists(table, column.Name))
			{
				Logger.Warn("Column {0}.{1} does not exist", table, column.Name);
				return;
			}

		    var existingColumn = GetColumnByName(table, column.Name);
		    
            column.Name = existingColumn.Name; // name might have different case.

			string tempColumn = "temp_" + column.Name;
			RenameColumn(table, column.Name, tempColumn);

			// check if this is not-null
			bool isNotNull = (column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull;

			// remove the not-null option
			column.ColumnProperty = (column.ColumnProperty & ~ColumnProperty.NotNull);

			AddColumn(table, column);
			ExecuteQuery(String.Format("UPDATE {0} SET {1}={2}", table, Dialect.Quote(column.Name), tempColumn));
			RemoveColumn(table, tempColumn);

			// if is not null, set that now
            if (isNotNull) ExecuteQuery(string.Format("ALTER TABLE {0} ALTER COLUMN {1} SET NOT NULL", table, Dialect.Quote(column.Name)));
		}

		public override string[] GetTables()
		{
			var tables = new List<string>();
			using (IDataReader reader = ExecuteQuery("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
			{
				while (reader.Read())
				{
					tables.Add((string) reader[0]);
				}
			}
			return tables.ToArray();
		}

		public override Column[] GetColumns(string table)
		{
			var columns = new List<Column>();
			using (
				IDataReader reader =
					ExecuteQuery(
						String.Format("select COLUMN_NAME, IS_NULLABLE from information_schema.columns where table_schema = 'public' AND table_name = lower('{0}');", table)))
			{
				// FIXME: Mostly duplicated code from the Transformation provider just to support stupid case-insensitivty of Postgre
				while (reader.Read())
				{
					var column = new Column(reader[0].ToString(), DbType.String);
					bool isNullable = reader.GetString(1) == "YES";
					column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

					columns.Add(column);
				}
			}

			return columns.ToArray();
		}

		public override Column GetColumnByName(string table, string columnName)
		{
			// Duplicate because of the lower case issue
			return Array.Find(GetColumns(table), column => column.Name == columnName.ToLower() || column.Name == columnName);
		}

        public override bool IndexExists(string table, string name)
        {
            using (IDataReader reader =
                ExecuteQuery(string.Format("SELECT indexname FROM pg_catalog.pg_indexes WHERE indexname = lower('{0}')", name)))
            {
                return reader.Read();
            }
        }
	}
}