using System;
using System.Data;
using Migrator.Framework;

namespace Migrator.Providers.Mysql;

public class MysqlDialect : Dialect
{
    public MysqlDialect()
    {
        // TODO: As per http://dev.mysql.com/doc/refman/5.0/en/char.html 5.0.3 and above
        // can handle varchar(n) up to a length OF 65,535 - so the limit of 255 should no longer apply.

        RegisterColumnType(DbType.AnsiStringFixedLength, "CHAR(255)");
        RegisterColumnType(DbType.AnsiStringFixedLength, 255, "CHAR($l)");
        RegisterColumnType(DbType.AnsiStringFixedLength, 65535, "TEXT");
        RegisterColumnType(DbType.AnsiStringFixedLength, 16777215, "MEDIUMTEXT");
        RegisterColumnType(DbType.AnsiString, "VARCHAR(255)");
        RegisterColumnType(DbType.AnsiString, 255, "VARCHAR($l)");
        RegisterColumnType(DbType.AnsiString, 256, "VARCHAR(255)");
        RegisterColumnType(DbType.AnsiString, 65535, "TEXT");
        RegisterColumnType(DbType.AnsiString, 16777215, "MEDIUMTEXT");
        RegisterColumnType(DbType.Binary, "LONGBLOB");
        RegisterColumnType(DbType.Binary, 127, "TINYBLOB");
        RegisterColumnType(DbType.Binary, 65535, "BLOB");
        RegisterColumnType(DbType.Binary, 16777215, "MEDIUMBLOB");
        RegisterColumnType(DbType.Boolean, "TINYINT(1)");
        RegisterColumnType(DbType.Byte, "TINYINT UNSIGNED");
        RegisterColumnType(DbType.Currency, "MONEY");
        RegisterColumnType(DbType.Date, "DATE");
        RegisterColumnType(DbType.DateTime, "DATETIME");
        RegisterColumnType(DbType.DateTime2, "DATETIME");
        RegisterColumnType(DbType.DateTimeOffset, "DATETIME");
        RegisterColumnType(DbType.Decimal, "NUMERIC(19,5)");
        RegisterColumnType(DbType.Decimal, 19, "NUMERIC(19, $l)");
        RegisterColumnType(DbType.Double, "DOUBLE");
        RegisterColumnType(DbType.Guid, "VARCHAR(40)");
        RegisterColumnType(DbType.Int16, "SMALLINT");
        RegisterColumnType(DbType.Int32, "INTEGER");
        RegisterColumnType(DbType.Int64, "BIGINT");
        RegisterColumnType(MigratorDbType.Interval, "BIGINT");
        RegisterColumnType(DbType.UInt16, "INTEGER");
        RegisterColumnType(DbType.UInt32, "BIGINT");
        RegisterColumnType(DbType.UInt64, "NUMERIC(20,0)");
        RegisterColumnType(DbType.Single, "FLOAT");
        RegisterColumnType(DbType.StringFixedLength, "CHAR(255)");
        RegisterColumnType(DbType.StringFixedLength, 255, "CHAR($l)");
        RegisterColumnType(DbType.StringFixedLength, 16383, "TEXT");
        RegisterColumnType(DbType.StringFixedLength, 5592415, "MEDIUMTEXT");
        RegisterColumnType(DbType.String, "VARCHAR(255)");
        RegisterColumnType(DbType.String, 16383, "VARCHAR($l)");
        RegisterColumnType(DbType.String, 5592415, "MEDIUMTEXT");
        RegisterColumnType(DbType.String, int.MaxValue, "LONGTEXT");
        RegisterColumnType(DbType.Time, "TIME");

        RegisterProperty(ColumnProperty.Unsigned, "UNSIGNED");
        RegisterProperty(ColumnProperty.Identity, "AUTO_INCREMENT");
        RegisterProperty(ColumnProperty.CaseSensitive, "BINARY");

        RegisterUnsignedCompatible(DbType.Int16);
        RegisterUnsignedCompatible(DbType.Int32);
        RegisterUnsignedCompatible(DbType.Int64);
        RegisterUnsignedCompatible(DbType.Decimal);
        RegisterUnsignedCompatible(DbType.Double);
        RegisterUnsignedCompatible(DbType.Single);

        AddReservedWords("ACCESSIBLE", "ACTION", "ADD",
            "AFTER", "AGAINST", "AGGREGATE",
            "ALGORITHM", "ALL", "ALTER",
            "ANALYZE", "AND", "ANY",
            "AS", "ASC", "ASCII",
            "ASENSITIVE", "AT", "AUTHORS",
            "AUTOEXTEND_SIZE", "AUTO_INCREMENT", "AVG",
            "AVG_ROW_LENGTH", "BACKUP", "BEFORE",
            "BEGIN", "BETWEEN", "BIGINT",
            "BINARY", "BINLOG", "BIT",
            "BLOB", "BLOCK", "BOOL",
            "BOOLEAN", "BOTH", "BTREE",
            "BY", "BYTE", "CACHE",
            "CALL", "CASCADE", "CASCADED",
            "CASE", "CATALOG_NAME", "CHAIN",
            "CHANGE", "CHANGED", "CHAR",
            "CHARACTER", "CHARSET", "CHECK",
            "CHECKSUM", "CIPHER", "CLASS_ORIGIN",
            "CLIENT", "CLOSE", "COALESCE",
            "CODE", "COLLATE", "COLLATION",
            "COLUMN", "COLUMNS", "COLUMN_NAME",
            "COMMENT", "COMMIT", "COMMITTED",
            "COMPACT", "COMPLETION", "COMPRESSED",
            "CONCURRENT", "CONDITION", "CONNECTION",
            "CONSISTENT", "CONSTRAINT", "CONSTRAINT_CATALOG",
            "CONSTRAINT_NAME", "CONSTRAINT_SCHEMA", "CONTAINS",
            "CONTEXT", "CONTINUE", "CONTRIBUTORS",
            "CONVERT", "CPU", "CREATE",
            "CROSS", "CUBE", "CURRENT_DATE",
            "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER",
            "CURSOR", "CURSOR_NAME", "DATA",
            "DATABASE", "DATABASES", "DATAFILE",
            "DATE", "DATETIME", "DAY",
            "DAY_HOUR", "DAY_MICROSECOND", "DAY_MINUTE",
            "DAY_SECOND", "DEALLOCATE", "DEC",
            "DECIMAL", "DECLARE", "DEFAULT",
            "DEFINER", "DELAYED", "DELAY_KEY_WRITE",
            "DELETE", "DESC", "DESCRIBE",
            "DES_KEY_FILE", "DETERMINISTIC", "DIRECTORY",
            "DISABLE", "DISCARD", "DISK",
            "DISTINCT", "DISTINCTROW", "DIV",
            "DO", "DOUBLE", "DROP",
            "DUAL", "DUMPFILE", "DUPLICATE",
            "DYNAMIC", "EACH", "ELSE",
            "ELSEIF", "ENABLE", "ENCLOSED",
            "END", "ENDS", "ENGINE",
            "ENGINES", "ENUM", "ERROR",
            "ERRORS", "ESCAPE", "ESCAPED",
            "EVENT", "EVENTS", "EVERY",
            "EXECUTE", "EXISTS", "EXIT",
            "EXPANSION", "EXPLAIN", "EXTENDED",
            "EXTENT_SIZE", "FALSE", "FAST",
            "FAULTS", "FETCH", "FIELDS",
            "FILE", "FIRST", "FIXED",
            "FLOAT", "FLOAT4", "FLOAT8",
            "FLUSH", "FOR", "FORCE",
            "FOREIGN", "FOUND", "FRAC_SECOND",
            "FROM", "FULL", "FULLTEXT",
            "FUNCTION", "GENERAL", "GEOMETRY",
            "GEOMETRYCOLLECTION", "GET_FORMAT", "GLOBAL",
            "GRANT", "GRANTS", "GROUP",
            "HANDLER", "HASH", "HAVING",
            "HELP", "HIGH_PRIORITY", "HOST",
            "HOSTS", "HOUR", "HOUR_MICROSECOND",
            "HOUR_MINUTE", "HOUR_SECOND", "IDENTIFIED",
            "IF", "IGNORE", "IGNORE_SERVER_IDS",
            "IMPORT", "IN", "INDEX",
            "INDEXES", "INFILE", "INITIAL_SIZE",
            "INNER", "INNOBASE", "INNODB",
            "INOUT", "INSENSITIVE", "INSERT",
            "INSERT_METHOD", "INSTALL", "INT",
            "INT1", "INT2", "INT3",
            "INT4", "INT8", "INTEGER",
            "INTERVAL", "INTO", "INVOKER",
            "IO", "IO_THREAD", "IPC",
            "IS", "ISOLATION", "ISSUER",
            "ITERATE", "JOIN", "KEY",
            "KEYS", "KEY_BLOCK_SIZE", "KILL",
            "LANGUAGE", "LAST", "LEADING",
            "LEAVE", "LEAVES", "LEFT",
            "LESS", "LEVEL", "LIKE",
            "LIMIT", "LINEAR", "LINES",
            "LINESTRING", "LIST", "LOAD",
            "LOCAL", "LOCALTIME", "LOCALTIMESTAMP",
            "LOCK", "LOCKS", "LOGFILE",
            "LOGS", "LONG", "LONGBLOB",
            "LONGTEXT", "LOOP", "LOW_PRIORITY",
            "MASTER", "MASTER_CONNECT_RETRY", "MASTER_HEARTBEAT_PERIOD",
            "MASTER_HOST", "MASTER_LOG_FILE", "MASTER_LOG_POS",
            "MASTER_PASSWORD", "MASTER_PORT", "MASTER_SERVER_ID",
            "MASTER_SSL", "MASTER_SSL_CA", "MASTER_SSL_CAPATH",
            "MASTER_SSL_CERT", "MASTER_SSL_CIPHER", "MASTER_SSL_KEY",
            "MASTER_SSL_VERIFY_SERVER_CERT", "MASTER_USER", "MATCH",
            "MAXVALUE", "MAX_CONNECTIONS_PER_HOUR", "MAX_QUERIES_PER_HOUR",
            "MAX_ROWS", "MAX_SIZE", "MAX_UPDATES_PER_HOUR",
            "MAX_USER_CONNECTIONS", "MEDIUM", "MEDIUMBLOB",
            "MEDIUMINT", "MEDIUMTEXT", "MEMORY",
            "MERGE", "MESSAGE_TEXT", "MICROSECOND",
            "MIDDLEINT", "MIGRATE", "MINUTE",
            "MINUTE_MICROSECOND", "MINUTE_SECOND", "MIN_ROWS",
            "MOD", "MODE", "MODIFIES",
            "MODIFY", "MONTH", "MULTILINESTRING",
            "MULTIPOINT", "MULTIPOLYGON", "MUTEX",
            "MYSQL_ERRNO", "NAME", "NAMES",
            "NATIONAL", "NATURAL", "NCHAR",
            "NDB", "NDBCLUSTER", "NEW",
            "NEXT", "NO", "NODEGROUP",
            "NONE", "NOT", "NO_WAIT",
            "NO_WRITE_TO_BINLOG", "NULL", "NUMERIC",
            "NVARCHAR", "OFFSET", "OLD_PASSWORD",
            "ON", "ONE", "ONE_SHOT",
            "OPEN", "OPTIMIZE", "OPTION",
            "OPTIONALLY", "OPTIONS", "OR",
            "ORDER", "OUT", "OUTER",
            "OUTFILE", "OWNER", "PACK_KEYS",
            "PAGE", "PARSER", "PARTIAL",
            "PARTITION", "PARTITIONING", "PARTITIONS",
            "PASSWORD", "PHASE", "PLUGIN",
            "PLUGINS", "POINT", "POLYGON",
            "PORT", "PRECISION", "PREPARE",
            "PRESERVE", "PREV", "PRIMARY",
            "PRIVILEGES", "PROCEDURE", "PROCESSLIST",
            "PROFILE", "PROFILES", "PROXY",
            "PURGE", "QUARTER", "QUERY",
            "QUICK", "RANGE", "READ",
            "READS", "READ_ONLY", "READ_WRITE",
            "REAL", "REBUILD", "RECOVER",
            "REDOFILE", "REDO_BUFFER_SIZE", "REDUNDANT",
            "REFERENCES", "REGEXP", "RELAY",
            "RELAYLOG", "RELAY_LOG_FILE", "RELAY_LOG_POS",
            "RELAY_THREAD", "RELEASE", "RELOAD",
            "REMOVE", "RENAME", "REORGANIZE",
            "REPAIR", "REPEAT", "REPEATABLE",
            "REPLACE", "REPLICATION", "REQUIRE",
            "RESET", "RESIGNAL", "RESTORE",
            "RESTRICT", "RESUME", "RETURN",
            "RETURNS", "REVOKE", "RIGHT",
            "RLIKE", "ROLLBACK", "ROLLUP",
            "ROUTINE", "ROW", "ROWS",
            "ROW_FORMAT", "RTREE", "SAVEPOINT",
            "SCHEDULE", "SCHEMA", "SCHEMAS",
            "SCHEMA_NAME", "SECOND", "SECOND_MICROSECOND",
            "SECURITY", "SELECT", "SENSITIVE",
            "SEPARATOR", "SERIAL", "SERIALIZABLE",
            "SERVER", "SESSION", "SET",
            "SHARE", "SHOW", "SHUTDOWN",
            "SIGNAL", "SIGNED", "SIMPLE",
            "SLAVE", "SLOW", "SMALLINT",
            "SNAPSHOT", "SOCKET", "SOME",
            "SONAME", "SOUNDS", "SOURCE",
            "SPATIAL", "SPECIFIC", "SQL",
            "SQLEXCEPTION", "SQLSTATE", "SQLWARNING",
            "SQL_BIG_RESULT", "SQL_BUFFER_RESULT", "SQL_CACHE",
            "SQL_CALC_FOUND_ROWS", "SQL_NO_CACHE", "SQL_SMALL_RESULT",
            "SQL_THREAD", "SQL_TSI_DAY", "SQL_TSI_FRAC_SECOND",
            "SQL_TSI_HOUR", "SQL_TSI_MINUTE", "SQL_TSI_MONTH",
            "SQL_TSI_QUARTER", "SQL_TSI_SECOND", "SQL_TSI_WEEK",
            "SQL_TSI_YEAR", "SSL", "START",
            "STARTING", "STARTS", "STATUS",
            "STOP", "STORAGE", "STRAIGHT_JOIN",
            "STRING", "SUBCLASS_ORIGIN", "SUBJECT",
            "SUBPARTITION", "SUBPARTITIONS", "SUPER",
            "SUSPEND", "SWAPS", "SWITCHES",
            "TABLE", "TABLES", "TABLESPACE",
            "TABLE_CHECKSUM", "TABLE_NAME", "TEMPORARY",
            "TEMPTABLE", "TERMINATED", "TEXT",
            "THAN", "THEN", "TIME",
            "TIMESTAMP", "TIMESTAMPADD", "TIMESTAMPDIFF",
            "TINYBLOB", "TINYINT", "TINYTEXT",
            "TO", "TRAILING", "TRANSACTION",
            "TRIGGER", "TRIGGERS", "TRUE",
            "TRUNCATE", "TYPE", "TYPES",
            "UNCOMMITTED", "UNDEFINED", "UNDO",
            "UNDOFILE", "UNDO_BUFFER_SIZE", "UNICODE",
            "UNINSTALL", "UNION", "UNIQUE",
            "UNKNOWN", "UNLOCK", "UNSIGNED",
            "UNTIL", "UPDATE", "UPGRADE",
            "USAGE", "USE", "USER",
            "USER_RESOURCES", "USE_FRM", "USING",
            "UTC_DATE", "UTC_TIME", "UTC_TIMESTAMP",
            "VALUE", "VALUES", "VARBINARY",
            "VARCHAR", "VARCHARACTER", "VARIABLES",
            "VARYING", "VIEW", "WAIT",
            "WARNINGS", "WEEK", "WHEN",
            "WHERE", "WHILE", "WITH",
            "WORK", "WRAPPER", "WRITE",
            "X509", "XA", "XML",
            "XOR", "YEAR", "YEAR_MONTH",
            "ZEROFILL"
        );
    }

    public override int MaxKeyLength
    {
        get { return 767; }
    }

    public override string QuoteTemplate
    {
        get { return "`{0}`"; }
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString,
        string defaultSchema, string scope, string providerName)
    {
        return new MySqlTransformationProvider(dialect, connectionString, scope, providerName);
    }

    public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
        string defaultSchema,
        string scope, string providerName)
    {
        return new MySqlTransformationProvider(dialect, connection, scope, providerName);
    }

    public override string Default(object defaultValue)
    {
        if (defaultValue.GetType().Equals(typeof(bool)))
        {
            defaultValue = ((bool)defaultValue) ? 1 : 0;
        }

        return base.Default(defaultValue);
    }
}
