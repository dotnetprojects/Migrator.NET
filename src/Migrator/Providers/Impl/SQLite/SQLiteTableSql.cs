using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using DotNetProjects.Migrator.Framework;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

/// <summary>Pure SQLite table rendering shared by execution and offline preview.</summary>
internal static class SQLiteTableSql
{
    public static string Generate(Dialect dialect, string quotedTable, IDbField[] fields)
    {
        if (fields.Any(f => f is not (Column or PrimaryKeyConstraint or UniqueConstraint or CheckConstraint or ForeignKeyConstraint or Index)))
            throw new NotSupportedException("Unsupported SQLite table definition.");
        if (!fields.OfType<Column>().Any()) throw new MigrationException("A table requires columns.");
        var columns = fields.Where(x => x is Column)
            .Cast<Column>()
            .Select(column => column.CopyDefinition())
            .ToArray();

        var explicitKeys = fields.OfType<PrimaryKeyConstraint>().ToArray();
        if (explicitKeys.Length > 1) throw new MigrationException("A table can have only one primary key.");
        var explicitKey = explicitKeys.SingleOrDefault();
        if (explicitKey != null)
        {
            TransformationProvider.ValidateKeyColumns(explicitKey.Name, explicitKey.KeyColumns, columns);
            if (explicitKey.NonClustered) throw new NotSupportedException("SQLite does not support nonclustered primary keys.");
            foreach (var column in columns.Where(c => explicitKey.KeyColumns.Contains(c.Name, StringComparer.OrdinalIgnoreCase)))
                column.IsNullable = false;
            var identities = columns.Where(c => c.IsIdentity).ToArray();
            if (identities.Length != 0 && (identities.Length != 1 || explicitKey.KeyColumns.Length != 1 || !explicitKey.KeyColumns[0].Equals(identities[0].Name, StringComparison.OrdinalIgnoreCase) || dialect.GetTypeName(identities[0].Type) != "INTEGER"))
                throw new MigrationException("SQLite identity requires one INTEGER primary-key column.");
        }
        foreach (var unique in fields.OfType<UniqueConstraint>()) TransformationProvider.ValidateKeyColumns(unique.Name, unique.KeyColumns, columns);

        if (explicitKey == null && columns.Any(c => c.IsIdentity))
            throw new MigrationException("SQLite identity requires an explicit INTEGER primary-key constraint.");
        var columnSql = columns.Select(column =>
        {
            var mapped = column.CopyDefinition();
            mapped.IsIdentity = false;
            var sql = dialect.GetAndMapColumnProperties(mapped).ColumnSql;
            if (column.IsIdentity)
                sql += (explicitKey.Name == null ? "" : $" CONSTRAINT {dialect.QuoteIdentifier(explicitKey.Name)}") + " PRIMARY KEY AUTOINCREMENT";
            return sql;
        }).ToList();
        if (explicitKey != null && !columns.Any(c => c.IsIdentity))
            columnSql.Add(dialect.GetTableConstraintSql(explicitKey));
        var table = quotedTable;
        var stringBuilder = new StringBuilder($"CREATE TABLE {table} ({string.Join(", ", columnSql)}");

        // Uniques
        var uniques = fields.Where(x => x is UniqueConstraint).Cast<UniqueConstraint>().ToArray();

        foreach (var u in uniques)
        {
            if (!string.IsNullOrEmpty(u.Name))
            {
                stringBuilder.Append($", CONSTRAINT {dialect.QuoteIdentifier(u.Name)}");
            }
            else
            {
                stringBuilder.Append(", ");
            }

            var uniqueColumnsCommaSeparated = string.Join(", ", u.KeyColumns.Select(dialect.QuoteColumnNameIfRequired));
            stringBuilder.Append($" UNIQUE ({uniqueColumnsCommaSeparated})");
        }

        // Foreign keys
        var foreignKeys = fields.Where(x => x is ForeignKeyConstraint).Cast<ForeignKeyConstraint>().ToArray();

        List<string> foreignKeyStrings = [];

        foreach (var fk in foreignKeys)
        {
            var match = ValidateMatch(fk.Match);
            var sourceColumnNamesQuotedString = string.Join(", ", fk.ChildColumns.Select(dialect.QuoteColumnNameIfRequired));
            var parentColumnNamesQuotedString = string.Join(", ", fk.ParentColumns.Select(dialect.QuoteColumnNameIfRequired));
            var childRelation = SqlIdentifier.Catalog(quotedTable);
            var parentRelation = SqlIdentifier.Catalog(fk.ParentTable);
            if (parentRelation.Schema != null && !string.Equals(parentRelation.Schema, childRelation.Schema ?? "main", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("SQLite foreign keys cannot reference another database namespace.");
            var parentTableNameQuoted = dialect.QuoteIdentifier(parentRelation.Name);

            var foreignKeySql = (fk.Name == null ? "" : $"CONSTRAINT {dialect.QuoteIdentifier(fk.Name)} ") +
                $"FOREIGN KEY ({sourceColumnNamesQuotedString}) REFERENCES {parentTableNameQuoted}" +
                (fk.ParentColumns.Length == 0 ? "" : $"({parentColumnNamesQuotedString})");
            if (match == "SIMPLE") foreignKeySql += " MATCH SIMPLE";
            if (!string.IsNullOrWhiteSpace(fk.OnDelete) && !string.Equals(fk.OnDelete, "NO ACTION", StringComparison.OrdinalIgnoreCase))
            {
                foreignKeySql += $" ON DELETE {ValidateAction(fk.OnDelete)}";
            }

            if (!string.IsNullOrWhiteSpace(fk.OnUpdate)) foreignKeySql += $" ON UPDATE {ValidateAction(fk.OnUpdate)}";
            foreignKeyStrings.Add(foreignKeySql);
        }

        if (foreignKeyStrings.Count > 0)
        {
            stringBuilder.Append(", ");
            stringBuilder.Append(string.Join(", ", foreignKeyStrings));
        }

        // Check Constraints
        var checkConstraints = fields.Where(x => x is CheckConstraint).OfType<CheckConstraint>().ToArray();
        List<string> checkConstraintStrings = [];

        foreach (var checkConstraint in checkConstraints)
        {
            checkConstraintStrings.Add(dialect.GetTableConstraintSql(checkConstraint));
        }

        if (checkConstraintStrings.Count > 0)
        {
            stringBuilder.Append($", {string.Join(", ", checkConstraintStrings)}");
        }

        stringBuilder.Append(')');

        return stringBuilder.ToString();
    }

    internal static string ValidateMatch(string match)
    {
        var value = match?.Trim().ToUpperInvariant();
        // SQLite accepts MATCH syntax but enforces only SIMPLE. NONE is the
        // value returned by PRAGMA foreign_key_list when no match is declared.
        if (string.IsNullOrEmpty(value) || value is "NONE" or "SIMPLE") return value;
        throw new NotSupportedException("SQLite only enforces MATCH SIMPLE; unsupported foreign-key match: " + match);
    }

    private static string ValidateAction(string action)
    {
        var value = action.ToUpperInvariant();
        if (value is not ("CASCADE" or "RESTRICT" or "SET NULL" or "SET DEFAULT" or "NO ACTION"))
            throw new MigrationException("Unsupported foreign-key action: " + action);
        return value;
    }
}
