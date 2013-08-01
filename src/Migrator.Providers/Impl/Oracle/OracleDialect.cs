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
			//RegisterColumnType(DbType.String, 1073741823, "NCLOB");
            RegisterColumnType(DbType.String, int.MaxValue, "NCLOB");
			RegisterColumnType(DbType.Time, "DATE");
			RegisterColumnType(DbType.Guid, "RAW(16)");
			
			// the original Migrator.Net code had this, but it's a bad idea - when
			// apply a "null" migration to a "not-null" field, it just leaves it as "not-null" and silent fails
			// because Oracle doesn't consider ALTER TABLE <table> MODIFY (column <type>) as being a request to make the field null.
			
			//RegisterProperty(ColumnProperty.Null, String.Empty);

            AddReservedWords("ACCOUNT", "ACTIVATE", "ADMIN", "ADVISE", "AFTER", "ALL_ROWS", "ALLOCATE", "ANALYZE", "ARCHIVE", "ARCHIVELOG", "ARRAY", "AT", "AUTHENTICATED", "AUTHORIZATION", "AUTOEXTEND", "AUTOMATIC", "BACKUP", "BECOME", "BEFORE", "BEGIN", "BFILE", "BITMAP", "BLOB", "BLOCK", "BODY", "CACHE", "CACHE_INSTANCES", "CANCEL", "CASCADE", "CAST", "CFILE", "CHAINED", "CHANGE", "CHAR_CS", "CHARACTER", "CHECKPOINT", "CHOOSE", "CHUNK", "CLEAR", "CLOB", "CLONE", "CLOSE", "CLOSE_CACHED_OPEN_CURSORS", "COALESCE", "COLUMNS", "COMMIT", "COMMITTED", "COMPATIBILITY", "COMPILE", "COMPLETE", "COMPOSITE_LIMIT", "COMPUTE", "CONNECT_TIME", "CONSTRAINT", "CONSTRAINTS", "CONTENTS", "CONTINUE", "CONTROLFILE", "CONVERT", "COST", "CPU_PER_CALL", "CPU_PER_SESSION", "CURRENT_SCHEMA", "CURREN_USER", "CURSOR", "CYCLE", "DANGLING", "DATABASE", "DATAFILE", "DATAFILES", "DATAOBJNO", "DBA", "DBHIGH", "DBLOW", "DBMAC", "DEALLOCATE", "DEBUG", "DEC", "DECLARE", "DEFERRABLE", "DEFERRED", "DEGREE", "DEREF", "DIRECTORY", "DISABLE", "DISCONNECT", "DISMOUNT", "DISTRIBUTED", "DML", "DOUBLE", "DUMP", "EACH", "ENABLE", "END", "ENFORCE", "ENTRY", "ESCAPE", "EXCEPT", "EXCEPTIONS", "EXCHANGE", "EXCLUDING", "EXECUTE", "EXPIRE", "EXPLAIN", "EXTENT", "EXTENTS", "EXTERNALLY", "FAILED_LOGIN_ATTEMPTS", "FALSE", "FAST", "FIRST_ROWS", "FLAGGER", "FLOB", "FLUSH", "FORCE", "FOREIGN", "FREELIST", "FREELISTS", "FULL", "FUNCTION", "GLOBAL", "GLOBALLY", "GLOBAL_NAME", "GROUPS", "HASH", "HASHKEYS", "HEADER", "HEAP", "IDGENERATORS", "IDLE_TIME", "IF", "INCLUDING", "INDEXED", "INDEXES", "INDICATOR", "IND_PARTITION", "INITIALLY", "INITRANS", "INSTANCE", "INSTANCES", "INSTEAD", "INT", "INTERMEDIATE", "ISOLATION", "ISOLATION_LEVEL", "KEEP", "KEY", "KILL", "LABEL", "LAYER", "LESS", "LIBRARY", "LIMIT", "LINK", "LIST", "LOB", "LOCAL", "LOCKED", "LOG", "LOGFILE", "LOGGING", "LOGICAL_READS_PER_CALL", "LOGICAL_READS_PER_SESSION", "MANAGE", "MASTER", "MAX", "MAXARCHLOGS", "MAXDATAFILES", "MAXINSTANCES", "MAXLOGFILES", "MAXLOGHISTORY", "MAXLOGMEMBERS", "MAXSIZE", "MAXTRANS", "MAXVALUE", "MIN", "MEMBER", "MINIMUM", "MINEXTENTS", "MINVALUE", "MLS_LABEL_FORMAT", "MOUNT", "MOVE", "MTS_DISPATCHERS", "MULTISET", "NATIONAL", "NCHAR", "NCHAR_CS", "NCLOB", "NEEDED", "NESTED", "NETWORK", "NEW", "NEXT", "NOARCHIVELOG", "NOCACHE", "NOCYCLE", "NOFORCE", "NOLOGGING", "NOMAXVALUE", "NOMINVALUE", "NONE", "NOORDER", "NOOVERRIDE", "NOPARALLEL", "NOPARALLEL", "NOREVERSE", "NORMAL", "NOSORT", "NOTHING", "NUMERIC", "NVARCHAR2", "OBJECT", "OBJNO", "OBJNO_REUSE", "OFF", "OID", "OIDINDEX", "OLD", "ONLY", "OPCODE", "OPEN", "OPTIMAL", "OPTIMIZER_GOAL", "ORGANIZATION", "OSLABEL", "OVERFLOW", "OWN", "PACKAGE", "PARALLEL", "PARTITION", "PASSWORD", "PASSWORD_GRACE_TIME", "PASSWORD_LIFE_TIME", "PASSWORD_LOCK_TIME", "PASSWORD_REUSE_MAX", "PASSWORD_REUSE_TIME", "PASSWORD_VERIFY_FUNCTION", "PCTINCREASE", "PCTTHRESHOLD", "PCTUSED", "PCTVERSION", "PERCENT", "PERMANENT", "PLAN", "PLSQL_DEBUG", "POST_TRANSACTION", "PRECISION", "PRESERVE", "PRIMARY", "PRIVATE", "PRIVATE_SGA", "PRIVILEGE", "PROCEDURE", "PROFILE", "PURGE", "QUEUE", "QUOTA", "RANGE", "RBA", "READ", "READUP", "REAL", "REBUILD", "RECOVER", "RECOVERABLE", "RECOVERY", "REF", "REFERENCES", "REFERENCING", "REFRESH", "REPLACE", "RESET", "RESETLOGS", "RESIZE", "RESTRICTED", "RETURN", "RETURNING", "REUSE", "REVERSE", "ROLE", "ROLES", "ROLLBACK", "RULE", "SAMPLE", "SAVEPOINT", "SB4", "SCAN_INSTANCES", "SCHEMA", "SCN", "SCOPE", "SD_ALL", "SD_INHIBIT", "SD_SHOW", "SEGMENT", "SEG_BLOCK", "SEG_FILE", "SEQUENCE", "SERIALIZABLE", "SESSION_CACHED_CURSORS", "SESSIONS_PER_USER", "SIZE", "SHARED", "SHARED_POOL", "SHRINK", "SKIP", "SKIP_UNUSABLE_INDEXES", "SNAPSHOT", "SOME", "SORT", "SPECIFICATION", "SPLIT", "SQL_TRACE", "STANDBY", "STATEMENT_ID", "STATISTICS", "STOP", "STORAGE", "STORE", "STRUCTURE", "SWITCH", "SYS_OP_ENFORCE_NOT_NULL$", "SYS_OP_NTCIMG$", "SYSDBA", "SYSOPER", "SYSTEM", "TABLES", "TABLESPACE", "TABLESPACE_NO", "TABNO", "TEMPORARY", "THAN", "THE", "THREAD", "TIMESTAMP", "TIME", "TOPLEVEL", "TRACE", "TRACING", "TRANSACTION", "TRANSITIONAL", "TRIGGERS", "TRUE", "TRUNCATE", "TX", "TYPE", "UB2", "UBA", "UNARCHIVED", "UNDO", "UNLIMITED", "UNLOCK", "UNRECOVERABLE", "UNTIL", "UNUSABLE", "UNUSED", "UPDATABLE", "USAGE", "USE", "USING", "VALIDATION", "VALUE", "VARYING", "WHEN", "WITHOUT", "WORK", "WRITE", "WRITEDOWN", "WRITEUP", "XID", "YEAR", "ZONE");
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

        public override bool ConstraintNameNeedsQuote
        {
            get { return false; }
        }
		public override bool TableNameNeedsQuote
		{
			get { return false; }
		}

        public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
		{
			return new OracleTransformationProvider(dialect, connectionString, defaultSchema, scope, providerName);
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
                return String.Format("DEFAULT {0}", (bool)defaultValue ? "1" : "0");
            }

			return base.Default(defaultValue);
		}
	}
}