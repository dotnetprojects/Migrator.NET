using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Migrator.Framework;
using Oracle.DataAccess.Client;
using ForeignKeyConstraint = Migrator.Framework.ForeignKeyConstraint;

namespace Migrator.Providers.Oracle
{
    public class OracleTransformationProviderBase : TransformationProvider
    {
        protected OracleTransformationProviderBase(Dialect dialect, string connectionString, string defaultSchema) : base(dialect, connectionString, defaultSchema)
        {
        }

        public const string TemporaryColumnName = "TEMPCOL";

        public override void AddForeignKey(string name, string primaryTable, string[] primaryColumns, string refTable,
                                           string[] refColumns, ForeignKeyConstraint constraint)
        {
            GuardAgainstMaximumIdentifierLengthForOracle(name);

            if (ConstraintExists(primaryTable, name))
            {
                Logger.Warn("Constraint {0} already exists", name);
                return;
            }

            primaryTable = QuoteTableNameIfRequired(primaryTable);
            refTable = QuoteTableNameIfRequired(refTable);
            string primaryColumnsSql = String.Join(",", primaryColumns.Select(col => QuoteColumnNameIfRequired(col)).ToArray());
            string refColumnsSql = String.Join(",", refColumns.Select(col => QuoteColumnNameIfRequired(col)).ToArray());

            ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD CONSTRAINT {1} FOREIGN KEY ({2}) REFERENCES {3} ({4})", primaryTable, name, primaryColumnsSql, refTable, refColumnsSql));
        }

