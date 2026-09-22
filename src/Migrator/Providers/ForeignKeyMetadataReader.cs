using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.DB2;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;

namespace DotNetProjects.Migrator.Providers;

internal static class ForeignKeyMetadataReader
{
    public static ForeignKeyConstraint[] Read(TransformationProvider provider, string table)
    {
        var parameterTable = provider.QuoteTableNameIfRequired(table);
        string schema = null;
        string sql;
        if (provider.Dialect is SqlServerDialect)
            sql = @"SELECT f.name,CASE WHEN OBJECT_SCHEMA_NAME(f.referenced_object_id)=OBJECT_SCHEMA_NAME(f.parent_object_id) THEN OBJECT_NAME(f.referenced_object_id) ELSE QUOTENAME(OBJECT_SCHEMA_NAME(f.referenced_object_id))+'.'+QUOTENAME(OBJECT_NAME(f.referenced_object_id)) END,cc.name,pc.name,k.constraint_column_id,
                    REPLACE(f.delete_referential_action_desc,'_',' '),REPLACE(f.update_referential_action_desc,'_',' ')
                FROM sys.foreign_keys f JOIN sys.foreign_key_columns k ON k.constraint_object_id=f.object_id
                JOIN sys.columns cc ON cc.object_id=k.parent_object_id AND cc.column_id=k.parent_column_id
                JOIN sys.columns pc ON pc.object_id=k.referenced_object_id AND pc.column_id=k.referenced_column_id
                WHERE f.parent_object_id=OBJECT_ID(@lookup_table) ORDER BY f.name,k.constraint_column_id";
        else if (provider.Dialect is PostgreSQLDialect)
        {
            parameterTable = provider.QuoteTableNameIfRequired(table);
            sql = @"SELECT c.conname,CASE WHEN p.relnamespace=(SELECT relnamespace FROM pg_class WHERE oid=c.conrelid) THEN p.relname ELSE quote_ident((SELECT nspname FROM pg_namespace WHERE oid=p.relnamespace))||'.'||quote_ident(p.relname) END,cc.attname,pc.attname,k.ordinality,c.confdeltype::text,c.confupdtype::text
                FROM pg_constraint c JOIN pg_class p ON p.oid=c.confrelid
                CROSS JOIN LATERAL unnest(c.conkey,c.confkey) WITH ORDINALITY k(childnum,parentnum,ordinality)
                JOIN pg_attribute cc ON cc.attrelid=c.conrelid AND cc.attnum=k.childnum
                JOIN pg_attribute pc ON pc.attrelid=c.confrelid AND pc.attnum=k.parentnum
                WHERE c.conrelid=to_regclass(@lookup_table) AND c.contype='f' ORDER BY c.conname,k.ordinality";
        }
        else if (provider.Dialect is MysqlDialect)
        {
            var relation = SqlIdentifier.Catalog(provider.QuoteTableNameIfRequired(table));
            parameterTable = relation.Name; schema = relation.Schema;
            sql = @"SELECT k.CONSTRAINT_NAME,k.REFERENCED_TABLE_NAME,k.COLUMN_NAME,k.REFERENCED_COLUMN_NAME,k.ORDINAL_POSITION,r.DELETE_RULE,r.UPDATE_RULE
                FROM information_schema.KEY_COLUMN_USAGE k JOIN information_schema.REFERENTIAL_CONSTRAINTS r
                  ON r.CONSTRAINT_SCHEMA=k.CONSTRAINT_SCHEMA AND r.TABLE_NAME=k.TABLE_NAME AND r.CONSTRAINT_NAME=k.CONSTRAINT_NAME
                WHERE k.TABLE_NAME=@lookup_table AND k.TABLE_SCHEMA=COALESCE(@lookup_schema,DATABASE())
                  AND k.REFERENCED_TABLE_NAME IS NOT NULL ORDER BY k.CONSTRAINT_NAME,k.ORDINAL_POSITION";
        }
        else if (provider.Dialect is OracleDialect)
        {
            var relation = SqlIdentifier.Catalog(provider.QuoteTableNameIfRequired(table), true);
            parameterTable = relation.Name; schema = relation.Schema;
            sql = @"SELECT c.CONSTRAINT_NAME,
                    CASE WHEN p.OWNER=c.OWNER THEN p.TABLE_NAME ELSE p.OWNER||'.'||p.TABLE_NAME END,
                    cc.COLUMN_NAME,pc.COLUMN_NAME,cc.POSITION,c.DELETE_RULE,'NO ACTION'
                FROM ALL_CONSTRAINTS c JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                JOIN ALL_CONSTRAINTS p ON p.OWNER=c.R_OWNER AND p.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME
                JOIN ALL_CONS_COLUMNS pc ON pc.OWNER=p.OWNER AND pc.CONSTRAINT_NAME=p.CONSTRAINT_NAME AND pc.POSITION=cc.POSITION
                WHERE c.CONSTRAINT_TYPE='R' AND c.TABLE_NAME=:lookup_table
                  AND c.OWNER=COALESCE(:lookup_schema,SYS_CONTEXT('USERENV','CURRENT_SCHEMA'))
                ORDER BY c.CONSTRAINT_NAME,cc.POSITION";
        }
        else if (provider.Dialect is DB2Dialect)
        {
            parameterTable = table.StartsWith('"') ? table.Trim('"') : table.ToUpperInvariant();
            sql = @"SELECT r.CONSTNAME,r.REFTABNAME,c.COLNAME,p.COLNAME,c.COLSEQ,r.DELETERULE,r.UPDATERULE
                FROM SYSCAT.REFERENCES r JOIN SYSCAT.KEYCOLUSE c ON c.TABSCHEMA=r.TABSCHEMA AND c.TABNAME=r.TABNAME AND c.CONSTNAME=r.CONSTNAME
                JOIN SYSCAT.KEYCOLUSE p ON p.TABSCHEMA=r.REFTABSCHEMA AND p.TABNAME=r.REFTABNAME AND p.CONSTNAME=r.REFKEYNAME AND p.COLSEQ=c.COLSEQ
                WHERE r.TABSCHEMA=CURRENT SCHEMA AND r.TABNAME=@lookup_table ORDER BY r.CONSTNAME,c.COLSEQ";
        }
        else if (provider.Dialect is FirebirdDialect)
        {
            parameterTable = table.StartsWith('"') ? table.Trim('"') : table.ToUpperInvariant();
            sql = @"SELECT TRIM(c.RDB$CONSTRAINT_NAME),TRIM(p.RDB$RELATION_NAME),TRIM(ck.RDB$FIELD_NAME),TRIM(pk.RDB$FIELD_NAME),
                    ck.RDB$FIELD_POSITION,TRIM(r.RDB$DELETE_RULE),TRIM(r.RDB$UPDATE_RULE)
                FROM RDB$RELATION_CONSTRAINTS c JOIN RDB$REF_CONSTRAINTS r ON r.RDB$CONSTRAINT_NAME=c.RDB$CONSTRAINT_NAME
                JOIN RDB$RELATION_CONSTRAINTS p ON p.RDB$CONSTRAINT_NAME=r.RDB$CONST_NAME_UQ
                JOIN RDB$INDEX_SEGMENTS ck ON ck.RDB$INDEX_NAME=c.RDB$INDEX_NAME
                JOIN RDB$INDEX_SEGMENTS pk ON pk.RDB$INDEX_NAME=p.RDB$INDEX_NAME AND pk.RDB$FIELD_POSITION=ck.RDB$FIELD_POSITION
                WHERE c.RDB$RELATION_NAME=@lookup_table ORDER BY c.RDB$CONSTRAINT_NAME,ck.RDB$FIELD_POSITION";
        }
        else throw new NotSupportedException("Foreign-key metadata is unsupported by " + provider.Dialect.GetType().Name + ".");
        using var command = provider.CreateCommand();
        AddParameter(command, "lookup_table", parameterTable);
        if (provider.Dialect is MysqlDialect or OracleDialect) AddParameter(command, "lookup_schema", schema);
        var rows = new List<(string Name, string Parent, string ChildColumn, string ParentColumn, string Delete, string Update)>();
        using (var reader = provider.ExecuteQuery(command, sql))
            while (reader.Read())
                rows.Add((reader.GetString(0).Trim(), reader.GetString(1).Trim(), reader.GetString(2).Trim(), reader.GetString(3).Trim(),
                    Action(reader.GetString(5)), Action(reader.GetString(6))));
        return rows.GroupBy(r => r.Name).Select(group => new ForeignKeyConstraint(group.Key, group.First().Parent,
            group.Select(r => r.ParentColumn).ToArray(), table, group.Select(r => r.ChildColumn).ToArray())
            { OnDelete = group.First().Delete, OnUpdate = group.First().Update }).ToArray();
    }
    private static string Action(string value) => value.Trim().ToUpperInvariant() switch
    {
        "A" => "NO ACTION", "R" => "RESTRICT", "C" => "CASCADE", "N" => "SET NULL", "D" => "SET DEFAULT",
        "NO ACTION" or "RESTRICT" or "CASCADE" or "SET NULL" or "SET DEFAULT" => value.Trim().ToUpperInvariant(),
        _ => throw new MigrationException("Unknown foreign-key action in catalog: " + value)
    };
    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.DbType = DbType.String;
        parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter);
    }
}
