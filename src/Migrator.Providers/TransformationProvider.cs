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
using System.Linq;
using System.Text;
using Migrator.Framework;
using Migrator.Framework.Loggers;
using Migrator.Framework.SchemaBuilder;
using ForeignKeyConstraint = Migrator.Framework.ForeignKeyConstraint;

namespace Migrator.Providers
{
	/// <summary>
	/// Base class for every transformation providers.
	/// A 'tranformation' is an operation that modifies the database.
	/// </summary>
	public abstract class TransformationProvider : ITransformationProvider
	{
		protected readonly string _connectionString;
		protected readonly string _defaultSchema;
		readonly ForeignKeyConstraintMapper constraintMapper = new ForeignKeyConstraintMapper();
		List<long> _appliedMigrations;
		protected IDbConnection _connection;
		protected Dialect _dialect;
		ILogger _logger;
		IDbTransaction _transaction;

		protected TransformationProvider(Dialect dialect, string connectionString, string defaultSchema)
		{
			_dialect = dialect;
			_connectionString = connectionString;
			_defaultSchema = defaultSchema;
			_logger = new Logger(false);
		}

		public Dialect Dialect
		{
			get { return _dialect; }
		}

		/// <summary>
		/// Returns the event logger
		/// </summary>
		public virtual ILogger Logger
		{
			get { return _logger; }
			set { _logger = value; }
		}

		public virtual ITransformationProvider this[string provider]
		{
			get
			{
				if (null != provider && IsThisProvider(provider))
					return this;

				return NoOpTransformationProvider.Instance;
			}
		}

		public virtual Column[] GetColumns(string table)
		{
			var columns = new List<Column>();
			using (
				IDataReader reader =
					ExecuteQuery(
						String.Format("select COLUMN_NAME, IS_NULLABLE from INFORMATION_SCHEMA.COLUMNS where table_name = '{0}'", table)))
			{
				while (reader.Read())
				{
					var column = new Column(reader.GetString(0), DbType.String);
					string nullableStr = reader.GetString(1);
					bool isNullable = nullableStr == "YES";
					column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

					columns.Add(column);
				}
			}

			return columns.ToArray();
		}

