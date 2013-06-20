using System;
using System.Data;
using Migrator.Framework;
using Migrator.Providers.Impl.Oracle;

namespace Migrator.Providers.Oracle
{
    public class OracleDialect : Dialect
	{
		public OracleDialect()
		{
			RegisterColumnType(DbType.AnsiStringFixedLength, "CHAR(255)");
			RegisterColumnType(DbType.AnsiStringFixedLength, 2000, "CHAR($l)");
			RegisterColumnType(DbType.AnsiString, "VARCHAR2(255)");
			RegisterColumnType(DbType.AnsiString, 2000, "VARCHAR2($l)");
			RegisterColumnType(DbType.AnsiString, 2147483647, "CLOB"); // should use the IType.ClobType
			RegisterColumnType(DbType.Binary, "RAW(2000)");
			RegisterColumnType(DbType.Binary, 2000, "RAW($l)");
			RegisterColumnType(DbType.Binary, 2147483647, "BLOB");
			RegisterColumnType(DbType.Boolean, "NUMBER(1,0)");
			RegisterColumnType(DbType.Byte, "NUMBER(3,0)");
			RegisterColumnType(DbType.Currency, "NUMBER(19,1)");
			RegisterColumnType(DbType.Date, "DATE");
			RegisterColumnType(DbType.DateTime, "TIMESTAMP(4)");
			RegisterColumnType(DbType.Decimal, "NUMBER(19,5)");
			RegisterColumnType(DbType.Decimal, 19, "NUMBER(19, $l)");
			// having problems with both ODP and OracleClient from MS not being able
			// to read values out of a field that is DOUBLE PRECISION
			RegisterColumnType(DbType.Double, "DOUBLE PRECISION"); //"FLOAT(53)" );
			//RegisterColumnType(DbType.Guid, "CHAR(38)");						
			RegisterColumnType(DbType.Int16, "NUMBER(5,0)");
			RegisterColumnType(DbType.Int32, "NUMBER(10,0)");
			RegisterColumnType(DbType.Int64, "NUMBER(20,0)");
			RegisterColumnType(DbType.Single, "FLOAT(24)");
			RegisterColumnType(DbType.StringFixedLength, "NCHAR(255)");
			RegisterColumnType(DbType.StringFixedLength, 2000, "NCHAR($l)");
			RegisterColumnType(DbType.String, "NVARCHAR2(255)");
			RegisterColumnType(DbType.String, 2000, "NVARCHAR2($l)");
			RegisterColumnType(DbType.String, 1073741823, "NCLOB");
			RegisterColumnType(DbType.Time, "DATE");
			RegisterColumnType(DbType.Guid, "RAW(16)");
			
			// the original Migrator.Net code had this, but it's a bad idea - when
			// apply a "null" migration to a "not-null" field, it just leaves it as "not-null" and silent fails
			// because Oracle doesn't consider ALTER TABLE <table> MODIFY (column <type>) as being a request to make the field null.
			
			//RegisterProperty(ColumnProperty.Null, String.Empty);

			AddReservedWords("ABORT", "ABS", "ABSOLUTE", "ACCESS", "ACTION", "ADA", "ADD", "ADMIN", "AFTER", "AGGREGATE", "ALIAS", "ALL", "ALLOCATE", "ALTER", "ANALYSE", "ANALYZE", "AND", "ANY", "ARE",
			                 "ARRAY", "AS", "ASC", "ASENSITIVE", "ASSERTION", "ASSIGNMENT", "ASYMMETRIC", "AT", "ATOMIC", "AUTHORIZATION", "AVG", "BACKWARD", "BEFORE", "BEGIN", "BETWEEN", "BIGINT", "BINARY",
			                 "BIT", "BITVAR", "BIT_LENGTH", "BLOB", "BOOLEAN", "BOTH", "BREADTH", "BY", "C", "CACHE", "CALL", "CALLED", "CARDINALITY", "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG",
			                 "CATALOG_NAME", "CHAIN", "CHAR", "CHARACTER", "CHARACTERISTICS", "CHARACTER_LENGTH", "CHARACTER_SET_CATALOG", "CHARACTER_SET_NAME", "CHARACTER_SET_SCHEMA", "CHAR_LENGTH",
			                 "CHECK", "CHECKED", "CHECKPOINT", "CLASS", "CLASS_ORIGIN", "CLOB", "CLOSE", "CLUSTER", "COALESCE", "COBOL", "COLLATE", "COLLATION", "COLLATION_CATALOG", "COLLATION_NAME",
			                 "COLLATION_SCHEMA", "COLUMN", "COLUMN_NAME", "COMMAND_FUNCTION", "COMMAND_FUNCTION_CODE", "COMMENT", "COMMIT", "COMMITTED", "COMPLETION", "CONDITION_NUMBER", "CONNECT",
			                 "CONNECTION", "CONNECTION_NAME", "CONSTRAINT", "CONSTRAINTS", "CONSTRAINT_CATALOG", "CONSTRAINT_NAME", "CONSTRAINT_SCHEMA", "CONSTRUCTOR", "CONTAINS", "CONTENTS", "CONTINUE",
			                 "CONVERSION",
			                 "CONVERT", "COPY", "CORRESPONDING", "COUNT", "CREATE", "CREATEDB", "CREATEUSER", "CROSS", "CUBE", "CURRENT", "CURRENT_DATE", "CURRENT_PATH", "CURRENT_ROLE", "CURRENT_TIME",
			                 "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR", "CURSOR_NAME", "CYCLE", "DATA", "DATABASE", "DATE", "DATETIME_INTERVAL_CODE", "DATETIME_INTERVAL_PRECISION", "DAY", "DEALLOCATE",
			                 "DEC", "DECIMAL", "DECLARE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DEFINED", "DEFINER", "DELETE", "DELIMITER", "DELIMITERS", "DEPTH", "DEREF", "DESC", "DESCRIBE", "DESCRIPTOR",
			                 "DESTROY", "DESTRUCTOR", "DETERMINISTIC", "DIAGNOSTICS", "DICTIONARY", "DISCONNECT", "DISPATCH", "DISTINCT", "DO", "DOMAIN", "DOUBLE", "DROP", "DYNAMIC", "DYNAMIC_FUNCTION",
			                 "DYNAMIC_FUNCTION_CODE", "EACH", "ELSE", "ENCODING", "ENCRYPTED", "END", "END-EXEC", "EQUALS", "ESCAPE", "EVERY", "EXCEPT", "EXCEPTION", "EXCLUSIVE", "EXEC", "EXECUTE",
			                 "EXISTING", "EXISTS", "EXPLAIN", "EXTERNAL", "EXTRACT", "FALSE", "FETCH", "FINAL", "FIRST", "FLOAT", "FOR", "FORCE", "FOREIGN", "FORTRAN", "FORWARD", "FOUND", "FREE", "FREEZE",
			                 "FROM", "FULL", "FUNCTION", "G", "GENERAL", "GENERATED", "GET", "GLOBAL", "GO", "GOTO", "GRANT", "GRANTED", "GROUP", "GROUPING", "HANDLER", "HAVING", "HIERARCHY", "HOLD", "HOST",
			                 "HOUR", "IDENTITY", "IGNORE", "ILIKE", "IMMEDIATE", "IMMUTABLE", "IMPLEMENTATION", "IMPLICIT", "IN", "INCREMENT", "INDEX", "INDICATOR", "INFIX", "INHERITS", "INITIALIZE",
			                 "INITIALLY", "INNER", "INOUT", "INPUT", "INSENSITIVE", "INSERT", "INSTANCE", "INSTANTIABLE", "INSTEAD", "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO", "INVOKER", "IS",
			                 "ISNULL", "ISOLATION", "ITERATE", "JOIN", "K", "KEY", "KEY_MEMBER", "KEY_TYPE", "LANCOMPILER", "LANGUAGE", "LARGE", "LAST", "LATERAL", "LEADING", "LEFT", "LENGTH", "LESS",
			                 "LEVEL", "LIKE", "LIMIT", "LISTEN", "LOAD", "LOCAL", "LOCALTIME", "LOCALTIMESTAMP", "LOCATION", "LOCATOR", "LOCK", "LOWER", "M", "MAP", "MATCH", "MAX", "MAXVALUE",
			                 "MESSAGE_LENGTH", "MESSAGE_OCTET_LENGTH", "MESSAGE_TEXT", "METHOD", "MIN", "MINUTE", "MINVALUE", "MOD", "MODE", "MODIFIES", "MODIFY", "MODULE", "MONTH", "MORE", "MOVE", "MUMPS",
			                 "NAME", "NAMES", "NATIONAL", "NATURAL", "NCHAR", "NCLOB", "NEW", "NEXT", "NO", "NOCREATEDB", "NOCREATEUSER", "NONE", "NOT", "NOTHING", "NOTIFY", "NOTNULL", "NULL", "NULLABLE",
			                 "NULLIF", "NUMBER", "NUMERIC", "OBJECT", "OCTET_LENGTH", "OF", "OFF", "OFFSET", "OIDS", "OLD", "ON", "ONLY", "OPEN", "OPERATION", "OPERATOR", "OPTION", "OPTIONS", "OR", "ORDER",
			                 "ORDINALITY", "OUT", "OUTER", "OUTPUT", "OVERLAPS", "OVERLAY", "OVERRIDING", "OWNER", "PAD", "PARAMETER", "PARAMETERS", "PARAMETER_MODE", "PARAMETER_NAME",
			                 "PARAMETER_ORDINAL_POSITION", "PARAMETER_SPECIFIC_CATALOG", "PARAMETER_SPECIFIC_NAME", "PARAMETER_SPECIFIC_SCHEMA", "PARTIAL", "PASCAL", "PASSWORD", "PATH", "PENDANT", "PLACING",
			                 "PLI", "POSITION", "POSTFIX", "PRECISION", "PREFIX", "PREORDER", "PREPARE", "PRESERVE", "PRIMARY", "PRIOR", "PRIVILEGES", "PROCEDURAL", "PROCEDURE", "PUBLIC", "READ", "READS",
			                 "REAL", "RECHECK", "RECURSIVE", "REF", "REFERENCES", "REFERENCING", "REINDEX", "RELATIVE", "RENAME", "REPEATABLE", "REPLACE", "RESET", "RESTRICT", "RESULT", "RETURN",
			                 "RETURNED_LENGTH", "RETURNED_OCTET_LENGTH", "RETURNED_SQLSTATE", "RETURNS", "REVOKE", "RIGHT", "ROLE", "ROLLBACK", "ROLLUP", "ROUTINE", "ROUTINE_CATALOG", "ROUTINE_NAME",
			                 "ROUTINE_SCHEMA", "ROW", "ROWS", "ROW_COUNT", "RULE", "SAVEPOINT", "SCALE", "SCHEMA", "SCHEMA_NAME", "SCOPE", "SCROLL", "SEARCH", "SECOND", "SECTION", "SECURITY", "SELECT",
			                 "SELF", "SENSITIVE", "SEQUENCE", "SERIALIZABLE", "SERVER_NAME", "SESSION", "SESSION_USER", "SET", "SETOF", "SETS", "SHARE", "SHOW", "SIMILAR", "SIMPLE", "SIZE", "SMALLINT",
			                 "SOME", "SOURCE", "SPACE", "SPECIFIC", "SPECIFICTYPE", "SPECIFIC_NAME", "SQL", "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "STABLE", "START",
			                 "STATEMENT", "STATIC", "STATISTICS", "STDIN", "STDOUT", "STORAGE", "STRICT", "STRUCTURE", "STYLE", "SUBCLASS_ORIGIN", "SUBLIST", "SUBSTRING", "SUM", "SYMMETRIC", "SYSID",
			                 "SYSTEM", "SYSTEM_USER", "TABLE", "TABLE_NAME", "TEMP", "TEMPLATE", "TEMPORARY", "TERMINATE", "THAN", "THEN", "TIME", "TIMESTAMP", "TIMEZONE_HOUR", "TIMEZONE_MINUTE", "TO",
			                 "TOAST", "TRAILING", "TRANSACTION", "TRANSACTIONS_COMMITTED", "TRANSACTIONS_ROLLED_BACK", "TRANSACTION_ACTIVE", "TRANSFORM", "TRANSFORMS", "TRANSLATE", "TRANSLATION", "TREAT",
			                 "TRIGGER", "TRIGGER_CATALOG", "TRIGGER_SCHEMA", "TRIM", "TRUE", "TRUNCATE", "TRUSTED", "TYPE", "UNCOMMITTED", "UNDER", "UNENCRYPTED", "UNION", "UNIQUE",
			                 "UNKNOWN", "UNLISTEN", "UNNAMED", "UNNEST", "UNTIL", "UPDATE", "UPPER", "USAGE", "USER", "USER_DEFINED_TYPE_CATALOG", "USER_DEFINED_TYPE_NAME", "USER_DEFINED_TYPE_SCHEMA",
			                 "USING", "VACUUM", "VALID", "VALIDATOR", "VALUE", "VALUES", "VARCHAR", "VARIABLE", "VARYING", "VERBOSE", "VERSION", "VIEW", "VOLATILE", "WHEN", "WHENEVER", "WHERE", "WITH",
			                 "WITHOUT", "WORK", "WRITE", "YEAR", "ZONE");
		}

		// in Oracle, this:  ALTER TABLE EXTERNALSYSTEMREFERENCES MODIFY (TestScriptId RAW(16)) will no make the column nullable, it just leaves it at it's current null/not-null state
		
		public override bool NeedsNullForNullableWhenAlteringTable
		{
			get { return true; }
		}

		public override bool ColumnNameNeedsQuote
		{
			get { return false; }
		}

		public override bool TableNameNeedsQuote
		{
			get { return false; }
		}

		public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema)
		{
			return new OracleTransformationProvider(dialect, connectionString, defaultSchema);
		}
		
		public override ColumnPropertiesMapper GetColumnMapper(Column column)
		{
			string type = column.Size > 0 ? GetTypeName(column.Type, column.Size) : GetTypeName(column.Type);
			if (!IdentityNeedsType && column.IsIdentity)
				type = String.Empty;

			return new OracleColumnPropertiesMapper(this, type);
		}

		public override string Default(object defaultValue)
		{
			if (defaultValue.GetType().Equals(typeof(bool)))
			{
				defaultValue = ((bool)defaultValue) ? 1 : 0;
			}
			return String.Format("DEFAULT {0}", defaultValue);
		}
	}
}