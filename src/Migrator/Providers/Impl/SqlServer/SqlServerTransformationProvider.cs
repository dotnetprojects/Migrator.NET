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
using Index = Migrator.Framework.Index;

namespace Migrator.Providers.SqlServer
{
	/// <summary>
	/// Migration transformations provider for Microsoft SQL Server.
	/// </summary>
	public class SqlServerTransformationProvider : TransformationProvider
	{
		public SqlServerTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
			: base(dialect, connectionString, defaultSchema, scope)
		{
			CreateConnection(providerName);
		}

		public SqlServerTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
		   : base(dialect, connection, defaultSchema, scope)
		{
		}


		protected virtual void CreateConnection(string providerName)
		{
			if (string.IsNullOrEmpty(providerName))
				providerName = "System.Data.SqlClient";
			var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
			_connection = fac.CreateConnection(); //  new SqlConnection();
			_connection.ConnectionString = _connectionString;
			_connection.Open();

			string collationString = null;
			var collation = this.ExecuteScalar("SELECT DATABASEPROPERTYEX('" + _connection.Database + "', 'Collation')");
			if (collation != null)
				collationString = collation.ToString();
			if (string.IsNullOrWhiteSpace(collationString))
				collationString = "Latin1_General_CI_AS";
			this.Dialect.RegisterProperty(ColumnProperty.CaseSensitive, "COLLATE " + collationString.Replace("_CI_", "_CS_"));
		}

		public override bool ConstraintExists(string table, string name)
		{
			bool retVal = false;
			using (var cmd = CreateCommand())
			using (IDataReader reader = ExecuteQuery(cmd, string.Format("SELECT TOP 1 * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME ='{0}'", name)))
			{
				retVal = reader.Read();
			}

			if (!retVal)
				using (var cmd = CreateCommand())
				using (IDataReader reader = ExecuteQuery(cmd, string.Format("SELECT TOP 1 * FROM SYS.DEFAULT_CONSTRAINTS WHERE PARENT_OBJECT_ID = OBJECT_ID('{0}') AND Name = '{1}'", table, name)))
				{
					return reader.Read();
				}
			return true;
		}

		public override void AddColumn(string table, string sqlColumn)
		{
			table = _dialect.TableNameNeedsQuote ? _dialect.Quote(table) : table;
			ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
		}
		public override void AddPrimaryKeyNonClustered(string name, string table, params string[] columns)
		{
			if (ConstraintExists(table, name))
			{
				Logger.Warn("Primary key {0} already exists", name);
				return;
			}
			string nonclusteredString = "NONCLUSTERED";
			ExecuteNonQuery(
			String.Format("ALTER TABLE {0} ADD CONSTRAINT {1} PRIMARY KEY {2} ({3}) ", table, name, nonclusteredString,
						  String.Join(",", QuoteColumnNamesIfRequired(columns))));
		}
		public override void AddIndex(string table, Index index)
		{
			if (IndexExists(table, index.Name))
			{
				Logger.Warn("Index {0} already exists", index.Name);
				return;
			}

			var name = QuoteConstraintNameIfRequired(index.Name);

			table = QuoteTableNameIfRequired(table);

			var columns = QuoteColumnNamesIfRequired(index.KeyColumns);

			if (index.IncludeColumns != null && index.IncludeColumns.Length > 0)
			{
				var include = QuoteColumnNamesIfRequired(index.IncludeColumns);
				ExecuteNonQuery(String.Format("CREATE {0}{1} INDEX {2} ON {3} ({4}) INCLUDE ({5})", (index.Unique ? "UNIQUE " : ""), (index.Clustered ? "CLUSTERED" : "NONCLUSTERED"), name, table, string.Join(", ", columns), string.Join(", ", include)));
			}
			else
			{
				ExecuteNonQuery(String.Format("CREATE {0}{1} INDEX {2} ON {3} ({4})", (index.Unique ? "UNIQUE " : ""), (index.Clustered ? "CLUSTERED" : "NONCLUSTERED"), name, table, string.Join(", ", columns)));
			}
		}

		public override void ChangeColumn(string table, Column column)
		{
			if (column.DefaultValue == null || column.DefaultValue == DBNull.Value)
			{
				base.ChangeColumn(table, column);
			}
			else
			{
				var def = column.DefaultValue;
				var notNull = column.ColumnProperty.IsSet(ColumnProperty.NotNull);
				column.DefaultValue = null;
				column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.Null);
				column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.NotNull);

				base.ChangeColumn(table, column);

				ColumnPropertiesMapper mapper = _dialect.GetAndMapColumnPropertiesWithoutDefault(column);
				ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} {2} FOR {3}", this.QuoteTableNameIfRequired(table), "DF_" + table + "_" + column.Name, _dialect.Default(def), this.QuoteColumnNameIfRequired(column.Name)));

