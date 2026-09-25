using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using DotNetProjects.Migrator.Framework;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using UniqueConstraint = DotNetProjects.Migrator.Framework.UniqueConstraint;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.Ingres;

public class IngresTransformationProvider : TransformationProvider
{
    public IngresTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "Ingres.Client";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, null, null);
        _connection = fac.CreateConnection();
        _connection.ConnectionString = _connectionString;
        this._connection.Open();
    }

    public IngresTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, null, scope)
    {
    }

    public override List<string> GetDatabases() => [Convert.ToString(ExecuteScalar("SELECT DBMSINFO('database')")).TrimEnd()];

    private string Predicate(string table, string owner = "table_owner", string name = "table_name") =>
        $"{owner}={NamespaceSql(table, "DBMSINFO('username')")} AND {name}={ObjectSqlLiteral(table)}";

    public override bool TableExists(string table) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM iitables WHERE {Predicate(table)} AND table_type='T'")) > 0;

    public override bool ViewExists(string table) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM iitables WHERE {Predicate(table)} AND table_type='V'")) > 0;

    public override string[] GetConstraints(string table) =>
        ExecuteStringQuery($"SELECT DISTINCT constraint_name FROM iiconstraints WHERE {Predicate(table, "schema_name")}").Select(n => n.TrimEnd()).ToArray();

    public override bool ConstraintExists(string table, string name) =>
        GetConstraints(table).Contains(name, StringComparer.OrdinalIgnoreCase);

    protected override string GetPrimaryKeyConstraintName(string table) =>
        ExecuteStringQuery($"SELECT DISTINCT constraint_name FROM iiconstraints WHERE {Predicate(table, "schema_name")} AND constraint_type='P'").FirstOrDefault()?.TrimEnd();

    public override bool IndexExists(string table, string name) => GetIndexes(table).Any(i => i.Name == name);

    // Standard catalogs are keyed by owner AND object name. Constraint text can span
    // multiple rows; read it separately so it cannot multiply composite key columns.
    private Dictionary<string, (string Kind, StringBuilder Text)> ConstraintDefinitions(string table)
    {
        var result = new Dictionary<string, (string, StringBuilder)>(StringComparer.Ordinal);
        using var command = CreateCommand();
        using var reader = ExecuteQuery(command, $"SELECT constraint_name,constraint_type,text_segment FROM iiconstraints WHERE {Predicate(table, "schema_name")} ORDER BY constraint_name,text_sequence");
        while (reader.Read())
        {
            var name = reader.GetString(0).TrimEnd();
            if (!result.TryGetValue(name, out var definition))
                result.Add(name, definition = (reader.GetString(1).Trim(), new StringBuilder()));
            // Do not trim or insert separators: a segment boundary may split a token/literal.
            if (!reader.IsDBNull(2)) definition.Item2.Append(reader.GetString(2));
        }
        return result;
    }

    public override TableConstraint[] GetTableConstraints(string table)
    {
        var definitions = ConstraintDefinitions(table);
        var keys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"SELECT constraint_name,column_name FROM iikeys WHERE {Predicate(table, "schema_name")} ORDER BY constraint_name,key_position"))
            while (reader.Read())
            {
                var name = reader.GetString(0).TrimEnd();
                if (!keys.TryGetValue(name, out var columns)) keys.Add(name, columns = []);
                columns.Add(reader.GetString(1).TrimEnd());
            }
        var constraints = new List<TableConstraint>();
        foreach (var (name, definition) in definitions)
        {
            if (definition.Kind == "R") continue;
            keys.TryGetValue(name, out var columns);
            constraints.Add(definition.Kind switch
            {
                "P" => new PrimaryKeyConstraint(name, columns?.ToArray() ?? []),
                "U" => new UniqueConstraint(name, columns?.ToArray() ?? []),
                "C" => new CheckConstraint(name, IngresConstraintText.CheckExpression(definition.Text.ToString())),
                _ => throw new MigrationException("Unknown Ingres constraint type: " + definition.Kind)
            });
        }
        constraints.AddRange(ReadForeignKeys(table, definitions));
        return constraints.ToArray();
    }

    public override ForeignKeyConstraint[] GetForeignKeyConstraints(string table) => ReadForeignKeys(table, ConstraintDefinitions(table));

    private ForeignKeyConstraint[] ReadForeignKeys(string table, Dictionary<string, (string Kind, StringBuilder Text)> definitions)
    {
        var rows = new List<(string Name, string Parent, string ChildColumn, string ParentColumn)>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"""
            SELECT r.ref_constraint_name,r.ref_schema_name,r.unique_schema_name,r.unique_table_name,c.column_name,p.column_name
            FROM iiref_constraints r
            JOIN iikeys c ON c.schema_name=r.ref_schema_name AND c.table_name=r.ref_table_name AND c.constraint_name=r.ref_constraint_name
            JOIN iikeys p ON p.schema_name=r.unique_schema_name AND p.table_name=r.unique_table_name AND p.constraint_name=r.unique_constraint_name AND p.key_position=c.key_position
            WHERE {Predicate(table, "r.ref_schema_name", "r.ref_table_name")}
            ORDER BY r.ref_constraint_name,c.key_position
            """))
            while (reader.Read())
            {
                var childOwner = reader.GetString(1).TrimEnd();
                var parentOwner = reader.GetString(2).TrimEnd();
                var parent = _dialect.QuoteIdentifier(reader.GetString(3).TrimEnd());
                if (parentOwner != childOwner) parent = _dialect.QuoteIdentifier(parentOwner) + "." + parent;
                rows.Add((reader.GetString(0).TrimEnd(), parent, reader.GetString(4).TrimEnd(), reader.GetString(5).TrimEnd()));
            }
        return rows.GroupBy(r => r.Name).Select(group =>
        {
            if (!definitions.TryGetValue(group.Key, out var definition) || definition.Kind != "R")
                throw new MigrationException("Missing Ingres foreign-key definition: " + group.Key);
            var actions = IngresConstraintText.Actions(definition.Text.ToString());
            return new ForeignKeyConstraint(group.Key, group.First().Parent, group.Select(r => r.ParentColumn).ToArray(), table, group.Select(r => r.ChildColumn).ToArray())
                { OnDelete = actions.Delete, OnUpdate = actions.Update };
        }).ToArray();
    }

    private List<(string Index, string Constraint, string Kind)> IndexConstraints(string table)
    {
        var result = new List<(string, string, string)>();
        using var command = CreateCommand();
        using var reader = ExecuteQuery(command, $"""
            SELECT DISTINCT i.index_name,c.constraint_name,c.constraint_type
            FROM iiconstraint_indexes i JOIN iiconstraints c ON c.schema_name=i.schema_name AND c.constraint_name=i.constraint_name
            WHERE {Predicate(table, "c.schema_name", "c.table_name")}
            """);
        while (reader.Read()) result.Add((reader.GetString(0).TrimEnd(), reader.GetString(1).TrimEnd(), reader.GetString(2).Trim()));
        return result;
    }

    public override Index[] GetIndexes(string table)
    {
        var constraints = IndexConstraints(table);
        var rows = new List<(string Name, bool Unique, string Column, int Key, int Position)>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"""
            SELECT i.index_name,i.unique_rule,c.column_name,k.key_sequence,c.column_sequence
            FROM iiindexes i JOIN iicolumns c ON c.table_owner=i.index_owner AND c.table_name=i.index_name
            LEFT JOIN iiindex_columns k ON k.index_owner=i.index_owner AND k.index_name=i.index_name AND k.column_name=c.column_name
            WHERE {Predicate(table, "i.base_owner", "i.base_name")} AND (c.column_name<>'tidp' OR k.key_sequence IS NOT NULL)
            ORDER BY i.index_name,c.column_sequence
            """))
            while (reader.Read()) rows.Add((reader.GetString(0).TrimEnd(), reader.GetString(1).Trim() == "U", reader.GetString(2).TrimEnd(),
                reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)), Convert.ToInt32(reader.GetValue(4))));
        return rows.GroupBy(r => r.Name).Select(group => new Index
        {
            Name = group.Key, Unique = group.First().Unique,
            PrimaryKey = constraints.Any(c => c.Index == group.Key && c.Kind == "P"),
            UniqueConstraint = constraints.Any(c => c.Index == group.Key && c.Kind == "U"),
            KeyColumns = group.Where(r => r.Key > 0).OrderBy(r => r.Key).Select(r => r.Column).ToArray(),
            IncludeColumns = group.Where(r => r.Key == 0).OrderBy(r => r.Position).Select(r => r.Column).ToArray()
        }).ToArray();
    }

    public override string AddIndex(string table, Index index)
    {
        if (index.Clustered || index.FilterItems.Count != 0)
            throw new NotSupportedException("Ingres secondary indexes do not support Clustered or FilterItems.");
        if (index.KeyColumns == null || index.KeyColumns.Length == 0 || index.KeyColumns.Any(string.IsNullOrWhiteSpace))
            throw new MigrationException("An index requires nonempty key columns.");
        var name = index.Name ?? "IX_" + CatalogRelation(table).Name + "_" + string.Join("_", index.KeyColumns);
        var columns = index.KeyColumns.Concat(index.IncludeColumns).ToArray();
        if (columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw new MigrationException("Index columns must not repeat.");
        var keys = string.Join(", ", index.KeyColumns.Select(QuoteColumnNameIfRequired));
        ExecuteNonQuery($"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX {QualifyInSameNamespace(table, name)} ON {QuoteTableNameIfRequired(table)} ({string.Join(", ", columns.Select(QuoteColumnNameIfRequired))}) WITH STRUCTURE=BTREE, KEY=({keys}), PERSISTENCE");
        return name;
    }

    public override void RemoveIndex(string table, string name)
    {
        var index = GetIndexes(table).SingleOrDefault(i => i.Name == name);
        if (index == null) return;
        // A system index name need not match the constraint that owns it.
        foreach (var constraint in IndexConstraints(table).Where(c => c.Index == name)) RemoveConstraint(table, constraint.Constraint);
        if (IndexExists(table, name)) ExecuteNonQuery("DROP INDEX " + QualifyInSameNamespace(table, name));
    }

    public override void RemoveAllIndexes(string table)
    {
        foreach (var index in GetIndexes(table)) RemoveIndex(table, index.Name);
    }

    public override void RemoveAllForeignKeys(string tableName, string columnName)
    {
        var outgoing = Predicate(tableName, "r.ref_schema_name", "r.ref_table_name");
        var incoming = Predicate(tableName, "r.unique_schema_name", "r.unique_table_name");
        if (columnName != null)
        {
            outgoing += " AND c.column_name=" + SqlLiteral(columnName);
            incoming += " AND p.column_name=" + SqlLiteral(columnName);
        }
        var references = new List<(string Table, string Constraint)>();
        using (var command = CreateCommand())
        using (var reader = ExecuteQuery(command, $"""
            SELECT DISTINCT r.ref_schema_name,r.ref_table_name,r.ref_constraint_name
            FROM iiref_constraints r
            JOIN iikeys c ON c.schema_name=r.ref_schema_name AND c.table_name=r.ref_table_name AND c.constraint_name=r.ref_constraint_name
            JOIN iikeys p ON p.schema_name=r.unique_schema_name AND p.table_name=r.unique_table_name AND p.constraint_name=r.unique_constraint_name AND p.key_position=c.key_position
            WHERE ({outgoing}) OR ({incoming})
            """))
            while (reader.Read()) references.Add((_dialect.QuoteIdentifier(reader.GetString(0).TrimEnd()) + "." + _dialect.QuoteIdentifier(reader.GetString(1).TrimEnd()), reader.GetString(2).TrimEnd()));
        foreach (var reference in references) RemoveConstraint(reference.Table, reference.Constraint);
    }

    public override void RemoveConstraint(string table, string name)
    {
        var names = GetConstraints(table);
        var actual = names.FirstOrDefault(n => n == name) ?? names.SingleOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new MigrationException("Constraint was not found in the requested table: " + name);
        ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} DROP CONSTRAINT {_dialect.QuoteIdentifier(actual)} RESTRICT");
    }

    public override void RemoveColumn(string table, string name)
    {
        var column = GetColumnByName(table, name) ?? throw new MigrationException("Column was not found in the requested table: " + name);
        ExecuteNonQuery($"ALTER TABLE {QuoteTableNameIfRequired(table)} DROP COLUMN {_dialect.QuoteIdentifier(column.Name)} RESTRICT");
    }

    public override Column[] GetColumns(string table)
    {
        var columns = new List<Column>();
        using var command = CreateCommand();
        using var reader = ExecuteQuery(command, $"SELECT column_name,column_datatype,column_length,column_scale,column_nulls,column_default_val FROM iicolumns WHERE {Predicate(table)} ORDER BY column_sequence");
        while (reader.Read())
        {
            var type = reader.GetString(1).Trim().ToLowerInvariant() switch
            {
                "integer" or "int" => DbType.Int32, "smallint" => DbType.Int16, "bigint" => DbType.Int64,
                "decimal" or "numeric" or "money" => DbType.Decimal, "float" or "double precision" => DbType.Double,
                "real" => DbType.Single, "date" or "ansidate" => DbType.Date,
                "timestamp without time zone" or "timestamp" or "ingresdate" => DbType.DateTime,
                "boolean" => DbType.Boolean, _ => DbType.String
            };
            var column = new Column(reader.GetString(0).TrimEnd(), type) { IsNullable = reader.GetString(4).Trim() == "Y" };
            if (type == DbType.String) column.Size = Convert.ToInt32(reader.GetValue(2));
            if (type == DbType.Decimal) { column.Precision = Convert.ToInt32(reader.GetValue(2)); column.Scale = Convert.ToInt32(reader.GetValue(3)); }
            if (!reader.IsDBNull(5)) column.DefaultValue = CatalogDefaultValue.Parse(reader.GetString(5), type);
            columns.Add(column);
        }
        return columns.ToArray();
    }
}