		public virtual Column GetColumnByName(string table, string columnName)
		{
			var columns = GetColumns(table);
			return columns.First(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
		}

		public virtual string[] GetTables()
		{
			var tables = new List<string>();
			using (IDataReader reader = ExecuteQuery("SELECT table_name FROM INFORMATION_SCHEMA.TABLES"))
			{
				while (reader.Read())
				{
					tables.Add((string) reader[0]);
				}
			}
			return tables.ToArray();
		}

		public virtual void RemoveForeignKey(string table, string name)
		{
			RemoveConstraint(table, name);
		}

		public virtual void RemoveConstraint(string table, string name)
		{
			if (TableExists(table) && ConstraintExists(table, name))
			{
				table = _dialect.TableNameNeedsQuote ? _dialect.Quote(table) : table;
				name = _dialect.ConstraintNameNeedsQuote ? _dialect.Quote(name) : name;
				ExecuteNonQuery(String.Format("ALTER TABLE {0} DROP CONSTRAINT {1}", table, name));
			}
		}

		/// <summary>
		/// Add a new table
		/// </summary>
		/// <param name="name">Table name</param>
		/// <param name="columns">Columns</param>
		/// <example>
		/// Adds the Test table with two columns:
		/// <code>
		/// Database.AddTable("Test",
		///	                  new Column("Id", typeof(int), ColumnProperty.PrimaryKey),
		///	                  new Column("Title", typeof(string), 100)
		///	                 );
		/// </code>
		/// </example>
		public virtual void AddTable(string name, params Column[] columns)
		{
			// Most databases don't have the concept of a storage engine, so default is to not use it.
			AddTable(name, null, columns);
		}

		/// <summary>
		/// Add a new table
		/// </summary>
		/// <param name="name">Table name</param>
		/// <param name="columns">Columns</param>
		/// <param name="engine">the database storage engine to use</param>
		/// <example>
		/// Adds the Test table with two columns:
		/// <code>
		/// Database.AddTable("Test", "INNODB",
		///	                  new Column("Id", typeof(int), ColumnProperty.PrimaryKey),
		///	                  new Column("Title", typeof(string), 100)
		///	                 );
		/// </code>
		/// </example>
		public virtual void AddTable(string name, string engine, params Column[] columns)
		{
			if (TableExists(name))
			{
				Logger.Warn("Table {0} already exists", name);
				return;
			}

			List<string> pks = GetPrimaryKeys(columns);
			bool compoundPrimaryKey = pks.Count > 1;

			var columnProviders = new List<ColumnPropertiesMapper>(columns.Length);
			foreach (Column column in columns)
			{
				// Remove the primary key notation if compound primary key because we'll add it back later
				if (compoundPrimaryKey && column.IsPrimaryKey)
				{
					column.ColumnProperty = column.ColumnProperty ^ ColumnProperty.PrimaryKey; 
					column.ColumnProperty = column.ColumnProperty | ColumnProperty.NotNull; // PK is always not-null
				}

				ColumnPropertiesMapper mapper = _dialect.GetAndMapColumnProperties(column);
				columnProviders.Add(mapper);
			}

			string columnsAndIndexes = JoinColumnsAndIndexes(columnProviders);
			AddTable(name, engine, columnsAndIndexes);

			if (compoundPrimaryKey)
			{
				AddPrimaryKey(String.Format("PK_{0}", name), name, pks.ToArray());
			}
		}

		public virtual void RemoveTable(string name)
		{
		    if (!TableExists(name))
			{
                throw new MigrationException(String.Format("Table with name '{0}' does not exist to rename", name));
			}
			
            ExecuteNonQuery(String.Format("DROP TABLE {0}", name));
		}

		public virtual void RenameTable(string oldName, string newName)
		{
		    oldName = QuoteTableNameIfRequired(oldName);
		    newName = QuoteTableNameIfRequired(newName);

			if (TableExists(newName))
			{
			    throw new MigrationException(String.Format("Table with name '{0}' already exists", newName));
			}

			if (!TableExists(oldName))
			{
                throw new MigrationException(String.Format("Table with name '{0}' does not exist to rename", oldName));
			}

			ExecuteNonQuery(String.Format("ALTER TABLE {0} RENAME TO {1}", oldName, newName));
		}

		public virtual void RenameColumn(string tableName, string oldColumnName, string newColumnName)
		{
		    if (ColumnExists(tableName, newColumnName))
			{
			    throw new MigrationException(String.Format("Table '{0}' has column named '{1}' already", tableName, newColumnName));
			}

            if (!ColumnExists(tableName, oldColumnName))
		    {
		        throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", tableName, oldColumnName));
		    }
		    
            var column = GetColumnByName(tableName, oldColumnName);

		    var quotedNewColumnName = QuoteColumnNameIfRequired(newColumnName);

            ExecuteNonQuery(String.Format("ALTER TABLE {0} RENAME COLUMN {1} TO {2}", tableName, Dialect.Quote(column.Name), quotedNewColumnName));
		}

		public virtual void RemoveColumn(string table, string column)
		{
            if (!ColumnExists(table, column))
            {
                throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", table, column));                
            }

		    var existingColumn = GetColumnByName(table, column);

		    ExecuteNonQuery(String.Format("ALTER TABLE {0} DROP COLUMN {1} ", table, Dialect.Quote(existingColumn.Name)));
		}

		public virtual bool ColumnExists(string table, string column)
		{
			try
			{
				ExecuteNonQuery(String.Format("SELECT {0} FROM {1}", column, table));
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public virtual void ChangeColumn(string table, Column column)
		{
			if (!ColumnExists(table, column.Name))
			{
				Logger.Warn("Column {0}.{1} does not exist", table, column.Name);
				return;
			}

			ColumnPropertiesMapper mapper = _dialect.GetAndMapColumnProperties(column);

			ChangeColumn(table, mapper.ColumnSql);
		}

		public virtual bool TableExists(string table)
		{
			try
			{
				ExecuteNonQuery("SELECT COUNT(*) FROM " + table);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Add a new column to an existing table.
		/// </summary>
		/// <param name="table">Table to which to add the column</param>
		/// <param name="column">Column name</param>
		/// <param name="type">Date type of the column</param>
		/// <param name="size">Max length of the column</param>
		/// <param name="property">Properties of the column, see <see cref="ColumnProperty">ColumnProperty</see>,</param>
		/// <param name="defaultValue">Default value</param>
		public virtual void AddColumn(string table, string column, DbType type, int size, ColumnProperty property,
		                              object defaultValue)
		{
			if (ColumnExists(table, column))
			{
				Logger.Warn("Column {0}.{1} already exists", table, column);
				return;
			}

			ColumnPropertiesMapper mapper =
				_dialect.GetAndMapColumnProperties(new Column(column, type, size, property, defaultValue));

			AddColumn(table, mapper.ColumnSql);
		}

		/// <summary>
		/// <see cref="TransformationProvider.AddColumn(string, string, DbType, int, ColumnProperty, object)">
		/// AddColumn(string, string, Type, int, ColumnProperty, object)
		/// </see>
		/// </summary>
		public virtual void AddColumn(string table, string column, DbType type)
		{
			AddColumn(table, column, type, 0, ColumnProperty.Null, null);
		}

		/// <summary>
		/// <see cref="TransformationProvider.AddColumn(string, string, DbType, int, ColumnProperty, object)">
		/// AddColumn(string, string, Type, int, ColumnProperty, object)
		/// </see>
		/// </summary>
		public virtual void AddColumn(string table, string column, DbType type, int size)
		{
			AddColumn(table, column, type, size, ColumnProperty.Null, null);
		}

		public virtual void AddColumn(string table, string column, DbType type, object defaultValue)
		{
			if (ColumnExists(table, column))
			{
				Logger.Warn("Column {0}.{1} already exists", table, column);
				return;
			}

			ColumnPropertiesMapper mapper =
				_dialect.GetAndMapColumnProperties(new Column(column, type, defaultValue));

			AddColumn(table, mapper.ColumnSql);
		}

		/// <summary>
		/// <see cref="TransformationProvider.AddColumn(string, string, DbType, int, ColumnProperty, object)">
		/// AddColumn(string, string, Type, int, ColumnProperty, object)
		/// </see>
		/// </summary>
		public virtual void AddColumn(string table, string column, DbType type, ColumnProperty property)
		{
			AddColumn(table, column, type, 0, property, null);
		}

		/// <summary>
		/// <see cref="TransformationProvider.AddColumn(string, string, DbType, int, ColumnProperty, object)">
		/// AddColumn(string, string, Type, int, ColumnProperty, object)
		/// </see>
		/// </summary>
		public virtual void AddColumn(string table, string column, DbType type, int size, ColumnProperty property)
		{
			AddColumn(table, column, type, size, property, null);
		}

		/// <summary>
		/// Append a primary key to a table.
		/// </summary>
		/// <param name="name">Constraint name</param>
		/// <param name="table">Table name</param>
		/// <param name="columns">Primary column names</param>
		public virtual void AddPrimaryKey(string name, string table, params string[] columns)
		{
			if (ConstraintExists(table, name))
			{
				Logger.Warn("Primary key {0} already exists", name);
				return;
			}
			ExecuteNonQuery(
				String.Format("ALTER TABLE {0} ADD CONSTRAINT {1} PRIMARY KEY ({2}) ", table, name,
				              String.Join(",", columns)));
		}

		public virtual void AddUniqueConstraint(string name, string table, params string[] columns)
		{
			if (ConstraintExists(table, name))
			{
				Logger.Warn("Constraint {0} already exists", name);
				return;
			}

			QuoteColumnNames(columns);

			table = QuoteTableNameIfRequired(table);

			ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD CONSTRAINT {1} UNIQUE({2}) ", table, name, string.Join(", ", columns)));
		}

		public virtual void AddCheckConstraint(string name, string table, string checkSql)
		{
			if (ConstraintExists(table, name))
			{
				Logger.Warn("Constraint {0} already exists", name);
				return;
			}

			table = QuoteTableNameIfRequired(table);

			ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD CONSTRAINT {1} CHECK ({2}) ", table, name, checkSql));
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string primaryColumn, string refTable, string refColumn)
		{
			AddForeignKey("FK_" + primaryTable + "_" + refTable, primaryTable, primaryColumn, refTable, refColumn);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </see>
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string[] primaryColumns, string refTable,
		                                       string[] refColumns)
		{
			AddForeignKey("FK_" + primaryTable + "_" + refTable, primaryTable, primaryColumns, refTable, refColumns);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string primaryColumn, string refTable,
		                                       string refColumn, ForeignKeyConstraint constraint)
		{
			AddForeignKey("FK_" + primaryTable + "_" + refTable, primaryTable, primaryColumn, refTable, refColumn,
			              constraint);
		}

		/// <summary>
		/// Guesses the name of the foreign key and add it
		/// </see>
		/// </summary>
		public virtual void GenerateForeignKey(string primaryTable, string[] primaryColumns, string refTable,
		                                       string[] refColumns, ForeignKeyConstraint constraint)
		{
			AddForeignKey("FK_" + primaryTable + "_" + refTable, primaryTable, primaryColumns, refTable, refColumns,
			              constraint);
		}

		/// <summary>
		/// Append a foreign key (relation) between two tables.
		/// tables.
		/// </summary>
		/// <param name="name">Constraint name</param>
		/// <param name="primaryTable">Table name containing the primary key</param>
		/// <param name="primaryColumn">Primary key column name</param>
		/// <param name="refTable">Foreign table name</param>
		/// <param name="refColumn">Foreign column name</param>
		public virtual void AddForeignKey(string name, string primaryTable, string primaryColumn, string refTable,
		                                  string refColumn)
		{
			try
			{
				AddForeignKey(name, primaryTable, new[] {primaryColumn}, refTable, new[] {refColumn});
			}
			catch (Exception ex)
			{
				throw new Exception(string.Format("Error occured while adding foreign key: \"{0}\" between table: \"{1}\" and table: \"{2}\" - see inner exception for details", name, primaryTable, refTable), ex);
			}
		}

		/// <summary>
		/// <see cref="ITransformationProvider.AddForeignKey(string, string, string, string, string)">
		/// AddForeignKey(string, string, string, string, string)
		/// </see>
		/// </summary>
		public virtual void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable, string[] refColumns)
		{
			AddForeignKey(name, primaryTable, primaryColumns, refTable, refColumns, ForeignKeyConstraint.NoAction);
		}

		public virtual void AddForeignKey(string name, string primaryTable, string primaryColumn, string refTable, string refColumn, ForeignKeyConstraint constraint)
		{
			AddForeignKey(name, primaryTable, new[] {primaryColumn}, refTable, new[] {refColumn},
			              constraint);
		}

		public virtual void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable,
		                                  string[] refColumns, ForeignKeyConstraint constraint)
		{
			if (ConstraintExists(primaryTable, name))
			{
				Logger.Warn("Constraint {0} already exists", name);
				return;
			}

			refTable = QuoteTableNameIfRequired(refTable);
			primaryTable = QuoteTableNameIfRequired(primaryTable);
			QuoteColumnNames(primaryColumns);
			QuoteColumnNames(refColumns);

			string constraintResolved = constraintMapper.SqlForConstraint(constraint);

			ExecuteNonQuery(
				String.Format(
					"ALTER TABLE {0} ADD CONSTRAINT {1} FOREIGN KEY ({2}) REFERENCES {3} ({4}) ON UPDATE {5} ON DELETE {6}",
					primaryTable, name, String.Join(",", primaryColumns),
					refTable, String.Join(",", refColumns), constraintResolved, constraintResolved));
		}

		/// <summary>
		/// Determines if a constraint exists.
		/// </summary>
		/// <param name="name">Constraint name</param>
		/// <param name="table">Table owning the constraint</param>
		/// <returns><c>true</c> if the constraint exists.</returns>
		public abstract bool ConstraintExists(string table, string name);

		public virtual bool PrimaryKeyExists(string table, string name)
		{
			return ConstraintExists(table, name);
		}

		public virtual int ExecuteNonQuery(string sql)
		{
			Logger.Trace(sql);
			Logger.ApplyingDBChange(sql);
			using (IDbCommand cmd = BuildCommand(sql))
			{
				try
				{
					return cmd.ExecuteNonQuery();
				}
				catch (Exception ex)
				{
					Logger.Warn(ex.Message);
					throw new Exception(string.Format("Error occured executing sql: {0}, see inner exception for details, error: " + ex, sql), ex);
				}
			}
		}

		/// <summary>
		/// Execute an SQL query returning results.
		/// </summary>
		/// <param name="sql">The SQL command.</param>
		/// <returns>A data iterator, <see cref="System.Data.IDataReader">IDataReader</see>.</returns>
		public virtual IDataReader ExecuteQuery(string sql)
		{
			Logger.Trace(sql);
			using (IDbCommand cmd = BuildCommand(sql))
			{
				try
				{
					return cmd.ExecuteReader();
				}
				catch (Exception ex)
				{
					Logger.Warn("query failed: {0}", cmd.CommandText);
					throw new Exception("Failed to execute sql statement: " + sql, ex);
				}
			}
		}

		public virtual object ExecuteScalar(string sql)
		{
			Logger.Trace(sql);
			using (IDbCommand cmd = BuildCommand(sql))
			{
				try
				{
					return cmd.ExecuteScalar();
				}
				catch
				{
					Logger.Warn("Query failed: {0}", cmd.CommandText);
					throw;
				}
			}
		}

		public virtual IDataReader Select(string what, string from)
		{
			return Select(what, from, "1=1");
		}

		public virtual IDataReader Select(string what, string from, string where)
		{
			return ExecuteQuery(String.Format("SELECT {0} FROM {1} WHERE {2}", what, from, where));
		}

		public object SelectScalar(string what, string from)
		{
			return SelectScalar(what, from, "1=1");
		}

		public virtual object SelectScalar(string what, string from, string where)
		{
			return ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2}", what, from, where));
		}

		public virtual int Update(string table, string[] columns, string[] values)
		{
			return Update(table, columns, values, null);
		}

		public virtual int Update(string table, string[] columns, string[] values, string where)
		{
			string namesAndValues = JoinColumnsAndValues(columns, values);

			string query = "UPDATE {0} SET {1}";
			if (!String.IsNullOrEmpty(where))
			{
				query += " WHERE " + where;
			}

			return ExecuteNonQuery(String.Format(query, table, namesAndValues));
		}

		public virtual int Insert(string table, string[] columns, object[] values)
		{
			if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("table");
			if (columns == null) throw new ArgumentNullException("columns");
			if (values == null) throw new ArgumentNullException("values");
			if (columns.Length != values.Length) throw new Exception(string.Format("The number of columns: {0} does not match the number of supplied values: {1}", columns.Length, values.Length));

			table = QuoteTableNameIfRequired(table);

			string columnNames = string.Join(", ", columns.Select(col => QuoteColumnNameIfRequired(col)).ToArray());

			var builder = new StringBuilder();

			for (int i = 0; i < values.Length; i++)
			{
				if (builder.Length > 0) builder.Append(", ");
				builder.Append(GenerateParameterName(i));
			}

			string parameterNames = builder.ToString();

			using (IDbCommand command = _connection.CreateCommand())
			{
				command.Transaction = _transaction;

				command.CommandText = String.Format("INSERT INTO {0} ({1}) VALUES ({2})", table, columnNames, parameterNames);
				command.CommandType = CommandType.Text;

				int paramCount = 0;

				foreach (object value in values)
				{
					IDbDataParameter parameter = command.CreateParameter();

					ConfigureParameterWithValue(parameter, paramCount, value);

					parameter.ParameterName = GenerateParameterName(paramCount);

					command.Parameters.Add(parameter);

					paramCount++;
				}

				return command.ExecuteNonQuery();
			}
		}

		public virtual int Delete(string table, string[] columns, string[] values)
		{
			if (null == columns || null == values)
			{
				return ExecuteNonQuery(String.Format("DELETE FROM {0}", table));
			}
			else
			{
				return ExecuteNonQuery(String.Format("DELETE FROM {0} WHERE ({1})", table, JoinColumnsAndValues(columns, values)));
			}
		}

		public virtual int Delete(string table, string wherecolumn, string wherevalue)
		{
			if (string.IsNullOrEmpty(wherecolumn) && string.IsNullOrEmpty(wherevalue))
			{
				return Delete(table, (string[]) null, null);
			}

			return ExecuteNonQuery(String.Format("DELETE FROM {0} WHERE {1} = {2}", table, wherecolumn, QuoteValues(wherevalue)));
		}

		/// <summary>
		/// Starts a transaction. Called by the migration mediator.
		/// </summary>
		public virtual void BeginTransaction()
		{
			if (_transaction == null && _connection != null)
			{
				EnsureHasConnection();
				_transaction = _connection.BeginTransaction(IsolationLevel.ReadCommitted);
			}
		}

		/// <summary>
		/// Rollback the current migration. Called by the migration mediator.
		/// </summary>
		public virtual void Rollback()
		{
			if (_transaction != null && _connection != null && _connection.State == ConnectionState.Open)
			{
				try
				{
					_transaction.Rollback();
				}
				finally
				{
					_connection.Close();
				}
			}
			_transaction = null;
		}

		/// <summary>
		/// Commit the current transaction. Called by the migrations mediator.
		/// </summary>
		public virtual void Commit()
		{
			if (_transaction != null && _connection != null && _connection.State == ConnectionState.Open)
			{
				try
				{
					_transaction.Commit();
				}
				finally
				{
					_connection.Close();
				}
			}
			_transaction = null;
		}

		/// <summary>
		/// The list of Migrations currently applied to the database.
		/// </summary>
		public virtual List<long> AppliedMigrations
		{
			get
			{
				if (_appliedMigrations == null)
				{
					_appliedMigrations = new List<long>();
					CreateSchemaInfoTable();

					string versionColumn = "Version";

					versionColumn = QuoteColumnNameIfRequired(versionColumn);

					using (IDataReader reader = Select(versionColumn, "SchemaInfo"))
					{
						while (reader.Read())
						{
							if (reader.GetFieldType(0) == typeof (Decimal))
							{
								_appliedMigrations.Add((long) reader.GetDecimal(0));
							}
							else
							{
								_appliedMigrations.Add(reader.GetInt64(0));
							}
						}
					}
				}
				return _appliedMigrations;
			}
		}

		/// <summary>
		/// Marks a Migration version number as having been applied
		/// </summary>
		/// <param name="version">The version number of the migration that was applied</param>
		public void MigrationApplied(long version)
		{
			CreateSchemaInfoTable();
			Insert("SchemaInfo", new string[] { "Version" }, new object[] { version });
			_appliedMigrations.Add(version);
		}

		/// <summary>
		/// Marks a Migration version number as having been rolled back from the database
		/// </summary>
		/// <param name="version">The version number of the migration that was removed</param>
		public void MigrationUnApplied(long version)
		{
			CreateSchemaInfoTable();
			Delete("SchemaInfo", "Version", version.ToString());
			_appliedMigrations.Remove(version);
		}

		public virtual void AddColumn(string table, Column column)
		{
			AddColumn(table, column.Name, column.Type, column.Size, column.ColumnProperty, column.DefaultValue);
		}

		public virtual void GenerateForeignKey(string primaryTable, string refTable)
		{
			GenerateForeignKey(primaryTable, refTable, ForeignKeyConstraint.NoAction);
		}

		public virtual void GenerateForeignKey(string primaryTable, string refTable, ForeignKeyConstraint constraint)
		{
			GenerateForeignKey(primaryTable, refTable + "Id", refTable, "Id", constraint);
		}

		public virtual IDbCommand GetCommand()
		{
			return BuildCommand(null);
		}

		public virtual void ExecuteSchemaBuilder(SchemaBuilder builder)
		{
			foreach (ISchemaBuilderExpression expr in builder.Expressions)
				expr.Create(this);
		}

		public void Dispose()
		{
			if (_connection != null && _connection.State == ConnectionState.Open)
			{
				_connection.Close();
			}
		}

		public virtual string QuoteColumnNameIfRequired(string name)
		{
			if (Dialect.ColumnNameNeedsQuote || Dialect.IsReservedWord(name))
			{
				return Dialect.Quote(name);
			}
			return name;
		}

		public virtual string QuoteTableNameIfRequired(string name)
		{
			if (Dialect.TableNameNeedsQuote || Dialect.IsReservedWord(name))
			{
				return Dialect.Quote(name);
			}
			return name;
		}

		public virtual string Encode(Guid guid)
		{
			return guid.ToString();
		}

		public virtual string[] QuoteColumnNamesIfRequired(params string[] columnNames)
		{
			var quotedColumns = new string[columnNames.Length];

			for (int i = 0; i < columnNames.Length; i++)
			{
				quotedColumns[i] = QuoteColumnNameIfRequired(columnNames[i]);
			}

			return quotedColumns;
		}

		public virtual bool IsThisProvider(string provider)
		{
			// XXX: This might need to be more sophisticated. Currently just a convention
			return GetType().Name.ToLower().StartsWith(provider.ToLower());
		}

		public virtual void AddTable(string table, string engine, string columns)
		{
			table = _dialect.TableNameNeedsQuote ? _dialect.Quote(table) : table;
			string sqlCreate = String.Format("CREATE TABLE {0} ({1})", table, columns);
			ExecuteNonQuery(sqlCreate);
		}

		public virtual List<string> GetPrimaryKeys(IEnumerable<Column> columns)
		{
			var pks = new List<string>();
			foreach (Column col in columns)
			{
				if (col.IsPrimaryKey)
					pks.Add(col.Name);
			}
			return pks;
		}

		public virtual void AddColumn(string table, string sqlColumn)
		{
			table = QuoteTableNameIfRequired(table);
			ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD COLUMN {1}", table, sqlColumn));
		}

		public virtual void ChangeColumn(string table, string sqlColumn)
		{
			table = QuoteTableNameIfRequired(table);
			ExecuteNonQuery(String.Format("ALTER TABLE {0} ALTER COLUMN {1}", table, sqlColumn));
		}

		protected virtual string JoinColumnsAndIndexes(IEnumerable<ColumnPropertiesMapper> columns)
		{
			string indexes = JoinIndexes(columns);
			string columnsAndIndexes = JoinColumns(columns) + (indexes != null ? "," + indexes : String.Empty);
			return columnsAndIndexes;
		}

		protected virtual string JoinIndexes(IEnumerable<ColumnPropertiesMapper> columns)
		{
			var indexes = new List<string>();
			foreach (ColumnPropertiesMapper column in columns)
			{
				string indexSql = column.IndexSql;
				if (indexSql != null)
					indexes.Add(indexSql);
			}

			if (indexes.Count == 0)
				return null;

			return String.Join(", ", indexes.ToArray());
		}

		protected virtual string JoinColumns(IEnumerable<ColumnPropertiesMapper> columns)
		{
			var columnStrings = new List<string>();
			foreach (ColumnPropertiesMapper column in columns)
				columnStrings.Add(column.ColumnSql);
			return String.Join(", ", columnStrings.ToArray());
		}

		IDbCommand BuildCommand(string sql)
		{
			IDbCommand cmd = _connection.CreateCommand();
			cmd.CommandText = sql;
			cmd.CommandType = CommandType.Text;
			if (_transaction != null)
			{
				cmd.Transaction = _transaction;
			}
			return cmd;
		}

		public virtual int Delete(string table)
		{
			return Delete(table, null, (string[]) null);
		}

		protected void EnsureHasConnection()
		{
			if (_connection.State != ConnectionState.Open)
			{
				_connection.Open();
			}
		}

		protected void CreateSchemaInfoTable()
		{
			EnsureHasConnection();
			if (!TableExists("SchemaInfo"))
			{
				AddTable("SchemaInfo", new Column("Version", DbType.Int64, ColumnProperty.PrimaryKey));
			}
		}

		public virtual string QuoteValues(string values)
		{
			return QuoteValues(new[] {values})[0];
		}

		public virtual string[] QuoteValues(string[] values)
		{
			return Array.ConvertAll(values,
			                        delegate(string val)
			                        	{
			                        		if (null == val)
			                        			return "null";
			                        		else
			                        			return String.Format("'{0}'", val.Replace("'", "''"));
			                        	});
		}

		public virtual string JoinColumnsAndValues(string[] columns, string[] values)
		{
			string[] quotedValues = QuoteValues(values);
			var namesAndValues = new string[columns.Length];
			for (int i = 0; i < columns.Length; i++)
			{
				namesAndValues[i] = String.Format("{0}={1}", columns[i], quotedValues[i]);
			}

			return String.Join(", ", namesAndValues);
		}

		protected virtual string GenerateParameterName(int index)
		{
			return "@p" + index;
		}

		protected virtual void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
		{
			if (value == null || value == DBNull.Value)
			{
				parameter.Value = DBNull.Value;
			}
			else if (value is Guid || value is Guid?)
			{
				parameter.DbType = DbType.Guid;
				parameter.Value = (Guid) value;
			}
			else if (value is Int32)
			{
				parameter.DbType = DbType.Int32;
				parameter.Value = value;
			}
			else if (value is Int64)
			{
				parameter.DbType = DbType.Int64;
				parameter.Value = value;
			}
			else if (value is String)
			{
				parameter.DbType = DbType.String;
				parameter.Value = value;
			}
			else if (value is DateTime || value is DateTime?)
			{
				parameter.DbType = DbType.DateTime;
				parameter.Value = value;
			}
			else if (value is Boolean || value is Boolean?)
			{
				parameter.DbType = DbType.Boolean;
				parameter.Value = value;
			}
			else
			{
				throw new NotSupportedException(string.Format("TransformationProvider does not support value: {0} of type: {1}", value, value.GetType()));
			}
		}

		string FormatValue(object value)
		{
			if (value == null) return null;
			if (value is DateTime) return ((DateTime) value).ToString("yyyy-MM-dd HH:mm:ss:fff");
			return value.ToString();
		}

		void QuoteColumnNames(string[] primaryColumns)
		{
			for (int i = 0; i < primaryColumns.Length; i++)
			{
				primaryColumns[i] = QuoteColumnNameIfRequired(primaryColumns[i]);
			}
		}

        public virtual void RemoveIndex(string table, string name)
		{
			if (TableExists(table) && IndexExists(table, name))
			{
			    name = QuoteConstraintNameIfRequired(name);
				ExecuteNonQuery(String.Format("DROP INDEX {0}", name));
			}
		}

        public virtual void AddIndex(string name, string table, params string[] columns)
		{
			if (IndexExists(table, name))
			{
				Logger.Warn("Index {0} already exists", name);
				return;
			}

            name = QuoteConstraintNameIfRequired(name);

            table = QuoteTableNameIfRequired(table);

            columns = QuoteColumnNamesIfRequired(columns);

            ExecuteNonQuery(String.Format("CREATE INDEX {0} ON {1} ({2}) ", name, table, string.Join(", ", columns)));
		}

	    protected string QuoteConstraintNameIfRequired(string name)
	    {
	        return _dialect.ConstraintNameNeedsQuote ? _dialect.Quote(name) : name;
	    }

	    public abstract bool IndexExists(string table, string name);
	}
}