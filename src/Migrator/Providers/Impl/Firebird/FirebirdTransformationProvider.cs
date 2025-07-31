using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Migrator.Framework;

namespace Migrator.Providers.Impl.Firebird;

/// <summary>
/// Firebird transformation provider
/// </summary>
public class FirebirdTransformationProvider : TransformationProvider
{
    public FirebirdTransformationProvider(Dialect dialect, string connectionString, string scope, string providerName)
        : base(dialect, connectionString, null, scope)
    {
        if (string.IsNullOrEmpty(providerName)) providerName = "FirebirdSql.Data.FirebirdClient";
        var fac = DbProviderFactoriesHelper.GetFactory(providerName, "FirebirdSql.Data.FirebirdClient", "FirebirdSql.Data.FirebirdClient.FirebirdClientFactory");
        _connection = fac.CreateConnection();
        _connection.ConnectionString = _connectionString;
        this._connection.Open();
    }

    public FirebirdTransformationProvider(Dialect dialect, IDbConnection connection, string scope, string providerName)
       : base(dialect, connection, null, scope)
    {
    }

    public override void AddColumn(string table, string sqlColumn)
    {
        table = QuoteTableNameIfRequired(table);
        ExecuteNonQuery(String.Format("ALTER TABLE {0} ADD {1}", table, sqlColumn));
    }

    public override void DropDatabases(string databaseName)
    {
        if (string.IsNullOrEmpty(databaseName))
            ExecuteNonQuery(string.Format("DROP DATABASE"));
    }

    /// <summary>
    /// Execute an SQL query returning results.
    /// </summary>
    /// <param name="sql">The SQL command.</param>
    /// <returns>A data iterator, <see cref="System.Data.IDataReader">IDataReader</see>.</returns>
    public override IDataReader ExecuteQuery(IDbCommand cmd, string sql)
    {
        Logger.Trace(sql);
        //IDbCommand cmd = BuildCommand(sql);
        {
            try
            {
                return cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                Logger.Warn("query failed: {0}", cmd.CommandText);
                throw new Exception("Failed to execute sql statement: " + sql, ex);
            }
        }
    }

    public override Column[] GetColumns(string table)
    {
        var columns = new List<Column>();
        using (var cmd = CreateCommand())
        using (
            var reader =
                ExecuteQuery(cmd,
                    String.Format("select RDB$FIELD_NAME, RDB$NULL_FLAG from RDB$RELATION_FIELDS where RDB$RELATION_NAME = '{0}'", table.ToUpper())))
        {
            while (reader.Read())
            {
                var column = new Column(reader.GetString(0).Trim(), DbType.String);
                var nullableStr = reader.GetString(1);
                var isNullable = nullableStr == "1";
                column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

                columns.Add(column);
            }
        }

        return columns.ToArray();
    }

    public override void AddTable(string name, params IDbField[] fields)
    {
        var columns = fields.Where(x => x is Column).Cast<Column>().ToArray();

        base.AddTable(name, fields);

        if (columns.Any(c => c.ColumnProperty == ColumnProperty.PrimaryKeyWithIdentity))
        {
            var identityColumn = columns.First(c => c.ColumnProperty == ColumnProperty.PrimaryKeyWithIdentity);

            var seqTName = name.Length > 21 ? name.Substring(0, 21) : name;
            if (seqTName.EndsWith("_"))
                seqTName = seqTName.Substring(0, seqTName.Length - 1);

            // Create a sequence for the table
            using (var cmd = CreateCommand())
                ExecuteQuery(cmd, String.Format("CREATE GENERATOR {0}_SEQUENCE", seqTName));
            using (var cmd = CreateCommand())
                ExecuteQuery(cmd, String.Format("SET GENERATOR {0}_SEQUENCE TO 0", seqTName));

            var sql = ""; // "set term !! ;";
            sql += "CREATE TRIGGER {1}_TRIGGER FOR {0}\n";
            sql += "ACTIVE BEFORE INSERT POSITION 0\n";
            sql += "AS\n";
            sql += "BEGIN\n";
            sql += "if (NEW.{2} is NULL) then NEW.{2} = GEN_ID({1}_SEQUENCE, 1);\n";
            sql += "END\n";

            using (var cmd = CreateCommand())
                ExecuteQuery(cmd, String.Format(sql, name, seqTName, identityColumn.Name));
        }
    }

    public override List<string> GetDatabases()
    {
        throw new NotImplementedException();
    }

    public override bool ConstraintExists(string table, string name)
    {
        //todo, implement this!!!

        //http://edn.embarcadero.com/article/25259 field infos in FB
        //http://www.felix-colibri.com/papers/db/interbase/using_interbase_system_tables/using_interbase_system_tables.html

        return false;
    }

    public override bool IndexExists(string table, string name)
    {
        return false;
    }
}
