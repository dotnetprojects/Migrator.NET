using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;

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

    public override List<string> GetDatabases()
    {
        throw new NotImplementedException();
    }

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

    public override bool IndexExists(string table, string name) => Convert.ToInt32(ExecuteScalar(
        $"SELECT COUNT(*) FROM iiindexes WHERE {Predicate(table, "base_owner", "base_name")} AND index_name={SqlLiteral(name)}")) > 0;

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