				if (notNull)
				{
					column.ColumnProperty = column.ColumnProperty.Set(ColumnProperty.NotNull);
					column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Null);
					base.ChangeColumn(table, column);
				}
			}
		}

		//public override bool ColumnExists(string table, string column)
		//{
		//	string schema;
		//	if (!TableExists(table))
		//	{
		//		return false;
		//	}
		//	int firstIndex = table.IndexOf(".");
		//	if (firstIndex >= 0)
		//	{
		//		schema = table.Substring(0, firstIndex);
		//		table = table.Substring(firstIndex + 1);
		//	}
		//	else
		//	{
		//		schema = _defaultSchema;
		//	}
		//	using (var cmd = CreateCommand())
		//	using (
		//		IDataReader reader = base.ExecuteQuery(cmd, string.Format("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{0}' AND TABLE_NAME='{1}' AND COLUMN_NAME='{2}'", schema, table, column)))
		//	{
		//		return reader.Read();
		//	}
		//}

		public override void RemoveColumnDefaultValue(string table, string column)
		{
			var sql = string.Format("SELECT Name FROM SYS.DEFAULT_CONSTRAINTS WHERE PARENT_OBJECT_ID = OBJECT_ID('{0}') AND PARENT_COLUMN_ID = (SELECT column_id FROM sys.columns WHERE NAME = '{1}' AND object_id = OBJECT_ID('{0}'))", table, column);
			var constraintName = ExecuteScalar(sql);
			if (constraintName != null)
				RemoveConstraint(table, constraintName.ToString());
		}


		//public override bool TableExists(string table)
		//{
		//	string schema;

		//	int firstIndex = table.IndexOf(".");
		//	if (firstIndex >= 0)
		//	{
		//		schema = table.Substring(0, firstIndex);
		//		table = table.Substring(firstIndex + 1);
		//	}
		//	else
		//	{
		//		schema = _defaultSchema;
		//	}

		//	using (var cmd = CreateCommand())
		//	using (IDataReader reader = base.ExecuteQuery(cmd, string.Format("SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{0}' AND TABLE_SCHEMA='{1}'", table, schema)))
		//	{
		//		return reader.Read();
		//	}
		//}

		public override Index[] GetIndexes(string table)
		{
			var retVal = new List<Index>();

			var sql = @"SELECT  Tab.[name] AS TableName,
                        Ind.[name] AS IndexName,
                        Ind.[type_desc] AS IndexType,
                        Ind.[is_primary_key] AS IndexPrimary,
                        Ind.[is_unique] AS IndexUnique,
                        Ind.[is_unique_constraint] AS ConstraintUnique,
                        SUBSTRING(( SELECT  ',' + AC.name
                    FROM    sys.[tables] AS T
                            INNER JOIN sys.[indexes] I ON T.[object_id] = I.[object_id]
                            INNER JOIN sys.[index_columns] IC ON I.[object_id] = IC.[object_id]
                                                                 AND I.[index_id] = IC.[index_id]
                            INNER JOIN sys.[all_columns] AC ON T.[object_id] = AC.[object_id]
                                                               AND IC.[column_id] = AC.[column_id]
                    WHERE   Ind.[object_id] = I.[object_id]
                            AND Ind.index_id = I.index_id
                            AND IC.is_included_column = 0
                    ORDER BY IC.key_ordinal
                  FOR
                    XML PATH('') ), 2, 8000) AS KeyCols,
        SUBSTRING(( SELECT  ',' + AC.name
                    FROM    sys.[tables] AS T
                            INNER JOIN sys.[indexes] I ON T.[object_id] = I.[object_id]
                            INNER JOIN sys.[index_columns] IC ON I.[object_id] = IC.[object_id]
                                                                 AND I.[index_id] = IC.[index_id]
                            INNER JOIN sys.[all_columns] AC ON T.[object_id] = AC.[object_id]
                                                               AND IC.[column_id] = AC.[column_id]
                    WHERE   Ind.[object_id] = I.[object_id]
                            AND Ind.index_id = I.index_id
                            AND IC.is_included_column = 1
                    ORDER BY IC.key_ordinal
                  FOR
                    XML PATH('') ), 2, 8000) AS IncludeCols
FROM    sys.[indexes] Ind
        INNER JOIN sys.[tables] AS Tab ON Tab.[object_id] = Ind.[object_id]
        WHERE LOWER(Tab.[name]) = LOWER('{0}')";

			using (var cmd = CreateCommand())
			using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
			{
				while (reader.Read())
				{
					if (!reader.IsDBNull(1))
					{
						var idx = new Index
						{
							Name = reader.GetString(1),
							Clustered = reader.GetString(2) == "CLUSTERED",
							PrimaryKey = reader.GetBoolean(3),
							Unique = reader.GetBoolean(4),
							UniqueConstraint = reader.GetBoolean(5),
						};
						if (!reader.IsDBNull(6)) idx.KeyColumns = (reader.GetString(6).Split(','));
						if (!reader.IsDBNull(7)) idx.IncludeColumns = (reader.GetString(7).Split(','));
						retVal.Add(idx);
					}
				}
			}

			return retVal.ToArray();
		}

		public override Column[] GetColumns(string table)
		{
			string schema;

			int firstIndex = table.IndexOf(".");
			if (firstIndex >= 0)
			{
				schema = table.Substring(0, firstIndex);
				table = table.Substring(firstIndex + 1);
			}
			else
			{
				schema = _defaultSchema;
			}

			var pkColumns = new List<string>();
			try
			{
				pkColumns = this.ExecuteStringQuery("SELECT cu.COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE cu WHERE EXISTS ( SELECT tc.* FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc WHERE tc.TABLE_NAME = '{0}' AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.CONSTRAINT_NAME = cu.CONSTRAINT_NAME )", table);
			}
			catch (Exception)
			{ }

			var idtColumns = new List<string>();
			try
			{
				idtColumns = this.ExecuteStringQuery(" select COLUMN_NAME from INFORMATION_SCHEMA.COLUMNS where TABLE_SCHEMA = '{1}' and TABLE_NAME = '{0}' and COLUMNPROPERTY(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1", table, schema);
			}
			catch (Exception)
			{ }

			var columns = new List<Column>();
			using (var cmd = CreateCommand())
			using (
					IDataReader reader =
					ExecuteQuery(cmd,
						String.Format("select COLUMN_NAME, IS_NULLABLE, DATA_TYPE, ISNULL(CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION), COLUMN_DEFAULT, NUMERIC_SCALE from INFORMATION_SCHEMA.COLUMNS where table_name = '{0}'", table)))
			{
				while (reader.Read())
				{
					var column = new Column(reader.GetString(0), DbType.String);

					if (pkColumns.Contains(column.Name))
						column.ColumnProperty |= ColumnProperty.PrimaryKey;

					if (idtColumns.Contains(column.Name))
						column.ColumnProperty |= ColumnProperty.Identity;

					string nullableStr = reader.GetString(1);
					bool isNullable = nullableStr == "YES";
					if (!reader.IsDBNull(2))
					{
						string type = reader.GetString(2);
						column.Type = Dialect.GetDbTypeFromString(type);
					}
					if (!reader.IsDBNull(3))
					{
						column.Size = reader.GetInt32(3);
					}
					if (!reader.IsDBNull(4))
					{
						column.DefaultValue = reader.GetValue(4);

						if (column.DefaultValue.ToString()[1] == '(' || column.DefaultValue.ToString()[1] == '\'')
							column.DefaultValue = column.DefaultValue.ToString().Substring(2, column.DefaultValue.ToString().Length - 4); // Example "((10))" or "('false')"
						else
							column.DefaultValue = column.DefaultValue.ToString().Substring(1, column.DefaultValue.ToString().Length - 2); // Example "(CONVERT([datetime],'20000101',(112)))"

						if (column.Type == DbType.Int16 || column.Type == DbType.Int32 || column.Type == DbType.Int64)
							column.DefaultValue = Int64.Parse(column.DefaultValue.ToString());

						if (column.Type == DbType.UInt16 || column.Type == DbType.UInt32 || column.Type == DbType.UInt64)
							column.DefaultValue = UInt64.Parse(column.DefaultValue.ToString());

						if (column.Type == DbType.Double || column.Type == DbType.Single)
							column.DefaultValue = double.Parse(column.DefaultValue.ToString());
					}
					if (!reader.IsDBNull(5))
					{
						if (column.Type == DbType.Decimal)
						{
							column.Size = reader.GetInt32(5);
						}
					}

					column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

					columns.Add(column);
				}
			}

			return columns.ToArray();
		}

		public override List<string> GetDatabases()
		{
			return ExecuteStringQuery("SELECT name FROM sys.databases");
		}

		public override void KillDatabaseConnections(string databaseName)
		{
			ExecuteNonQuery(string.Format(
				"USE [master]" + System.Environment.NewLine +
				"ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", databaseName));
		}

		public override void DropDatabases(string databaseName)
		{
			ExecuteNonQuery(string.Format("USE [master]" + System.Environment.NewLine + "DROP DATABASE {0}", databaseName));
		}

		public override void RemoveColumn(string table, string column)
		{
			DeleteColumnConstraints(table, column);
			DeleteColumnIndexes(table, column);
			RemoveColumnDefaultValue(table, column);
			base.RemoveColumn(table, column);
		}

		public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
		{
			if (ColumnExists(tableName, newColumnName))
				throw new MigrationException(String.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));

			if (ColumnExists(tableName, oldColumnName))
				ExecuteNonQuery(String.Format("EXEC sp_rename '{0}.{1}', '{2}', 'COLUMN'", tableName, oldColumnName, newColumnName));
		}

		public override void RenameTable(string oldName, string newName)
		{
			if (TableExists(newName))
			{
				throw new MigrationException(String.Format("Table with name '{0}' already exists", newName));
			}

			if (!TableExists(oldName))
			{
				throw new MigrationException(String.Format("Table with name '{0}' does not exist to rename", oldName));
			}

			ExecuteNonQuery(String.Format("EXEC sp_rename '{0}', '{1}'", oldName, newName));
		}

		// Deletes all constraints linked to a column. Sql Server
		// doesn't seems to do this.
		void DeleteColumnConstraints(string table, string column)
		{
			string sqlContrainte = FindConstraints(table, column);
			var constraints = new List<string>();
			using (var cmd = CreateCommand())
			using (IDataReader reader = ExecuteQuery(cmd, sqlContrainte))
			{
				while (reader.Read())
				{
					constraints.Add(reader.GetString(0));
				}
			}
			// Can't share the connection so two phase modif
			foreach (string constraint in constraints)
			{
				RemoveForeignKey(table, constraint);
			}
		}

		void DeleteColumnIndexes(string table, string column)
		{
			string sqlIndex = this.FindIndexes(table, column);
			var indexes = new List<string>();
			using (var cmd = CreateCommand())
			using (IDataReader reader = ExecuteQuery(cmd, sqlIndex))
			{
				while (reader.Read())
				{
					indexes.Add(reader.GetString(0));
				}
			}
			// Can't share the connection so two phase modif
			foreach (string index in indexes)
			{
				this.RemoveIndex(table, index);
			}
		}

		protected virtual string FindIndexes(string table, string column)
		{
			return string.Format(@"
select
    i.name as IndexName
from sys.indexes i
join sys.objects o on i.object_id = o.object_id
join sys.index_columns ic on ic.object_id = i.object_id
    and ic.index_id = i.index_id
join sys.columns co on co.object_id = i.object_id
    and co.column_id = ic.column_id
where (select count(*) from sys.index_columns ic1 where ic1.object_id = i.object_id and ic1.index_id = i.index_id) = 1
and o.[Name] = '{0}'
and co.[Name] = '{1}'",
					table, column);
		}

		// FIXME: We should look into implementing this with INFORMATION_SCHEMA if possible
		// so that it would be usable by all the SQL Server implementations
		protected virtual string FindConstraints(string table, string column)
		{
			return string.Format(@"SELECT DISTINCT CU.CONSTRAINT_NAME FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE CU
INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
ON CU.CONSTRAINT_NAME = TC.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'FOREIGN KEY'
AND CU.TABLE_NAME = '{0}'
AND CU.COLUMN_NAME = '{1}'",
					table, column);
		}

		public override bool IndexExists(string table, string name)
		{
			using (var cmd = CreateCommand())
			using (IDataReader reader =
				ExecuteQuery(cmd, string.Format("SELECT top 1 * FROM sys.indexes WHERE object_id = OBJECT_ID('{0}') AND name = '{1}'", table, name)))
			{
				return reader.Read();
			}
		}

		public override void RemoveIndex(string table, string name)
		{
			if (TableExists(table) && IndexExists(table, name))
			{
				ExecuteNonQuery(String.Format("DROP INDEX {0} ON {1}", QuoteConstraintNameIfRequired(name), QuoteTableNameIfRequired(table)));
			}
		}

		protected override string GetPrimaryKeyConstraintName(string table)
		{
			using (var cmd = CreateCommand())
			using (IDataReader reader =
				ExecuteQuery(cmd, string.Format("SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('{0}') AND is_primary_key = 1", table)))
			{
				return reader.Read() ? reader.GetString(0) : null;
			}
		}

		protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
		{
			if (value is UInt16)
			{
				parameter.DbType = DbType.Int32;
				parameter.Value = value;
			}
			else if (value is UInt32)
			{
				parameter.DbType = DbType.Int64;
				parameter.Value = value;
			}
			else if (value is UInt64)
			{
				parameter.DbType = DbType.Decimal;
				parameter.Value = value;
			}
			else
			{
				base.ConfigureParameterWithValue(parameter, index, value);
			}
		}

		public override string Concatenate(params string[] strings)
		{
			return string.Join(" + ", strings);
		}
	}
}
