using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
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
            sql = @"SELECT kc.name, kc.type, c.name, ic.key_ordinal, CAST(NULL AS nvarchar(max))
                FROM sys.key_constraints kc JOIN sys.index_columns ic ON ic.object_id=kc.parent_object_id AND ic.index_id=kc.unique_index_id
                JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE kc.parent_object_id=OBJECT_ID(@table) AND ic.key_ordinal>0
                UNION ALL SELECT name, 'C', NULL, 0, definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(@table)
                ORDER BY 1,4";
        else if (provider.Dialect is PostgreSQLDialect)
        {
            parameterTable = provider.QuoteTableNameIfRequired(table);
            sql = @"SELECT c.conname, c.contype::text, a.attname, k.ordinality, CASE WHEN c.contype='c' THEN pg_get_expr(c.conbin,c.conrelid) END
                FROM pg_constraint c LEFT JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum,ordinality) ON c.contype<>'c'
                LEFT JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.attnum
                WHERE c.conrelid=to_regclass(@table) AND c.contype IN ('p','u','c') ORDER BY c.conname,k.ordinality";
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
                WHERE c.TABLE_NAME=:table AND c.OWNER=COALESCE(:schema,SYS_CONTEXT('USERENV','CURRENT_SCHEMA')) AND c.CONSTRAINT_TYPE IN ('P','U','C')
                ORDER BY c.CONSTRAINT_NAME,k.POSITION"
                : @"SELECT c.CONSTRAINT_NAME,c.CONSTRAINT_TYPE,k.COLUMN_NAME,k.ORDINAL_POSITION,ch.CHECK_CLAUSE
                FROM information_schema.TABLE_CONSTRAINTS c LEFT JOIN information_schema.KEY_COLUMN_USAGE k
                  ON k.CONSTRAINT_SCHEMA=c.CONSTRAINT_SCHEMA AND k.TABLE_NAME=c.TABLE_NAME AND k.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                LEFT JOIN information_schema.CHECK_CONSTRAINTS ch ON ch.CONSTRAINT_SCHEMA=c.CONSTRAINT_SCHEMA AND ch.CONSTRAINT_NAME=c.CONSTRAINT_NAME
                WHERE c.TABLE_NAME=@table AND c.TABLE_SCHEMA=COALESCE(@schema,DATABASE()) AND c.CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE','CHECK')
                ORDER BY c.CONSTRAINT_NAME,k.ORDINAL_POSITION";
        }
        else throw new NotSupportedException("Structured constraint inspection is not implemented for " + provider.Dialect.GetType().Name + ".");

        var constraints = new List<TableConstraint>();
        using (var command = provider.CreateCommand())
        {
            AddParameter(command, "table", parameterTable);
            if (oracle || provider.Dialect is MysqlDialect) AddParameter(command, "schema", schema);
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
                        "U" or "UQ" or "UNIQUE" => new UniqueConstraint { Name = name },
                        "C" or "CHECK" => new CheckConstraint(name, reader.IsDBNull(4) ? null : reader.GetString(4)),
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

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name;
        parameter.DbType = DbType.String; parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
