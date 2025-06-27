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

using Migrator.Framework;
using Migrator.Framework.Loggers;
using Migrator.Framework.SchemaBuilder;
using Migrator.Framework.Support;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using ForeignKeyConstraintType = DotNetProjects.Migrator.Framework.ForeignKeyConstraintType;
using Index = Migrator.Framework.Index;

namespace Migrator.Providers
{
    /// <summary>
    /// Base class for every transformation providers.
    /// A 'tranformation' is an operation that modifies the database.
    /// </summary>
    public abstract class TransformationProvider : ITransformationProvider
    {
        private string _scope;
        protected readonly string _connectionString;
        protected readonly string _defaultSchema;
        readonly ForeignKeyConstraintMapper constraintMapper = new ForeignKeyConstraintMapper();
        protected List<long> _appliedMigrations;
        protected IDbConnection _connection;
        protected bool _outsideConnection = false;
        protected Dialect _dialect;
        ILogger _logger;
        IDbTransaction _transaction;

        protected TransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope)
        {
            _dialect = dialect;
            _connectionString = connectionString;
            _defaultSchema = defaultSchema;
            _logger = new Logger(false);
            _scope = scope;
        }

        protected TransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope)
        {
            _dialect = dialect;
            _connection = connection;
            _outsideConnection = true;
            _defaultSchema = defaultSchema;
            _logger = new Logger(false);
            _scope = scope;
        }

        public IMigration CurrentMigration { get; set; }

        private string _schemaInfotable = "SchemaInfo";
        public string SchemaInfoTable
        {
            get
            {
                return _schemaInfotable;
            }
            set
            {
                _schemaInfotable = value;
            }
        }

        public int? CommandTimeout { get; set; }

        public IDialect Dialect
        {
            get { return _dialect; }
        }

        public string ConnectionString { get { return _connectionString; } }

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

        public virtual Index[] GetIndexes(string table)
        {
            throw new NotImplementedException();
        }

        public virtual Column[] GetColumns(string table)
        {
            var columns = new List<Column>();
            using (IDbCommand cmd = CreateCommand())
            using (
                IDataReader reader =
                    ExecuteQuery(
                        cmd, String.Format("select COLUMN_NAME, IS_NULLABLE from INFORMATION_SCHEMA.COLUMNS where table_name = '{0}'", table)))
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

        public virtual ForeignKeyConstraint[] GetForeignKeyConstraints(string table)
        {
            var constraints = new List<ForeignKeyConstraint>();
            using (var cmd = CreateCommand())
            using (
                var reader =
                    // TODO: 
                    // In this statement the naming of alias PK is misleading since INFORMATION_SCHEMA.TABLE_CONSTRAINTS (alias PK) is the child
                    // while INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS (alias C) is the parent
                    ExecuteQuery(
                        cmd, String.Format("SELECT K_Table = FK.TABLE_NAME, FK_Column = CU.COLUMN_NAME, PK_Table = PK.TABLE_NAME, PK_Column = PT.COLUMN_NAME, Constraint_Name = C.CONSTRAINT_NAME FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS C INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS FK ON C.CONSTRAINT_NAME = FK.CONSTRAINT_NAME INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS PK ON C.UNIQUE_CONSTRAINT_NAME = PK.CONSTRAINT_NAME INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE CU ON C.CONSTRAINT_NAME = CU.CONSTRAINT_NAME INNER JOIN ( SELECT i1.TABLE_NAME, i2.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS i1 INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE i2 ON i1.CONSTRAINT_NAME = i2.CONSTRAINT_NAME WHERE i1.CONSTRAINT_TYPE = 'PRIMARY KEY' ) PT ON PT.TABLE_NAME = PK.TABLE_NAME  WHERE FK.table_name = '{0}'", table)))
            {
                while (reader.Read())
                {
                    var constraint = new ForeignKeyConstraint
                    {
                        Name = reader.GetString(4),
                        ParentTable = reader.GetString(0),
                        ParentColumns = [reader.GetString(1)],
                        ChildTable = reader.GetString(2),
                        ChildColumns = [reader.GetString(3)]
                    };

                    constraints.Add(constraint);
                }
            }

            return constraints.ToArray();
        }

        public virtual string[] GetConstraints(string table)
        {
            var constraints = new List<string>();
            using (IDbCommand cmd = CreateCommand())
            using (
                IDataReader reader =
                    ExecuteQuery(
                        cmd, String.Format("SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE LOWER(TABLE_NAME) = LOWER('{0}')", table)))
            {
                while (reader.Read())
                {
                    constraints.Add(reader.GetString(0));
                }
            }

            return constraints.ToArray();
        }

        public virtual Column GetColumnByName(string table, string columnName)
        {
            var columns = GetColumns(table);
            return columns.First(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        }

        public virtual int GetColumnContentSize(string table, string columnName)
        {
            object result = this.ExecuteScalar("SELECT MAX(LENGTH(" + this.QuoteColumnNameIfRequired(columnName) + ")) FROM " + this.QuoteTableNameIfRequired(table));

            if (result == DBNull.Value)
                return 0;
            return Convert.ToInt32(result);
        }

        public virtual string[] GetTables()
        {
            var tables = new List<string>();
            using (IDbCommand cmd = CreateCommand())
            using (IDataReader reader = ExecuteQuery(cmd, "SELECT table_name FROM INFORMATION_SCHEMA.TABLES"))
            {
                while (reader.Read())
                {
                    tables.Add((string)reader[0]);
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
                ExecuteNonQuery(String.Format("ALTER TABLE {0} DROP CONSTRAINT {1}", QuoteTableNameIfRequired(table), QuoteConstraintNameIfRequired(name)));
            }
        }

        public virtual void RemoveAllConstraints(string table)
        {
            foreach (var constraint in GetConstraints(table))
            {
                this.RemoveConstraint(table, constraint);
            }
        }

        public virtual void AddView(string name, string tableName, params IViewField[] fields)
        {
            var lst =
                fields.Where(x => string.IsNullOrEmpty(x.TableName) || x.TableName == tableName)
                    .Select(x => x.ColumnName)
                    .ToList();

            int nr = 0;
            string joins = "";
            foreach (var joinTable in fields.Where(x => !string.IsNullOrEmpty(x.TableName) && x.TableName != tableName).GroupBy(x => x.TableName))
            {
                foreach (var viewField in joinTable)
                {
                    joins += string.Format("JOIN {0} {1} ON {1}.{2} = {3}.{4} ", viewField.TableName, " T" + nr,
                        viewField.KeyColumnName, viewField.ParentTableName, viewField.ParentKeyColumnName);
                    lst.Add(" T" + nr + "." + viewField.ColumnName);
                }
            }

            var select = string.Format("SELECT {0} FROM {1} {2}", string.Join(",", lst), tableName, joins);

            var sql = string.Format("CREATE VIEW {0} AS {1}", name, select);

            ExecuteNonQuery(sql);
        }


        public virtual void AddView(string name, string tableName, params IViewElement[] viewElements)
        {
            var selectedColumns = viewElements.Where(x => x is ViewColumn)
                .Select(x =>
                {
                    var viewColumn = (ViewColumn)x;
                    return $"{viewColumn.Prefix}.{viewColumn.ColumnName} {viewColumn.Prefix}{viewColumn.ColumnName}";
                })
                .ToList();

            var joins = string.Empty;

            foreach (var viewJoin in viewElements.Where(x => x is ViewJoin).Cast<ViewJoin>())
            {
                var joinType = string.Empty;

                switch (viewJoin.JoinType)
                {
                    case JoinType.LeftJoin:
                        joinType = "LEFT JOIN";
                        break;
                    case JoinType.Join:
                        joinType = "JOIN";
                        break;
                }

                var tableAlias = string.IsNullOrEmpty(viewJoin.TableAlias) ? viewJoin.TableName : viewJoin.TableAlias;

                joins += string.Format("{0} {1} {2} ON {2}.{3} = {4}.{5} ", joinType, viewJoin.TableName, tableAlias,
                    viewJoin.ColumnName, viewJoin.ParentTableName, viewJoin.ParentColumnName);
            }

            var select = string.Format("SELECT {0} FROM {1} {1} {2}", string.Join(",", selectedColumns), tableName, joins);
            var sql = string.Format("CREATE VIEW {0} AS {1}", name, select);


            // Works with all DBs. "CREATE OR REPLACE" does not work with SQLite. "DROP IF EXISTS" does not work with oracle.
            try
            {
                ExecuteNonQuery($"DROP VIEW {name}");
            }
            catch
            {
                // Works with all DBs. "CREATE OR REPLACE" does not work with SQLite. "DROP IF EXISTS" does not work with oracle.
            }

            ExecuteNonQuery(sql);
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
        public virtual void AddTable(string name, params IDbField[] columns)
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
        public virtual void AddTable(string name, string engine, params IDbField[] fields)
        {
            var columns = fields.Where(x => x is Column).Cast<Column>().ToArray();

            List<string> pks = GetPrimaryKeys(columns);
            bool compoundPrimaryKey = pks.Count > 1;

            var columnProviders = new List<ColumnPropertiesMapper>(columns.Count());
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
                AddPrimaryKey(getPrimaryKeyname(name), name, pks.ToArray());
            }

            var indexes = fields.Where(x => x is Index).Cast<Index>().ToArray();
            foreach (var index in indexes)
            {
                AddIndex(name, index);
            }

            var foreignKeys = fields.Where(x => x is ForeignKeyConstraint).Cast<ForeignKeyConstraint>().ToArray();
            foreach (var foreignKey in foreignKeys)
            {
                this.AddForeignKey(name, foreignKey);
            }
        }

        protected virtual string getPrimaryKeyname(string tableName)
        {
            return "PK_" + tableName;
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
            if (!ColumnExists(table, column, true))
            {
                throw new MigrationException(string.Format("The table '{0}' does not have a column named '{1}'", table, column));
            }

            var existingColumn = GetColumnByName(table, column);

            ExecuteNonQuery(String.Format("ALTER TABLE {0} DROP COLUMN {1} ", table, Dialect.Quote(existingColumn.Name)));
        }

        public virtual bool ColumnExists(string table, string column)
        {
            return ColumnExists(table, column, true);
        }

        public virtual bool ColumnExists(string table, string column, bool ignoreCase)
        {
            try
            {
                if (ignoreCase)
                {
                    return GetColumns(table).Any(col => col.Name.ToLower() == column.ToLower());
                }

                return GetColumns(table).Any(col => col.Name == column);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public virtual void ChangeColumn(string table, Column column)
        {
            var isUniqueSet = column.ColumnProperty.IsSet(ColumnProperty.Unique);

            column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Unique);

            var mapper = _dialect.GetAndMapColumnProperties(column);

            ChangeColumn(table, mapper.ColumnSql);

            if (isUniqueSet)
            {
                AddUniqueConstraint(string.Format("UX_{0}_{1}", table, column.Name), table, [column.Name]);
            }
        }

        public virtual void RemoveColumnDefaultValue(string table, string column)
        {
            var sql = string.Format("ALTER TABLE {0} ALTER {1} DROP DEFAULT", table, column);
            ExecuteNonQuery(sql);
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

        public virtual bool ViewExists(string view)
        {
            try
            {
                ExecuteNonQuery("SELECT COUNT(*) FROM " + view);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public virtual void SwitchDatabase(string databaseName)
        {
            _connection.ChangeDatabase(databaseName);
        }

        public abstract List<string> GetDatabases();

        public bool DatabaseExists(string name)
        {
#if NETSTANDARD
			return GetDatabases().Any(c => string.Equals(name, c, StringComparison.CurrentCultureIgnoreCase));
#else
            return GetDatabases().Any(c => string.Equals(name, c, StringComparison.InvariantCultureIgnoreCase));
#endif
        }

        public virtual void CreateDatabases(string databaseName)
        {
            ExecuteNonQuery(string.Format("CREATE DATABASE {0}", databaseName));
        }

        public virtual void KillDatabaseConnections(string databaseName)
        {
            //todo, implement this for each DB, no default implementation possible!!!
        }

        public virtual void DropDatabases(string databaseName)
        {
            ExecuteNonQuery(string.Format("DROP DATABASE {0}", databaseName));
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
        public void AddColumn(string table, string column, DbType type, int size, ColumnProperty property,
                                      object defaultValue)
        {
            AddColumn(table, column, (MigratorDbType)type, size, property, defaultValue);
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
        public virtual void AddColumn(string table, string column, MigratorDbType type, int size, ColumnProperty property,
                                      object defaultValue)
        {
            var mapper =
                _dialect.GetAndMapColumnProperties(new Column(column, type, size, property, defaultValue));

            AddColumn(table, mapper.ColumnSql);
        }

        /// <summary>
        /// <see cref="TransformationProvider.AddColumn(string, string, DbType, int, ColumnProperty, object)">
        /// AddColumn(string, string, Type, int, ColumnProperty, object)
        /// </see>
        /// </summary>
        public void AddColumn(string table, string column, DbType type)
        {
            AddColumn(table, column, type, 0, ColumnProperty.Null, null);
        }

        /// <summary>
        /// <see cref="TransformationProvider.AddColumn(string, string, MigratorDbType, int, ColumnProperty, object)">
        /// AddColumn(string, string, Type, int, ColumnProperty, object)
        /// </see>
        /// </summary>
        public virtual void AddColumn(string table, string column, MigratorDbType type)
        {
            AddColumn(table, column, type, 0, ColumnProperty.Null, null);
        }

        /// <summary>
        /// <see cref="TransformationProvider.AddColumn(string, string, DbType, int, ColumnProperty, object)">
        /// AddColumn(string, string, Type, int, ColumnProperty, object)
        /// </see>
        /// </summary>
        public void AddColumn(string table, string column, DbType type, int size)
        {
            AddColumn(table, column, type, size, ColumnProperty.Null, null);
        }

        /// <summary>
        /// <see cref="TransformationProvider.AddColumn(string, string, MigratorDbType, int, ColumnProperty, object)">
        /// AddColumn(string, string, Type, int, ColumnProperty, object)
        /// </see>
        /// </summary>
        public virtual void AddColumn(string table, string column, MigratorDbType type, int size)
        {
            AddColumn(table, column, type, size, ColumnProperty.Null, null);
        }

        public virtual void AddColumn(string table, string column, DbType type, object defaultValue)
        {
            AddColumn(table, column, (MigratorDbType)type, defaultValue);
        }

        public virtual void AddColumn(string table, string column, MigratorDbType type, object defaultValue)
        {
            var mapper =
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
        /// <see cref="TransformationProvider.AddColumn(string, string, MigratorDbType, int, ColumnProperty, object)">
        /// AddColumn(string, string, Type, int, ColumnProperty, object)
        /// </see>
        /// </summary>
        public virtual void AddColumn(string table, string column, MigratorDbType type, ColumnProperty property)
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
        /// <see cref="TransformationProvider.AddColumn(string, string, MigratorDbType, int, ColumnProperty, object)">
        /// AddColumn(string, string, Type, int, ColumnProperty, object)
        /// </see>
        /// </summary>
        public virtual void AddColumn(string table, string column, MigratorDbType type, int size, ColumnProperty property)
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
            ExecuteNonQuery(
                string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} PRIMARY KEY ({2}) ", table, name,
                              string.Join(",", QuoteColumnNamesIfRequired(columns))));
        }
        public virtual void AddPrimaryKeyNonClustered(string name, string table, params string[] columns)
        {
            this.AddPrimaryKey(name, table, columns);
        }
        public virtual void AddUniqueConstraint(string name, string table, params string[] columns)
        {
            QuoteColumnNames(columns);

            table = QuoteTableNameIfRequired(table);

            ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} UNIQUE({2}) ", table, name, string.Join(", ", columns)));
        }

        public virtual void AddCheckConstraint(string name, string table, string checkSql)
        {
            table = QuoteTableNameIfRequired(table);

            ExecuteNonQuery(string.Format("ALTER TABLE {0} ADD CONSTRAINT {1} CHECK ({2}) ", table, name, checkSql));
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
                                               string refColumn, ForeignKeyConstraintType constraint)
        {
            AddForeignKey("FK_" + primaryTable + "_" + refTable, primaryTable, primaryColumn, refTable, refColumn,
                          constraint);
        }

        /// <summary>
        /// Guesses the name of the foreign key and add it
        /// </see>
        /// </summary>
        public virtual void GenerateForeignKey(string primaryTable, string[] primaryColumns, string refTable,
                                               string[] refColumns, ForeignKeyConstraintType constraint)
        {
            AddForeignKey("FK_" + primaryTable + "_" + refTable, primaryTable, primaryColumns, refTable, refColumns,
                          constraint);
        }

        public virtual void AddForeignKey(string table, ForeignKeyConstraint fk)
        {
            AddForeignKey(fk.Name, table, fk.ParentColumns, fk.ChildTable, fk.ChildColumns);
        }

        public virtual void AddForeignKey(string name, string parentTable, string parentColumn, string childTable, string childColumn)
        {
            try
            {
                AddForeignKey(name, parentTable, [parentColumn], childTable, [childColumn]);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Error occured while adding foreign key: \"{0}\" between table: \"{1}\" and table: \"{2}\" - see inner exception for details", name, parentTable, childTable), ex);
            }
        }

        /// <summary>
        /// <see cref="ITransformationProvider.AddForeignKey(string, string, string, string, string)">
        /// AddForeignKey(string, string, string, string, string)
        /// </see>
        /// </summary>
        public virtual void AddForeignKey(string name, string parentTable, string[] parentColumns, string childTable, string[] childColumns)
        {
            AddForeignKey(name, parentTable, parentColumns, childTable, childColumns, ForeignKeyConstraintType.NoAction);
        }

        public virtual void AddForeignKey(string name, string parentTable, string parentColumn, string childTable, string childColumn, ForeignKeyConstraintType constraint)
        {
            AddForeignKey(name, parentTable, new[] { parentColumn }, childTable, new[] { childColumn },
                          constraint);
        }

        public virtual void AddForeignKey(string name, string parentTable, string[] parentColumns, string childTable,
                                          string[] childColumns, ForeignKeyConstraintType constraint)
        {
            childTable = QuoteTableNameIfRequired(childTable);
            parentTable = QuoteTableNameIfRequired(parentTable);
            QuoteColumnNames(parentColumns);
            QuoteColumnNames(childColumns);

            string constraintResolved = constraintMapper.SqlForConstraint(constraint);

            ExecuteNonQuery(
                String.Format(
                    "ALTER TABLE {0} ADD CONSTRAINT {1} FOREIGN KEY ({2}) REFERENCES {3} ({4}) ON UPDATE {5} ON DELETE {6}",
                    parentTable, name, String.Join(",", parentColumns),
                    childTable, String.Join(",", childColumns), constraintResolved, constraintResolved));
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
            return ExecuteNonQuery(sql, this.CommandTimeout.HasValue ? this.CommandTimeout.Value : 30);
        }

        public virtual int ExecuteNonQuery(string sql, int timeout)
        {
            return this.ExecuteNonQuery(sql, timeout, null);
        }

        public virtual int ExecuteNonQuery(string sql, int timeout, params object[] args)
        {
            if (args == null)
            {
                Logger.Trace(sql);
                Logger.ApplyingDBChange(sql);
            }
            else
            {
                Logger.Trace(string.Format(sql, args));
                Logger.ApplyingDBChange(string.Format(sql, args));
            }

            using (IDbCommand cmd = BuildCommand(sql))
            {
                try
                {
                    cmd.CommandTimeout = timeout;

                    if (args != null)
                    {
                        int index = 0;
                        foreach (object obj in args)
                        {
                            IDbDataParameter parameter = cmd.CreateParameter();
                            this.ConfigureParameterWithValue(parameter, index, obj);
                            parameter.ParameterName = this.GenerateParameterNameParameter(index);
                            cmd.Parameters.Add((object)parameter);
                            ++index;
                        }
                    }

                    Logger.Trace(cmd.CommandText);
                    return cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex.Message);
                    throw new Exception(string.Format("Error occured executing sql: {0}, see inner exception for details, error: " + ex, sql), ex);
                }
            }
        }

        public List<string> ExecuteStringQuery(string sql, params object[] args)
        {
            var values = new List<string>();

            using (var cmd = CreateCommand())
            {
                using (var reader = ExecuteQuery(cmd, string.Format(sql, args)))
                {
                    while (reader.Read())
                    {
                        var value = reader[0];

                        if (value == null || value == DBNull.Value)
                        {
                            values.Add(null);
                        }
                        else
                        {
                            values.Add(value.ToString());
                        }
                    }
                }
            }

            return values;
        }

        public virtual void ExecuteScript(string fileName)
        {
            if (CurrentMigration != null)
            {
#if NETSTANDARD
				var assembly = CurrentMigration.GetType().GetTypeInfo().Assembly;
#else
                var assembly = CurrentMigration.GetType().Assembly;
#endif

                string sqlText;
                string file = (new System.Uri(assembly.CodeBase)).AbsolutePath;
                using (var reader = File.OpenText(file))
                    sqlText = reader.ReadToEnd();

                ExecuteNonQuery(sqlText);
            }
        }

        public virtual void ExecuteEmbededScript(string resourceName)
        {
            if (CurrentMigration != null)
            {
#if NETSTANDARD
				var assembly = CurrentMigration.GetType().GetTypeInfo().Assembly;
#else
                var assembly = CurrentMigration.GetType().Assembly;
#endif

                string sqlText;
                string embeddedResourceName = TransformationProviderUtility.GetQualifiedResourcePath(assembly, resourceName);

                using (var stream = assembly.GetManifestResourceStream(embeddedResourceName))
                using (var reader = new StreamReader(stream))
                {
                    sqlText = reader.ReadToEnd();
                }
                ExecuteNonQuery(sqlText);
            }
        }

        /// <summary>
        /// Execute an SQL query returning results.
        /// </summary>
        /// <param name="sql">The SQL text.</param>
        /// <param name="cmd">The IDbCommand.</param>
        /// <returns>A data iterator, <see cref="System.Data.IDataReader">IDataReader</see>.</returns>
        public virtual IDataReader ExecuteQuery(IDbCommand cmd, string sql)
        {
            Logger.Trace(sql);
            cmd.CommandText = sql;
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

        public virtual IDataReader Select(IDbCommand cmd, string what, string from)
        {
            return Select(cmd, what, from, "1=1");
        }

        public virtual IDataReader Select(IDbCommand cmd, string what, string from, string where)
        {
            return ExecuteQuery(cmd, String.Format("SELECT {0} FROM {1} WHERE {2}", what, from, where));
        }

        public virtual IDataReader Select(IDbCommand cmd, string table, string[] columns, string[] whereColumns = null, object[] whereValues = null)
        {
            return SelectComplex(cmd, table, columns, whereColumns, whereValues);
        }

        public virtual IDataReader SelectComplex(IDbCommand cmd, string table, string[] columns, string[] whereColumns = null,
            object[] whereValues = null, string[] nullWhereColumns = null, string[] notNullWhereColumns = null)
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("table");
            if (columns == null) throw new ArgumentNullException("columns");

            table = QuoteTableNameIfRequired(table);

            var builder = new StringBuilder();
            for (int i = 0; i < columns.Length; i++)
            {
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(QuoteColumnNameIfRequired(columns[i]));
            }


            cmd.Transaction = _transaction;

            var query = String.Format("SELECT {0} FROM {1}", builder.ToString(), table);

            if (whereColumns != null || nullWhereColumns != null || notNullWhereColumns != null)
            {
                query = String.Format("SELECT {0} FROM {1} WHERE ", builder.ToString(), table);
            }

            bool andNeeded = false;
            if (whereColumns != null)
            {
                query += GetWhereString(whereColumns, whereValues);
                andNeeded = true;
            }
            if (nullWhereColumns != null)
            {
                if (andNeeded)
                    query += " AND ";
                query += GetWhereStringIsNull(nullWhereColumns);
                andNeeded = true;
            }
            if (notNullWhereColumns != null)
            {
                if (andNeeded)
                    query += " AND ";
                query += GetWhereStringIsNotNull(notNullWhereColumns);
                andNeeded = true;
            }

            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;

            int paramCount = 0;

            if (whereColumns != null)
            {
                foreach (object value in whereValues)
                {
                    IDbDataParameter parameter = cmd.CreateParameter();

                    ConfigureParameterWithValue(parameter, paramCount, value);

                    parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                    cmd.Parameters.Add(parameter);

                    paramCount++;
                }
            }

            Logger.Trace(cmd.CommandText);
            return cmd.ExecuteReader();

        }

        public object SelectScalar(string what, string from)
        {
            return SelectScalar(what, from, "1=1");
        }

        public virtual object SelectScalar(string what, string from, string where)
        {
            return ExecuteScalar(String.Format("SELECT {0} FROM {1} WHERE {2}", what, from, where));
        }

        public virtual object SelectScalar(string what, string from, string[] whereColumns, object[] whereValues)
        {
            using (IDbCommand command = _connection.CreateCommand())
            {
                if (CommandTimeout.HasValue)
                    command.CommandTimeout = CommandTimeout.Value;

                command.Transaction = _transaction;

                var query = String.Format("SELECT {0} FROM {1} WHERE {2}", what, from, GetWhereString(whereColumns, whereValues));

                command.CommandText = query;
                command.CommandType = CommandType.Text;

                int paramCount = 0;

                foreach (object value in whereValues)
                {
                    IDbDataParameter parameter = command.CreateParameter();

                    ConfigureParameterWithValue(parameter, paramCount, value);

                    parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                    command.Parameters.Add(parameter);

                    paramCount++;
                }

                Logger.Trace(command.CommandText);
                return command.ExecuteScalar();
            }
        }

        public virtual int Update(string table, string[] columns, object[] values)
        {
            return Update(table, columns, values, null);
        }

        public virtual int Update(string table, string[] columns, object[] values, string where)
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("table");
            if (columns == null) throw new ArgumentNullException("columns");
            if (values == null) throw new ArgumentNullException("values");
            if (columns.Length != values.Length) throw new Exception(string.Format("The number of columns: {0} does not match the number of supplied values: {1}", columns.Length, values.Length));

            table = QuoteTableNameIfRequired(table);

            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(QuoteColumnNameIfRequired(columns[i]));
                builder.Append(" = ");
                builder.Append(GenerateParameterName(i));
            }

            using (IDbCommand command = _connection.CreateCommand())
            {
                if (CommandTimeout.HasValue)
                    command.CommandTimeout = CommandTimeout.Value;

                command.Transaction = _transaction;

                var query = String.Format("UPDATE {0} SET {1}", table, builder.ToString());
                if (!String.IsNullOrEmpty(where))
                {
                    query += " WHERE " + where;
                }
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                int paramCount = 0;

                foreach (object value in values)
                {
                    IDbDataParameter parameter = command.CreateParameter();

                    ConfigureParameterWithValue(parameter, paramCount, value);

                    parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                    command.Parameters.Add(parameter);

                    paramCount++;
                }

                Logger.Trace(command.CommandText);
                return command.ExecuteNonQuery();
            }
        }

        public virtual int Update(string table, string[] columns, object[] values, string[] whereColumns, object[] whereValues)
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("table");
            if (columns == null) throw new ArgumentNullException("columns");
            if (values == null) throw new ArgumentNullException("values");
            if (columns.Length != values.Length) throw new Exception(string.Format("The number of columns: {0} does not match the number of supplied values: {1}", columns.Length, values.Length));
            if (whereColumns.Length != whereValues.Length) throw new Exception(string.Format("The number of whereColumns: {0} does not match the number of supplied whereValues: {1}", whereColumns.Length, whereValues.Length));

            table = QuoteTableNameIfRequired(table);

            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(QuoteColumnNameIfRequired(columns[i]));
                builder.Append(" = ");
                builder.Append(GenerateParameterName(i));
            }

            using (IDbCommand command = _connection.CreateCommand())
            {
                if (CommandTimeout.HasValue)
                    command.CommandTimeout = CommandTimeout.Value;

                command.Transaction = _transaction;

                var query = String.Format("UPDATE {0} SET {1} WHERE {2}", table, builder.ToString(), GetWhereStringWithNullCheck(whereColumns, whereValues, values.Length));

                command.CommandText = query;
                command.CommandType = CommandType.Text;

                int paramCount = 0;

                foreach (object value in values)
                {
                    IDbDataParameter parameter = command.CreateParameter();

                    ConfigureParameterWithValue(parameter, paramCount, value);

                    parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                    command.Parameters.Add(parameter);

                    paramCount++;
                }

                foreach (object value in whereValues)
                {
                    if (value == null || value == DBNull.Value)
                        continue;

                    IDbDataParameter parameter = command.CreateParameter();

                    ConfigureParameterWithValue(parameter, paramCount, value);

                    parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                    command.Parameters.Add(parameter);

                    paramCount++;
                }


                Logger.Trace(command.CommandText);
                return command.ExecuteNonQuery();
            }
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
                if (CommandTimeout.HasValue)
                    command.CommandTimeout = CommandTimeout.Value;

                command.Transaction = _transaction;

                command.CommandText = String.Format("INSERT INTO {0} ({1}) VALUES ({2})", table, columnNames, parameterNames);
                command.CommandType = CommandType.Text;

                int paramCount = 0;

                foreach (object value in values)
                {
                    IDbDataParameter parameter = command.CreateParameter();

                    ConfigureParameterWithValue(parameter, paramCount, value);

                    parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                    command.Parameters.Add(parameter);

                    paramCount++;
                }

                return command.ExecuteNonQuery();
            }
        }

        protected virtual string GetWhereStringWithNullCheck(string[] whereColumns, object[] whereValues, int parameterStartIndex = 0)
        {
            var builder2 = new StringBuilder();
            var parCnt = 0;
            for (int i = 0; i < whereColumns.Length; i++)
            {
                if (builder2.Length > 0) builder2.Append(" AND ");
                var val = whereValues[i];
                if (val == null || val == DBNull.Value)
                {
                    builder2.Append(QuoteColumnNameIfRequired(whereColumns[i]));
                    builder2.Append(" is null ");
                }
                else
                {
                    builder2.Append(QuoteColumnNameIfRequired(whereColumns[i]));
                    builder2.Append(" = ");
                    builder2.Append(GenerateParameterName(parCnt + parameterStartIndex));
                    parCnt++;
                }
            }

            return builder2.ToString();
        }

        protected virtual string GetWhereString(string[] whereColumns, object[] whereValues, int parameterStartIndex = 0)
        {
            var builder2 = new StringBuilder();
            for (int i = 0; i < whereColumns.Length; i++)
            {
                if (builder2.Length > 0) builder2.Append(" AND ");
                builder2.Append(QuoteColumnNameIfRequired(whereColumns[i]));
                builder2.Append(" = ");
                builder2.Append(GenerateParameterName(i + parameterStartIndex));
            }

            return builder2.ToString();
        }

        protected virtual string GetWhereStringIsNull(string[] whereColumns)
        {
            var builder2 = new StringBuilder();
            for (int i = 0; i < whereColumns.Length; i++)
            {
                if (builder2.Length > 0) builder2.Append(" AND ");
                builder2.Append(QuoteColumnNameIfRequired(whereColumns[i]));
                builder2.Append(" IS NULL");
            }

            return builder2.ToString();
        }

        protected virtual string GetWhereStringIsNotNull(string[] whereColumns)
        {
            var builder2 = new StringBuilder();
            for (int i = 0; i < whereColumns.Length; i++)
            {
                if (builder2.Length > 0) builder2.Append(" AND ");
                builder2.Append(QuoteColumnNameIfRequired(whereColumns[i]));
                builder2.Append(" IS NOT NULL");
            }

            return builder2.ToString();
        }

        public virtual int InsertIfNotExists(string table, string[] columns, object[] values, string[] whereColumns, object[] whereValues)
        {
            using (var cmd = CreateCommand())
            using (var reader = this.Select(cmd, table, new[] { whereColumns[0] }, whereColumns, whereValues))
            {
                if (!reader.Read())
                {
                    reader.Close();
                    return this.Insert(table, columns, values);
                }
                else
                {
                    reader.Close();
                    return 0;
                }
            }
        }

        public virtual int Delete(string table, string[] whereColumns = null, object[] whereValues = null)
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("table");


            if (null == whereColumns || null == whereValues)
            {
                return ExecuteNonQuery(String.Format("DELETE FROM {0}", table));
            }
            else
            {
                table = QuoteTableNameIfRequired(table);

                using (IDbCommand command = _connection.CreateCommand())
                {
                    if (CommandTimeout.HasValue)
                        command.CommandTimeout = CommandTimeout.Value;

                    command.Transaction = _transaction;

                    var query = String.Format("DELETE FROM {0} WHERE ({1})", table,
                        GetWhereString(whereColumns, whereValues));

                    command.CommandText = query;
                    command.CommandType = CommandType.Text;

                    int paramCount = 0;

                    foreach (object value in whereValues)
                    {
                        IDbDataParameter parameter = command.CreateParameter();

                        ConfigureParameterWithValue(parameter, paramCount, value);

                        parameter.ParameterName = GenerateParameterNameParameter(paramCount);

                        command.Parameters.Add(parameter);

                        paramCount++;
                    }

                    Logger.Trace(command.CommandText);
                    return command.ExecuteNonQuery();
                }
            }
        }

        public virtual int Delete(string table, string wherecolumn, string wherevalue)
        {
            if (string.IsNullOrEmpty(wherecolumn) && string.IsNullOrEmpty(wherevalue))
            {
                return Delete(table, (string[])null, null);
            }

            return ExecuteNonQuery(String.Format("DELETE FROM {0} WHERE {1} = {2}", table, wherecolumn, QuoteValues(wherevalue)));
        }

        public virtual int TruncateTable(string table)
        {
            return ExecuteNonQuery(String.Format("TRUNCATE TABLE {0} ", table));
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
                    if (!_outsideConnection)
                    {
                        _connection.Close();
                    }
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
                    if (!_outsideConnection)
                    {
                        _connection.Close();
                    }
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
                    string scopeColumn = "Scope";

                    versionColumn = QuoteColumnNameIfRequired(versionColumn);
                    scopeColumn = QuoteColumnNameIfRequired(scopeColumn);

                    using (var cmd = CreateCommand())
                    using (IDataReader reader = Select(cmd, versionColumn, _schemaInfotable, string.Format("{0} = '{1}'", scopeColumn, _scope)))
                    {
                        while (reader.Read())
                        {
                            if (reader.GetFieldType(0) == typeof(Decimal))
                            {
                                _appliedMigrations.Add((long)reader.GetDecimal(0));
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

        public virtual bool IsMigrationApplied(long version, string scope)
        {
            var value = SelectScalar("Version", _schemaInfotable, new[] { "Scope", "Version" }, new object[] { scope, version });
            return Convert.ToInt64(value) == version;
        }

        /// <summary>
        /// Marks a Migration version number as having been applied
        /// </summary>
        /// <param name="version">The version number of the migration that was applied</param>
        public virtual void MigrationApplied(long version, string scope)
        {
            CreateSchemaInfoTable();
            Insert(_schemaInfotable, new string[] { "Scope", "Version", "TimeStamp" }, new object[] { scope ?? _scope, version, DateTime.UtcNow });
            _appliedMigrations.Add(version);
        }

        /// <summary>
        /// Marks a Migration version number as having been rolled back from the database
        /// </summary>
        /// <param name="version">The version number of the migration that was removed</param>
        public virtual void MigrationUnApplied(long version, string scope)
        {
            CreateSchemaInfoTable();
            Delete(_schemaInfotable, new[] { "Scope", "Version" }, new[] { scope ?? _scope, version.ToString() });
            _appliedMigrations.Remove(version);
        }

        public virtual void AddColumn(string table, Column column)
        {
            AddColumn(table, column.Name, column.Type, column.Size, column.ColumnProperty, column.DefaultValue);
        }

        public virtual void GenerateForeignKey(string primaryTable, string refTable)
        {
            GenerateForeignKey(primaryTable, refTable, ForeignKeyConstraintType.NoAction);
        }

        public virtual void GenerateForeignKey(string primaryTable, string refTable, ForeignKeyConstraintType constraint)
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
                if (!_outsideConnection)
                {
                    _connection.Close();
                }
            }

            if (_connection != null)
            {
                if (!_outsideConnection)
                {
                    _connection.Close();
                }
            }

            _connection = null;
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

        public virtual void RemoveAllForeignKeys(string tableName, string columnName)
        { }

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

        public virtual void AddColumnDefaultValue(string table, string column, object defaultValue)
        {
            table = QuoteTableNameIfRequired(table);
            column = this.QuoteColumnNameIfRequired(column);
            var def = Dialect.Default(defaultValue);
            ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD DEFAULT('{1}') FOR {2}", table, def, column));
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
            {
                columnStrings.Add(column.ColumnSql);
            }

            return string.Join(", ", columnStrings.ToArray());
        }

        public IDbCommand CreateCommand()
        {
            EnsureHasConnection();
            IDbCommand cmd = _connection.CreateCommand();

            if (CommandTimeout.HasValue)
                cmd.CommandTimeout = CommandTimeout.Value;

            cmd.CommandType = CommandType.Text;
            if (_transaction != null)
            {
                cmd.Transaction = _transaction;
            }

            if (CommandTimeout.HasValue)
            {
                cmd.CommandTimeout = CommandTimeout.Value;
            }
            return cmd;
        }

        protected IDbCommand BuildCommand(string sql)
        {
            var cmd = CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        public virtual int Delete(string table)
        {
            return Delete(table, null, (string[])null);
        }

        protected void EnsureHasConnection()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        protected virtual void CreateSchemaInfoTable()
        {
            EnsureHasConnection();
            if (!TableExists(_schemaInfotable))
            {
                AddTable(_schemaInfotable,
                    new Column("Version", DbType.Int64, ColumnProperty.NotNull | ColumnProperty.PrimaryKey),
                    new Column("Scope", DbType.String, 50, ColumnProperty.NotNull | ColumnProperty.PrimaryKey, "default"),
                    new Column("TimeStamp", DbType.DateTime));
            }
            else
            {
                if (!ColumnExists(_schemaInfotable, "Scope"))
                {
                    AddColumn(_schemaInfotable, "Scope", DbType.String, 50, ColumnProperty.NotNull, "default");
                    RemoveAllConstraints(_schemaInfotable);
                    AddPrimaryKey("PK_SchemaInfo", _schemaInfotable, new[] { "Version", "Scope" });
                }

                if (!ColumnExists(_schemaInfotable, "TimeStamp"))
                {
                    AddColumn(_schemaInfotable, "TimeStamp", DbType.DateTime);
                }
            }
        }

        public virtual string QuoteValues(string values)
        {
            return QuoteValues(new[] { values })[0];
        }

        public virtual string[] QuoteValues(string[] values)
        {
            return values.Select(val =>
            {
                if (null == val)
                    return "null";
                else
                    return String.Format("'{0}'", val.Replace("'", "''"));
            }).ToArray();
        }

        public virtual string JoinColumnsAndValues(string[] columns, string[] values)
        {
            return JoinColumnsAndValues(columns, values, ", ");
        }

        public virtual string JoinColumnsAndValues(string[] columns, string[] values, string joinSeperator)
        {
            string[] quotedValues = QuoteValues(values);
            var namesAndValues = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                namesAndValues[i] = String.Format("{0}={1}", columns[i], quotedValues[i]);
            }

            return String.Join(joinSeperator, namesAndValues);
        }

        public virtual string GenerateParameterNameParameter(int index)
        {
            return "@p" + index;
        }

        public virtual string GenerateParameterName(int index)
        {
            return GenerateParameterNameParameter(index);
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
                parameter.Value = (Guid)value;
            }
            else if (value is Int16)
            {
                parameter.DbType = DbType.Int16;
                parameter.Value = value;
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
            else if (value is UInt16)
            {
                parameter.DbType = DbType.UInt16;
                parameter.Value = value;
            }
            else if (value is UInt32)
            {
                parameter.DbType = DbType.UInt32;
                parameter.Value = value;
            }
            else if (value is UInt64)
            {
                parameter.DbType = DbType.UInt64;
                parameter.Value = value;
            }
            else if (value is Double)
            {
                parameter.DbType = DbType.Double;
                parameter.Value = value;
            }
            else if (value is Decimal)
            {
                parameter.DbType = DbType.Decimal;
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
            if (value is DateTime) return ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss:fff");
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

        public virtual void AddIndex(string table, Index index)
        {
            AddIndex(index.Name, table, index.KeyColumns);
        }

        public virtual void AddIndex(string name, string table, params string[] columns)
        {
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

        protected virtual string GetPrimaryKeyConstraintName(string table)
        {
            return null;
        }

        public virtual void RemovePrimaryKey(string table)
        {
            if (!TableExists(table)) return;

            var primaryKeyConstraintName = GetPrimaryKeyConstraintName(table);

            if (primaryKeyConstraintName == null || !ConstraintExists(table, primaryKeyConstraintName)) return;

            RemoveConstraint(table, primaryKeyConstraintName);
        }

        public virtual void RemoveAllIndexes(string table)
        {
            if (!TableExists(table)) return;

            var indexes = GetIndexes(table);

            foreach (var index in indexes)
            {
                if (index.Name == null || !IndexExists(table, index.Name)) continue;

                if (index.PrimaryKey || index.UniqueConstraint)
                    RemoveConstraint(table, index.Name);
                else
                    RemoveIndex(table, index.Name);
            }
        }

        public virtual string Concatenate(params string[] strings)
        {
            return string.Join(" || ", strings);
        }

        public IDbConnection Connection
        {
            get { return _connection; }
        }

        public IEnumerable<string> GetTables(string schema)
        {
            var tableRestrictions = new string[4];
            tableRestrictions[1] = schema;

            var c = _connection as DbConnection;
            var tables = c.GetSchema("Tables", tableRestrictions);
            return from DataRow row in tables.Rows select (row["TABLE_NAME"] as string);
        }

        public IEnumerable<string> GetColumns(string schema, string table)
        {
            var tableRestrictions = new string[4];
            tableRestrictions[1] = schema;
            tableRestrictions[2] = table;

            var c = _connection as DbConnection;
            var tables = c.GetSchema("Columns", tableRestrictions);
            return from DataRow row in tables.Rows select (row["TABLE_NAME"] as string);
        }


    }
}
