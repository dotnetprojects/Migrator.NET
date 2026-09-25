using System;
using System.Linq;
using DotNetProjects.Migrator.Providers.Impl.DB2;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using DotNetProjects.Migrator.Providers.Impl.Hana;
using DotNetProjects.Migrator.Providers.Impl.Informix;
using DotNetProjects.Migrator.Providers.Impl.Ingres;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Sybase;

namespace DotNetProjects.Migrator.Providers;

internal static class NamespaceCatalog
{
    internal static string[] Tables(TransformationProvider provider, string schema)
    {
        var part = schema == null ? (SqlIdentifier.Part?)null : SqlIdentifier.Parse(provider.Dialect.QuoteTableNameIfRequired(schema)).Single();
        var name = part?.Value;
        if (part is { Quoted: false } && provider.Dialect is OracleDialect or DB2Dialect) name = name.ToUpperInvariant();
        if (part is { Quoted: false } && provider.Dialect is PostgreSQLDialect or InformixDialect or IngresDialect) name = name.ToLowerInvariant();
        string Literal(string value) => "'" + value.Replace("'", "''") + "'";
        string Scope(string fallback) => name == null ? fallback : Literal(name);
        var sql = provider.Dialect switch
        {
            SQLiteDialect => "SELECT name FROM " + provider.Dialect.QuoteIdentifier(name ?? "main") + ".sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
            SqlServerDialect => $"SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name={Scope("SCHEMA_NAME()")} ORDER BY t.name",
            PostgreSQLDialect => $"SELECT table_name FROM information_schema.tables WHERE table_schema={Scope("current_schema()")} AND table_type='BASE TABLE' ORDER BY table_name",
            OracleDialect => $"SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER={Scope("SYS_CONTEXT('USERENV','CURRENT_SCHEMA')")} ORDER BY TABLE_NAME",
            MysqlDialect => $"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA={Scope("DATABASE()")} AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME",
            DB2Dialect => $"SELECT TABNAME FROM SYSCAT.TABLES WHERE TABSCHEMA={Scope("CURRENT SCHEMA")} AND TYPE='T' ORDER BY TABNAME",
            InformixDialect => $"SELECT tabname FROM systables WHERE owner={Scope("USER")} AND tabid>=100 AND tabtype='T' ORDER BY tabname",
            SybaseDialect => $"SELECT name FROM sysobjects WHERE type='U' AND uid=user_id({(name == null ? "" : Literal(name))}) ORDER BY name",
            HanaDialect => $"SELECT TABLE_NAME FROM SYS.TABLES WHERE SCHEMA_NAME={Scope("CURRENT_SCHEMA")} ORDER BY TABLE_NAME",
            IngresDialect => $"SELECT table_name FROM iitables WHERE table_owner={Scope("DBMSINFO('username')")} AND table_type='T' ORDER BY table_name",
            FirebirdDialect when name == null => "SELECT TRIM(RDB$RELATION_NAME) FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG=0 AND RDB$VIEW_BLR IS NULL ORDER BY RDB$RELATION_NAME",
            FirebirdDialect => throw new NotSupportedException("The Firebird provider targets Firebird 5 and does not support namespaces."),
            _ => throw new NotSupportedException("Namespace enumeration is not implemented for " + provider.Dialect.GetType().Name)
        };
        return provider.ExecuteStringQuery(sql).Select(n => n.TrimEnd()).ToArray();
    }
}
