using Migrator.Framework;
using System;
using System.Data;

namespace Migrator.Providers.PostgreSQL
{
	public class PostgreSQLDialect : Dialect
	{
		public PostgreSQLDialect()
		{
			RegisterColumnType(DbType.AnsiStringFixedLength, "char(255)");
			RegisterColumnType(DbType.AnsiStringFixedLength, 1073741823, "char($l)");
			RegisterColumnType(DbType.AnsiString, "varchar(255)");
			RegisterColumnType(DbType.AnsiString, 8000, "varchar($l)");
			RegisterColumnType(DbType.AnsiString, int.MaxValue, "text");
			RegisterColumnType(DbType.Binary, "bytea");
			RegisterColumnType(DbType.Binary, 2147483647, "bytea");
			RegisterColumnType(DbType.Boolean, "boolean");
			RegisterColumnType(DbType.Byte, "int2");
			RegisterColumnType(DbType.Currency, "decimal(16,4)");
			RegisterColumnType(DbType.Date, "date");
			RegisterColumnType(DbType.DateTime, "timestamptz");
			RegisterColumnType(DbType.DateTime2, "timestamptz");
			RegisterColumnType(DbType.DateTimeOffset, "timestamptz");
			RegisterColumnType(DbType.Decimal, "decimal(19,5)");
			RegisterColumnType(DbType.Decimal, 19, "decimal(18, $l)");
			RegisterColumnTypeWithParameters(DbType.Decimal, "decimal({precision}, {scale})");
			RegisterColumnType(DbType.Double, "float8");
			RegisterColumnType(DbType.Int16, "int2");
			RegisterColumnType(DbType.Int32, "int4");
			RegisterColumnType(DbType.Int64, "int8");
			RegisterColumnType(DbType.UInt16, "int4");
			RegisterColumnType(DbType.UInt32, "int8");
			RegisterColumnType(DbType.UInt64, "decimal(20,0)");
			RegisterColumnType(DbType.Single, "float4");
			RegisterColumnType(DbType.StringFixedLength, "char(255)");
			RegisterColumnType(DbType.StringFixedLength, 1073741823, "char($l)");
			RegisterColumnType(DbType.String, "varchar(255)");
			RegisterColumnType(DbType.String, 4000, "varchar($l)");
			RegisterColumnType(DbType.String, int.MaxValue, "text");
			RegisterColumnType(DbType.Time, "time");
			RegisterColumnType(DbType.Guid, "uuid");

			RegisterProperty(ColumnProperty.Identity, "serial");

			AddReservedWords("ABS", "ABSOLUTE", "ACCESS", "ACTION", "ADA", "ADD", "ADMIN", "AFTER", "AGGREGATE", "ALIAS", "ALL", "ALLOCATE", "ALTER", "ANALYSE", "ANALYZE", "AND", "ANY", "ARE",
							 "ARRAY", "AS", "ASC", "ASENSITIVE", "ASSERTION", "ASSIGNMENT", "ASYMMETRIC", "AT", "ATOMIC", "AUTHORIZATION", "AVG", "BACKWARD", "BEFORE", "BEGIN", "BETWEEN", "BIGINT", "BINARY",
							 "BIT", "BITVAR", "BIT_LENGTH", "BLOB", "BOOLEAN", "BOTH", "BREADTH", "BY", "C", "CACHE", "CALL", "CALLED", "CARDINALITY", "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG",
							 "CATALOG_NAME", "CHAIN", "CHAR", "CHARACTER", "CHARACTERISTICS", "CHARACTER_LENGTH", "CHARACTER_SET_CATALOG", "CHARACTER_SET_NAME", "CHARACTER_SET_SCHEMA", "CHAR_LENGTH",
							 "CHECK", "CHECKED", "CHECKPOINT", "CLASS", "CLASS_ORIGIN", "CLOB", "CLOSE", "CLUSTER", "COALESCE", "COBOL", "COLLATE", "COLLATION", "COLLATION_CATALOG", "COLLATION_NAME",
							 "COLLATION_SCHEMA", "COLUMN", "COLUMN_NAME", "COMMAND_FUNCTION", "COMMAND_FUNCTION_CODE", "COMMENT", "COMMIT", "COMMITTED", "COMPLETION", "CONDITION_NUMBER", "CONNECT",
							 "CONNECTION", "CONNECTION_NAME", "CONSTRAINT", "CONSTRAINTS", "CONSTRAINT_CATALOG", "CONSTRAINT_NAME", "CONSTRAINT_SCHEMA", "CONSTRUCTOR", "CONTAINS", "CONTENTS", "CONTINUE",
							 "CONVERSION",
							 "CONVERT", "COPY", "CORRESPONDING", "COUNT", "CREATE", "CREATEDB", "CREATEUSER", "CROSS", "CUBE", "CURRENT", "CURRENT_DATE", "CURRENT_PATH", "CURRENT_ROLE", "CURRENT_TIME",
							 "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR", "CURSOR_NAME", "CYCLE", "DATABASE", "DATE", "DATETIME_INTERVAL_CODE", "DATETIME_INTERVAL_PRECISION", "DAY", "DEALLOCATE",
							 "DEC", "DECIMAL", "DECLARE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DEFINED", "DEFINER", "DELETE", "DELIMITER", "DELIMITERS", "DEPTH", "DEREF", "DESC", "DESCRIBE", "DESCRIPTOR",
							 "DESTROY", "DESTRUCTOR", "DETERMINISTIC", "DIAGNOSTICS", "DICTIONARY", "DISCONNECT", "DISPATCH", "DISTINCT", "DO", "DOMAIN", "DOUBLE", "DROP", "DYNAMIC", "DYNAMIC_FUNCTION",
							 "DYNAMIC_FUNCTION_CODE", "EACH", "ELSE", "ENCODING", "ENCRYPTED", "END", "END-EXEC", "EQUALS", "ESCAPE", "EVERY", "EXCEPT", "EXCEPTION", "EXCLUSIVE", "EXEC", "EXECUTE",
							 "EXISTING", "EXISTS", "EXPLAIN", "EXTERNAL", "EXTRACT", "FALSE", "FETCH", "FINAL", "FIRST", "FLOAT", "FOR", "FORCE", "FOREIGN", "FORTRAN", "FORWARD", "FOUND", "FREE", "FREEZE",
							 "FROM", "FULL", "FUNCTION", "G", "GENERAL", "GENERATED", "GET", "GLOBAL", "GO", "GOTO", "GRANT", "GRANTED", "GROUP", "GROUPING", "HANDLER", "HAVING", "HIERARCHY", "HOLD", "HOST",
							 "HOUR", "IDENTITY", "IGNORE", "ILIKE", "IMMEDIATE", "IMMUTABLE", "IMPLEMENTATION", "IMPLICIT", "IN", "INCREMENT", "INDEX", "INDICATOR", "INFIX", "INHERITS", "INITIALIZE",
							 "INITIALLY", "INNER", "INOUT", "INPUT", "INSENSITIVE", "INSERT", "INSTANCE", "INSTANTIABLE", "INSTEAD", "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO", "INVOKER", "IS",
							 "ISNULL", "ISOLATION", "ITERATE", "JOIN", "K", "KEY", "KEY_MEMBER", "KEY_TYPE", "LANCOMPILER", "LANGUAGE", "LARGE", "LAST", "LATERAL", "LEADING", "LEFT", "LENGTH", "LESS",
							 "LEVEL", "LIKE", "LIMIT", "LISTEN", "LOAD", "LOCAL", "LOCALTIME", "LOCALTIMESTAMP", "LOCATOR", "LOCK", "LOWER", "M", "MAP", "MATCH", "MAX", "MAXVALUE",
							 "MESSAGE_LENGTH", "MESSAGE_OCTET_LENGTH", "MESSAGE_TEXT", "METHOD", "MIN", "MINUTE", "MINVALUE", "MOD", "MODE", "MODIFIES", "MODIFY", "MODULE", "MONTH", "MORE", "MOVE", "MUMPS",
							 "NAMES", "NATIONAL", "NATURAL", "NCHAR", "NCLOB", "NEW", "NEXT", "NO", "NOCREATEDB", "NOCREATEUSER", "NONE", "NOT", "NOTHING", "NOTIFY", "NOTNULL", "NULL", "NULLABLE",
							 "NULLIF", "NUMBER", "NUMERIC", "OBJECT", "OCTET_LENGTH", "OF", "OFF", "OFFSET", "OIDS", "OLD", "ON", "ONLY", "OPEN", "OPERATION", "OPERATOR", "OPTION", "OPTIONS", "OR", "ORDER",
							 "ORDINALITY", "OUT", "OUTER", "OUTPUT", "OVERLAPS", "OVERLAY", "OVERRIDING", "OWNER", "PAD", "PARAMETER", "PARAMETERS", "PARAMETER_MODE", "PARAMETER_NAME",
							 "PARAMETER_ORDINAL_POSITION", "PARAMETER_SPECIFIC_CATALOG", "PARAMETER_SPECIFIC_NAME", "PARAMETER_SPECIFIC_SCHEMA", "PARTIAL", "PASCAL", "PATH", "PENDANT", "PLACING",
							 "PLI", "POSITION", "POSTFIX", "PRECISION", "PREFIX", "PREORDER", "PREPARE", "PRESERVE", "PRIMARY", "PRIOR", "PRIVILEGES", "PROCEDURAL", "PROCEDURE", "PUBLIC", "READ", "READS",
							 "REAL", "RECHECK", "RECURSIVE", "REF", "REFERENCES", "REFERENCING", "REINDEX", "RELATIVE", "RENAME", "REPEATABLE", "REPLACE", "RESET", "RESTRICT", "RESULT", "RETURN",
							 "RETURNED_LENGTH", "RETURNED_OCTET_LENGTH", "RETURNED_SQLSTATE", "RETURNS", "REVOKE", "RIGHT", "ROLE", "ROLLBACK", "ROLLUP", "ROUTINE", "ROUTINE_CATALOG", "ROUTINE_NAME",
							 "ROUTINE_SCHEMA", "ROW", "ROWS", "ROW_COUNT", "RULE", "SAVEPOINT", "SCALE", "SCHEMA", "SCHEMA_NAME", "SCOPE", "SCROLL", "SEARCH", "SECOND", "SECTION", "SECURITY", "SELECT",
							 "SELF", "SENSITIVE", "SEQUENCE", "SERIALIZABLE", "SERVER_NAME", "SESSION", "SESSION_USER", "SET", "SETOF", "SETS", "SHARE", "SHOW", "SIMILAR", "SIMPLE", "SIZE", "SMALLINT",
							 "SOME", "SPACE", "SPECIFIC", "SPECIFICTYPE", "SPECIFIC_NAME", "SQL", "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "STABLE", "START",
							 "STATEMENT", "STATIC", "STATISTICS", "STDIN", "STDOUT", "STORAGE", "STRICT", "STRUCTURE", "STYLE", "SUBCLASS_ORIGIN", "SUBLIST", "SUBSTRING", "SUM", "SYMMETRIC", "SYSID",
							 "SYSTEM", "SYSTEM_USER", "TABLE", "TABLE_NAME", "TEMP", "TEMPLATE", "TEMPORARY", "TERMINATE", "THAN", "THEN", "TIME", "TIMESTAMP", "TIMEZONE_HOUR", "TIMEZONE_MINUTE", "TO",
							 "TOAST", "TRAILING", "TRANSACTION", "TRANSACTIONS_COMMITTED", "TRANSACTIONS_ROLLED_BACK", "TRANSACTION_ACTIVE", "TRANSFORM", "TRANSFORMS", "TRANSLATE", "TRANSLATION", "TREAT",
							 "TRIGGER", "TRIGGER_CATALOG", "TRIGGER_SCHEMA", "TRIM", "TRUE", "TRUNCATE", "TRUSTED", "UNCOMMITTED", "UNDER", "UNENCRYPTED", "UNION", "UNIQUE",
							 "UNKNOWN", "UNLISTEN", "UNNAMED", "UNNEST", "UNTIL", "UPDATE", "UPPER", "USAGE", "USER", "USER_DEFINED_TYPE_CATALOG", "USER_DEFINED_TYPE_NAME", "USER_DEFINED_TYPE_SCHEMA",
							 "USING", "VACUUM", "VALID", "VALIDATOR", "VALUES", "VARCHAR", "VARIABLE", "VARYING", "VERBOSE", "VERSION", "VIEW", "VOLATILE", "WHEN", "WHENEVER", "WHERE", "WITH",
							 "WITHOUT", "WORK", "WRITE", "XMAX", "XMIN", "YEAR", "ZONE");
		}

		public override bool TableNameNeedsQuote
		{
			get { return false; }
		}

		public override bool ConstraintNameNeedsQuote
		{
			get { return false; }
		}

		public override bool IdentityNeedsType
		{
			get { return false; }
		}

		public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
		{
			return new PostgreSQLTransformationProvider(dialect, connectionString, defaultSchema, scope, providerName);
		}

		public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
		{
			return new PostgreSQLTransformationProvider(dialect, connection, defaultSchema, scope, providerName);
		}

		public override string SqlForProperty(ColumnProperty property, Column column)
		{
			if (property == ColumnProperty.Identity && (column.Type == DbType.Int64 || column.Type == DbType.UInt32 || column.Type == DbType.UInt64))
				return "bigserial";
			return base.SqlForProperty(property, column);
		}
	}
}
