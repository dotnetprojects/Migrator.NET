using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.DB2;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;

namespace DotNetProjects.Migrator.Providers;

internal static class ConstraintMetadataReader
{
    public static TableConstraint[] Read(TransformationProvider provider, string table)
    {
        string sql;
        var parameterTable = table;
        string schema = null;
        var oracle = provider.Dialect is OracleDialect;
        if (provider.Dialect is SqlServerDialect)
            sql = @"SELECT kc.name, CASE WHEN kc.type='PK' AND ix.type=2 THEN 'PN' ELSE kc.type END, c.name, ic.key_ordinal, CAST(NULL AS nvarchar(max))
                FROM sys.key_constraints kc JOIN sys.indexes ix ON ix.object_id=kc.parent_object_id AND ix.index_id=kc.unique_index_id JOIN sys.index_columns ic ON ic.object_id=kc.parent_object_id AND ic.index_id=kc.unique_index_id
                JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE kc.parent_object_id=OBJECT_ID(@lookup_table) AND ic.key_ordinal>0
                UNION ALL SELECT name, 'C', NULL, 0, definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(@lookup_table)
                ORDER BY 1,4";
        else if (provider.Dialect is PostgreSQLDialect)
        {
            parameterTable = provider.QuoteTableNameIfRequired(table);
            sql = @"SELECT c.conname, c.contype::text, a.attname, k.ordinality, CASE WHEN c.contype='c' THEN pg_get_expr(c.conbin,c.conrelid) END
                FROM pg_constraint c LEFT JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum,ordinality) ON c.contype<>'c'
                LEFT JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.attnum
                WHERE c.conrelid=to_regclass(@lookup_table) AND c.contype IN ('p','u','c') ORDER BY c.conname,k.ordinality";
        }
        else if (provider.Dialect is DB2Dialect)
        {
            parameterTable = table.StartsWith('"') ? table.Trim('"').Replace("\"\"", "\"") : table.ToUpperInvariant();
            sql = @"SELECT c.CONSTNAME,c.TYPE,k.COLNAME,k.COLSEQ,ch.TEXT
                FROM SYSCAT.TABCONST c LEFT JOIN SYSCAT.KEYCOLUSE k
                  ON k.TABSCHEMA=c.TABSCHEMA AND k.TABNAME=c.TABNAME AND k.CONSTNAME=c.CONSTNAME AND c.TYPE IN ('P','U')
                LEFT JOIN SYSCAT.CHECKS ch ON ch.TABSCHEMA=c.TABSCHEMA AND ch.TABNAME=c.TABNAME AND ch.CONSTNAME=c.CONSTNAME
                WHERE c.TABSCHEMA=CURRENT SCHEMA AND c.TABNAME=@lookup_table AND c.TYPE IN ('P','U','K')
                ORDER BY c.CONSTNAME,k.COLSEQ";
        }
        else if (provider.Dialect is FirebirdDialect)
        {
            parameterTable = table.StartsWith('"') ? table.Trim('"').Replace("\"\"", "\"") : table.ToUpperInvariant();
            sql = @"SELECT TRIM(c.RDB$CONSTRAINT_NAME),TRIM(c.RDB$CONSTRAINT_TYPE),TRIM(k.RDB$FIELD_NAME),k.RDB$FIELD_POSITION,
                (SELECT FIRST 1 t.RDB$TRIGGER_SOURCE FROM RDB$CHECK_CONSTRAINTS ch JOIN RDB$TRIGGERS t ON t.RDB$TRIGGER_NAME=ch.RDB$TRIGGER_NAME
                 WHERE ch.RDB$CONSTRAINT_NAME=c.RDB$CONSTRAINT_NAME)
                FROM RDB$RELATION_CONSTRAINTS c LEFT JOIN RDB$INDEX_SEGMENTS k ON k.RDB$INDEX_NAME=c.RDB$INDEX_NAME AND c.RDB$CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE')
                WHERE c.RDB$RELATION_NAME=@lookup_table AND c.RDB$CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE','CHECK')
                ORDER BY c.RDB$CONSTRAINT_NAME,k.RDB$FIELD_POSITION";
        }
        else if (oracle || provider.Dialect is MysqlDialect)
        {
            // Quoted identifiers containing a dot need a structured name API rather than ambiguous splitting.
            var parts = table.Split('.');
            if (parts.Length > 2 || parts.Any(p => p.Contains('"') || p.Contains('`') || p.Contains('[')))
                throw new NotSupportedException("Quoted qualified constraint lookup is not implemented for this provider.");
            parameterTable = oracle ? parts[^1].ToUpperInvariant() : parts[^1];
            schema = parts.Length == 2 ? (oracle ? parts[0].ToUpperInvariant() : parts[0]) : null;
            sql = oracle ? @"SELECT c.CONSTRAINT_NAME,c.CONSTRAINT_TYPE,k.COLUMN_NAME,k.POSITION,c.SEARCH_CONDITION_VC
                FROM ALL_CONSTRAINTS c LEFT JOIN ALL_CONS_COLUMNS k ON k.OWNER=c.OWNER AND k.CONSTRAINT_NAME=c.CONSTRAINT_NAME AND c.CONSTRAINT_TYPE IN ('P','U')
                WHERE c.TABLE_NAME=:lookup_table AND c.OWNER=COALESCE(:lookup_schema,SYS_CONTEXT('USERENV','CURRENT_SCHEMA')) AND c.CONSTRAINT_TYPE IN ('P','U','C')
                ORDER BY c.CONSTRAINT_NAME,k.POSITION"
                : @"SELECT c.CONSTRAINT_NAME,c.CONSTRAINT_TYPE,k.COLUMN_NAME,k.ORDINAL_POSITION,ch.CHECK_CLAUSE
                FROM information_schema.TABLE_CONSTRAINTS c LEFT JOIN information_schema.KEY_COLUMN_USAGE k
                  ON k.CONSTRAINT_SCHEMA=c.CONSTRAINT_SCHEMA AND k.TABLE_NAME=c.TABLE_NAME AND k.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                LEFT JOIN information_schema.CHECK_CONSTRAINTS ch ON ch.CONSTRAINT_SCHEMA=c.CONSTRAINT_SCHEMA AND ch.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                WHERE c.TABLE_NAME=@lookup_table AND c.TABLE_SCHEMA=COALESCE(@lookup_schema,DATABASE()) AND c.CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE','CHECK')
                ORDER BY c.CONSTRAINT_NAME,k.ORDINAL_POSITION";
        }
        else throw new NotSupportedException("Structured constraint inspection is not implemented for " + provider.Dialect.GetType().Name + ".");

