using System;
using System.Data;
using Migrator.Framework;

namespace Migrator.Providers.SqlServer
{
	public class SqlServerDialect : Dialect
	{
		public const string DboSchemaName = "dbo";
		
		public SqlServerDialect()
		{
			RegisterColumnType(DbType.AnsiStringFixedLength, "CHAR(255)");
		    RegisterColumnType(DbType.AnsiStringFixedLength, int.MaxValue - 1, "CHAR($l)");
		    RegisterColumnType(DbType.AnsiStringFixedLength, int.MaxValue, "CHAR(max)");
			RegisterColumnType(DbType.AnsiString, "VARCHAR(255)");
			RegisterColumnType(DbType.AnsiString, 8000, "VARCHAR($l)");
			RegisterColumnType(DbType.AnsiString, 2147483647, "TEXT");
			RegisterColumnType(DbType.Binary, "VARBINARY(8000)");
			RegisterColumnType(DbType.Binary, int.MaxValue-1, "VARBINARY($l)");
            RegisterColumnType(DbType.Binary, int.MaxValue, "VARBINARY(max)");
			RegisterColumnType(DbType.Boolean, "BIT");
			RegisterColumnType(DbType.Byte, "TINYINT");
			RegisterColumnType(DbType.Currency, "MONEY");
			RegisterColumnType(DbType.Date, "DATETIME");
			RegisterColumnType(DbType.DateTime, "DATETIME");
            RegisterColumnType(DbType.Decimal, "DECIMAL(19,5)");
            RegisterColumnType(DbType.Decimal, 19, "DECIMAL(19, $l)");
            RegisterColumnType(DbType.Double, "DOUBLE PRECISION"); //synonym for FLOAT(53)
            RegisterColumnType(DbType.Double, 24, "FLOAT(24)");
            RegisterColumnType(DbType.Double, 53, "FLOAT(53)");
            RegisterColumnType(DbType.Guid, "UNIQUEIDENTIFIER");
			RegisterColumnType(DbType.Int16, "SMALLINT");
			RegisterColumnType(DbType.Int32, "INT");
			RegisterColumnType(DbType.Int64, "BIGINT");
			RegisterColumnType(DbType.Single, "REAL"); //synonym for FLOAT(24) 
			RegisterColumnType(DbType.StringFixedLength, "NCHAR(255)");
		    RegisterColumnType(DbType.StringFixedLength, int.MaxValue - 1, "NCHAR($l)");
            RegisterColumnType(DbType.StringFixedLength, int.MaxValue, "NCHAR(max)");
			RegisterColumnType(DbType.String, "NVARCHAR(255)");
            RegisterColumnType(DbType.String, int.MaxValue - 1, "NVARCHAR($l)");
            RegisterColumnType(DbType.String, int.MaxValue, "NVARCHAR(max)");
			//RegisterColumnType(DbType.String, 1073741823, "NTEXT");
			RegisterColumnType(DbType.Time, "DATETIME");
            RegisterColumnType(DbType.VarNumeric, "NUMERIC(18,0)");
            RegisterColumnType(DbType.VarNumeric, 38, "NUMERIC($l,0)");

			RegisterProperty(ColumnProperty.Identity, "IDENTITY");

            AddReservedWords("ADD", "EXCEPT", "PERCENT", "ALL", "EXEC", "PLAN", "ALTER", "EXECUTE", "PRECISION", "AND", "EXISTS", "PRIMARY", "ANY", "EXIT", "PRINT", "AS", "FETCH", "PROC", "ASC", "FILE", "PROCEDURE", "AUTHORIZATION", "FILLFACTOR", "PUBLIC", "BACKUP", "FOR", "RAISERROR", "BEGIN", "FOREIGN", "READ", "BETWEEN", "FREETEXT", "READTEXT", "BREAK", "FREETEXTTABLE", "RECONFIGURE", "BROWSE", "FROM", "REFERENCES", "BULK", "FULL", "REPLICATION", "BY", "FUNCTION", "RESTORE", "CASCADE", "GOTO", "RESTRICT", "CASE", "GRANT", "RETURN", "CHECK", "GROUP", "REVOKE", "CHECKPOINT", "HAVING", "RIGHT", "CLOSE", "HOLDLOCK", "ROLLBACK", "CLUSTERED", "IDENTITY", "ROWCOUNT", "COALESCE", "IDENTITY_INSERT", "ROWGUIDCOL", "COLLATE", "IDENTITYCOL", "RULE", "COLUMN", "IF", "SAVE", "COMMIT", "IN", "SCHEMA", "COMPUTE", "INDEX", "SELECT", "CONSTRAINT", "INNER", "SESSION_USER", "CONTAINS", "INSERT", "SET", "CONTAINSTABLE", "INTERSECT", "SETUSER", "CONTINUE", "INTO", "SHUTDOWN", "CONVERT", "IS", "SOME", "CREATE", "JOIN", "STATISTICS", "CROSS", "KEY", "SYSTEM_USER", "CURRENT", "KILL", "TABLE", "CURRENT_DATE", "LEFT", "TEXTSIZE", "CURRENT_TIME", "LIKE", "THEN", "CURRENT_TIMESTAMP", "LINENO", "TO", "CURRENT_USER", "LOAD", "TOP", "CURSOR", "NATIONAL", "TRAN", "DATABASE", "NOCHECK", "TRANSACTION", "DBCC", "NONCLUSTERED", "TRIGGER", "DEALLOCATE", "NOT", "TRUNCATE", "DECLARE", "NULL", "TSEQUAL", "DEFAULT", "NULLIF", "UNION", "DELETE", "OF", "UNIQUE", "DENY", "OFF", "UPDATE", "DESC", "OFFSETS", "UPDATETEXT", "DISK", "ON", "USE", "DISTINCT", "OPEN", "USER", "DISTRIBUTED", "OPENDATASOURCE", "VALUES", "DOUBLE", "OPENQUERY", "VARYING", "DROP", "OPENROWSET", "VIEW", "DUMMY", "OPENXML", "WAITFOR", "DUMP", "OPTION", "WHEN", "ELSE", "OR", "WHERE", "END", "ORDER", "WHILE", "ERRLVL", "OUTER", "WITH", "ESCAPE", "OVER", "WRITETEXT");
		}