        void GuardAgainstMaximumIdentifierLengthForOracle(string name)
        {
            if (name.Length > 30)
            {
                throw new ArgumentException(string.Format("The name \"{0}\" is {1} characters in length, bug maximum length for Oracle identifier is 30 characters.", name, name.Length), "name");
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
			
            if (column.Type == DbType.String)
            {
                RenameColumn(table, column.Name, TemporaryColumnName);

                // check if this is not-null
                bool isNotNull = (column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull;

                // remove the not-null option
                column.ColumnProperty = (column.ColumnProperty & ~ColumnProperty.NotNull);

                AddColumn(table, column);
                CopyDataFromOneColumnToAnother(table, TemporaryColumnName, column.Name);
                RemoveColumn(table, TemporaryColumnName);
                //RenameColumn(table, TemporaryColumnName, column.Name);
				
                string columnName = QuoteColumnNameIfRequired(column.Name);
				
                // now set the column to not-null
                if (isNotNull) ExecuteQuery(String.Format("ALTER TABLE {0} MODIFY ({1} NOT NULL)", table, columnName));
            }
            else
            {
                if (((existingColumn.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull)
                    && ((column.ColumnProperty & ColumnProperty.NotNull) == ColumnProperty.NotNull))
                {
                    // was not null, 	and is being change to not-null - drop the not-null all together
                    column.ColumnProperty = column.ColumnProperty & ~ColumnProperty.NotNull;
                }
                else if 
                    (((existingColumn.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null)
                     && ((column.ColumnProperty & ColumnProperty.Null) == ColumnProperty.Null))
                {
                    // was null, and is being changed to null - drop the null all together
                    column.ColumnProperty = column.ColumnProperty & ~ColumnProperty.Null;
                }
			
                ColumnPropertiesMapper mapper = _dialect.GetAndMapColumnProperties(column);

                ChangeColumn(table, mapper.ColumnSql);
            }
        }

        void CopyDataFromOneColumnToAnother(string table, string fromColumn, string toColumn)
        {
            table = QuoteTableNameIfRequired(table);
            fromColumn = QuoteColumnNameIfRequired(fromColumn);
            toColumn = QuoteColumnNameIfRequired(toColumn);

            ExecuteNonQuery(string.Format("UPDATE {0} SET {1} = {2}", table, toColumn, fromColumn));
        }

        public override void RenameTable(string oldName, string newName)
        {
            GuardAgainstMaximumIdentifierLengthForOracle(newName);
            GuardAgainstExistingTableWithSameName(newName, oldName);

            oldName = QuoteTableNameIfRequired(oldName);
            newName = QuoteTableNameIfRequired(newName);

            ExecuteNonQuery(String.Format("ALTER TABLE {0} RENAME TO {1}", oldName, newName));
        }

        void GuardAgainstExistingTableWithSameName(string newName, string oldName)
        {
            if (TableExists(newName)) throw new MigrationException(string.Format("Can not rename table \"{0}\" to \"{1}\", a table with that name already exists", oldName, newName));
        }

        public override void RenameColumn(string tableName, string oldColumnName, string newColumnName)
        {
            GuardAgainstMaximumIdentifierLengthForOracle(newColumnName);
            GuardAgainstExistingColumnWithSameName(newColumnName, tableName);
			
            tableName = QuoteTableNameIfRequired(tableName);
            oldColumnName = QuoteColumnNameIfRequired(oldColumnName);
            newColumnName = QuoteColumnNameIfRequired(newColumnName);
			
            ExecuteNonQuery(string.Format("ALTER TABLE {0} RENAME COLUMN {1} TO {2}", tableName, oldColumnName, newColumnName));
        }

        void GuardAgainstExistingColumnWithSameName(string newColumnName, string tableName)
        {
            if (ColumnExists(tableName, newColumnName)) throw new MigrationException(string.Format("A column with the name \"{0}\" already exists in the table \"{1}\"", newColumnName, tableName));
        }

        public override void ChangeColumn(string table, string sqlColumn)
        {
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("table");
            if (string.IsNullOrEmpty(table)) throw new ArgumentNullException("sqlColumn");

            table = QuoteTableNameIfRequired(table);
            sqlColumn = QuoteColumnNameIfRequired(sqlColumn);
            ExecuteNonQuery(String.Format("ALTER TABLE {0} MODIFY {1}", table, sqlColumn));
        }

        public override void AddColumn(string table, string sqlColumn)
        {
            GuardAgainstMaximumIdentifierLengthForOracle(table);
            table = QuoteTableNameIfRequired(table);
            sqlColumn = QuoteColumnNameIfRequired(sqlColumn);
            ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
        }

        public override bool ConstraintExists(string table, string name)
        {
            string sql =
                string.Format(
                    "SELECT COUNT(constraint_name) FROM user_constraints WHERE lower(constraint_name) = '{0}' AND lower(table_name) = '{1}'",
                    name.ToLower(), table.ToLower());
            Logger.Log(sql);
            object scalar = ExecuteScalar(sql);
            return Convert.ToInt32(scalar) == 1;
        }

        public override bool ColumnExists(string table, string column)
        {
            if (!TableExists(table))
                return false;

            string sql =
                string.Format(
                    "SELECT COUNT(column_name) FROM user_tab_columns WHERE lower(table_name) = '{0}' AND lower(column_name) = '{1}'",
                    table.ToLower(), column.ToLower());
            Logger.Log(sql);
            object scalar = ExecuteScalar(sql);
            return Convert.ToInt32(scalar) == 1;
        }

        public override bool TableExists(string table)
        {
            string sql = string.Format("SELECT COUNT(table_name) FROM user_tables WHERE lower(table_name) = '{0}'",
                                       table.ToLower());
            Logger.Log(sql);
            object count = ExecuteScalar(sql);
            return Convert.ToInt32(count) == 1;
        }

        public override string[] GetTables()
        {
            var tables = new List<string>();

            using (IDataReader reader =
                ExecuteQuery("SELECT table_name FROM user_tables"))
            {
                while (reader.Read())
                {
                    tables.Add(reader[0].ToString());
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
                        string.Format(
                            "select column_name, data_type, data_length, data_precision, data_scale, NULLABLE FROM USER_TAB_COLUMNS WHERE lower(table_name) = '{0}'",
                            table.ToLower())))
            {
                while (reader.Read())
                {
                    string colName = reader[0].ToString();
                    DbType colType = DbType.String;
                    string dataType = reader[1].ToString().ToLower();
                    bool isNullable = ParseBoolean(reader.GetValue(5));

                    if (dataType.Equals("number"))
                    {
                        int precision = Convert.ToInt32(reader.GetValue(3));
                        int scale = Convert.ToInt32(reader.GetValue(4));
                        if (scale == 0)
                        {
                            colType = precision <= 10 ? DbType.Int16 : DbType.Int64;
                        }
                        else
                        {
                            colType = DbType.Decimal;
                        }
                    }
                    else if (dataType.StartsWith("timestamp") || dataType.Equals("date"))
                    {
                        colType = DbType.DateTime;
                    }

                    var columnProperties = (isNullable) ? ColumnProperty.Null : ColumnProperty.NotNull;

                    columns.Add(new Column(colName, colType, columnProperties));
                }
            }

            return columns.ToArray();
        }

        bool ParseBoolean(object value)
        {
            if (value is string)
            {
                if ("N" == (string)value) return false;
                if ("Y" == (string)value) return true;
            }

            return Convert.ToBoolean(value);
        }

        protected override string GenerateParameterName(int index)
        {
            return ":p" + index;
        }

        protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
        {
            if (value is Guid || value is Guid?)
            {
                parameter.DbType = DbType.Binary;

                if (value is Guid? && !((Guid?) value).HasValue)
                {
                    return;
                }

                parameter.Value = ((Guid) value).ToByteArray();
            }
            else if (value is bool || value is bool?)
            {
                parameter.DbType = DbType.Int32;
                parameter.Value = ((bool) value) ? 1 : 0;
            }
            else
            {
                base.ConfigureParameterWithValue(parameter, index, value);
            }
        }

        public override void AddTable(string name, params Column[] columns)
        {
            GuardAgainstMaximumIdentifierLengthForOracle(name);

            GuardAgainstMaximumColumnNameLengthForOracle(name, columns);

            base.AddTable(name, columns);
        }

        void GuardAgainstMaximumColumnNameLengthForOracle(string name, Column[] columns)
        {
            foreach (Column column in columns)
            {
                if (column.Name.Length > 30)
                {
                    throw new ArgumentException(
                        string.Format("When adding table: \"{0}\", the column: \"{1}\", the name of the column is: {2} characters in length, but maximum length for an oracle identifier is 30 characters", name,
                                      column.Name, column.Name.Length), "columns");
                }
            }
        }

        public override string Encode(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            var hex = new StringBuilder(bytes.Length*2);
            foreach (byte b in bytes) hex.AppendFormat("{0:X2}", b);
            return hex.ToString();
        }

        public override bool IndexExists(string table, string name)
        {
            string sql =
                string.Format(
                    "SELECT COUNT(index_name) FROM user_indexes WHERE lower(index_name) = '{0}' AND lower(table_name) = '{1}'",
                    name.ToLower(), table.ToLower());
            Logger.Log(sql);
            object scalar = ExecuteScalar(sql);
            return Convert.ToInt32(scalar) == 1;
        }
    }

    public class OracleTransformationProvider : OracleTransformationProviderBase
	{
        public OracleTransformationProvider(Dialect dialect, string connectionString, string defaultSchema)
			: base(dialect, connectionString, defaultSchema)
		{
			_connection = new OracleConnection();
			_connection.ConnectionString = _connectionString;
			_connection.Open();
		}
	}
}