        var constraints = new List<TableConstraint>();
        using (var command = provider.CreateCommand())
        {
            AddParameter(command, "lookup_table", parameterTable);
            if (oracle || provider.Dialect is MysqlDialect) AddParameter(command, "lookup_schema", schema);
            using var reader = provider.ExecuteQuery(command, sql);
            string lastName = null;
            TableConstraint current = null;
            var keys = new List<string>();
            void Complete()
            {
                if (current is PrimaryKeyConstraint pk) pk.KeyColumns = keys.ToArray();
                if (current is UniqueConstraint unique) unique.KeyColumns = keys.ToArray();
                if (current != null) constraints.Add(current);
            }
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (name != lastName)
                {
                    Complete(); keys.Clear(); lastName = name;
                    current = reader.GetString(1).Trim().ToUpperInvariant() switch
                    {
                        "P" or "PK" or "PRIMARY KEY" => new PrimaryKeyConstraint { Name = name },
                        "PN" => new PrimaryKeyConstraint { Name = name, NonClustered = true },
                        "U" or "UQ" or "UNIQUE" => new UniqueConstraint { Name = name },
                        "C" or "K" or "CHECK" => new CheckConstraint(name, reader.IsDBNull(4) ? null : CheckExpression(reader.GetString(4))),
                        _ => throw new MigrationException("Unknown catalog constraint type.")
                    };
                }
                if (!reader.IsDBNull(2)) keys.Add(reader.GetString(2));
            }
            Complete();
        }
        constraints.AddRange(provider.GetForeignKeyConstraints(table));
        return constraints.ToArray();
    }

    internal static string CheckExpression(string source)
    {
        var text = source.Trim();
        if (text.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
        {
            text = text[5..].Trim();
            if (text.StartsWith("(") && text.EndsWith(")")) text = text[1..^1];
        }
        return text;
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name;
        parameter.DbType = DbType.String; parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