		public override bool SupportsIndex
		{
			get { return false; }
		}

		public override bool ColumnNameNeedsQuote
		{
			get { return true; }
		}

		public override bool TableNameNeedsQuote
		{
			get { return true; }
		}

		public override string QuoteTemplate
		{
			get { return "[{0}]"; }
		}

        public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
		{
			return new SqlServerTransformationProvider(dialect, connectionString, defaultSchema ?? DboSchemaName, scope, providerName);
		}

		public override string Quote(string value)
		{
			int firstDotIndex = value.IndexOf('.');
			if (firstDotIndex >= 0)
			{
				string owner = value.Substring(0, firstDotIndex);
				string table = value.Substring(firstDotIndex + 1);
				return (string.Format(QuoteTemplate, owner) + "." + string.Format(QuoteTemplate, table));
			}
			return string.Format(QuoteTemplate, value);
		}

        public override string Default(object defaultValue)
        {
            if (defaultValue.GetType().Equals(typeof (bool)))
            {
                return String.Format("DEFAULT {0}", (bool)defaultValue ? "1" : "0");
            }
            else if (defaultValue.GetType().Equals(typeof(Guid)))
            {
                return "DEFAULT '" + ((Guid) defaultValue).ToString("D") + "'";
            }
            else if (defaultValue.GetType().Equals(typeof(DateTime)))
            {
                return "DEFAULT CONVERT(DateTime,'"
                    + ((DateTime)defaultValue).Year.ToString("D4") + '-'
                    + ((DateTime)defaultValue).Month.ToString("D2") + '-'
                    + ((DateTime)defaultValue).Day.ToString("D2") + ' '
                    + ((DateTime)defaultValue).Hour.ToString("D2") + ':'
                    + ((DateTime)defaultValue).Minute.ToString("D2") + ':'
                    + ((DateTime)defaultValue).Second.ToString("D2") + '.'
                    + ((DateTime)defaultValue).Millisecond.ToString("D3")
                    + "',121)";
            }

            return base.Default(defaultValue);
        }
    }
}
