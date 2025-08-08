#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using DotNetProjects.Migrator.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using Index = DotNetProjects.Migrator.Framework.Index;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL;

/// <summary>
/// Migration transformations provider for PostgreSql (using NPGSql .Net driver)
/// </summary>
public class PostgreSQLTransformationProvider : TransformationProvider
{
    public PostgreSQLTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName)
        : base(dialect, connectionString, defaultSchema, scope)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = "Npgsql";
        }

        var fac = DbProviderFactoriesHelper.GetFactory(providerName, "Npgsql", "Npgsql.NpgsqlFactory");
        _connection = fac.CreateConnection(); //new NpgsqlConnection();
        _connection.ConnectionString = _connectionString;
        _connection.Open();
    }

    public PostgreSQLTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName)
       : base(dialect, connection, defaultSchema, scope)
    {
    }

    protected override string GetPrimaryKeyConstraintName(string table)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT conname FROM pg_constraint WHERE contype = 'p' AND conrelid = (SELECT oid FROM pg_class WHERE relname = lower('{0}'));", table));
        return reader.Read() ? reader.GetString(0) : null;
    }

    public override Index[] GetIndexes(string table)
    {
        var retVal = new List<Index>();

        var sql = @"
SELECT * FROM (
SELECT i.relname as indname,
       idx.indisprimary,
       idx.indisunique,
       idx.indisclustered,
       i.relowner as indowner,
       cast(idx.indrelid::regclass as varchar) as tablenm,
       am.amname as indam,
       idx.indkey,
       ARRAY_TO_STRING(ARRAY(
       SELECT pg_get_indexdef(idx.indexrelid, k + 1, true)
       FROM generate_subscripts(idx.indkey, 1) as k
       ORDER BY k
       ), ',') as indkey_names,
       idx.indexprs IS NOT NULL as indexprs,
       idx.indpred IS NOT NULL as indpred
FROM   pg_index as idx
JOIN   pg_class as i
ON     i.oid = idx.indexrelid
JOIN   pg_am as am
ON     i.relam = am.oid
JOIN   pg_namespace as ns
ON     ns.oid = i.relnamespace
AND    ns.nspname = ANY(current_schemas(false))) AS t
WHERE  lower(tablenm) = lower('{0}')
;";


        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, string.Format(sql, table)))
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    var idx = new Index
                    {
                        Name = reader.GetString(0),
                        PrimaryKey = reader.GetBoolean(1),
                        Unique = reader.GetBoolean(2),
                        Clustered = reader.GetBoolean(3),
                    };
                    var cols = reader.GetString(8);
                    idx.KeyColumns = cols.Split(',');
                    retVal.Add(idx);
                }
            }
        }

        return retVal.ToArray();
    }

    public override void RemoveTable(string name)
    {
        if (!TableExists(name))
        {
            throw new MigrationException(string.Format("Table with name '{0}' does not exist to rename", name));
        }

        ExecuteNonQuery(string.Format("DROP TABLE IF EXISTS {0} CASCADE", name));
    }

    public override bool ConstraintExists(string table, string name)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT constraint_name FROM information_schema.table_constraints WHERE table_schema = 'public' AND constraint_name = lower('{0}')", name));

        return reader.Read();
    }

    public override bool ColumnExists(string table, string column)
    {
        if (!TableExists(table))
        {
            return false;
        }

        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = lower('{0}') AND (column_name = lower('{1}') OR column_name = '{1}')", table, column));
        return reader.Read();
    }

    public override bool TableExists(string table)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name = lower('{0}')", table));
        return reader.Read();
    }

    public override bool ViewExists(string view)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT table_name FROM information_schema.views WHERE table_schema = 'public' AND table_name = lower('{0}')", view));

        return reader.Read();
    }

    public override List<string> GetDatabases()
    {
        return ExecuteStringQuery("SELECT datname FROM pg_database WHERE datistemplate = false");
    }

    public override void ChangeColumn(string table, Column column)
    {
        var oldColumn = GetColumnByName(table, column.Name);

        var isUniqueSet = column.ColumnProperty.IsSet(ColumnProperty.Unique);

        column.ColumnProperty = column.ColumnProperty.Clear(ColumnProperty.Unique);

        var mapper = _dialect.GetAndMapColumnProperties(column);

        var change1 = string.Format("{0} TYPE {1}", QuoteColumnNameIfRequired(mapper.Name), mapper.type);

        if ((oldColumn.Type == DbType.Int16 || oldColumn.Type == DbType.Int32 || oldColumn.Type == DbType.Int64 || oldColumn.Type == DbType.Decimal) && column.Type == DbType.Boolean)
        {
            change1 += string.Format(" USING CASE {0} WHEN 1 THEN true ELSE false END", QuoteColumnNameIfRequired(mapper.Name));
        }
        else if (column.Type == DbType.Boolean)
        {
            change1 += string.Format(" USING CASE {0} WHEN '1' THEN true ELSE false END", QuoteColumnNameIfRequired(mapper.Name));
        }


        ChangeColumn(table, change1);

        if (mapper.Default != null)
        {
            var change2 = string.Format("{0} SET {1}", QuoteColumnNameIfRequired(mapper.Name), _dialect.Default(mapper.Default));
            ChangeColumn(table, change2);
        }
        else
        {
            var change2 = string.Format("{0} DROP DEFAULT", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change2);
        }

        if (column.ColumnProperty.HasFlag(ColumnProperty.NotNull))
        {
            var change3 = string.Format("{0} SET NOT NULL", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change3);
        }
        else
        {
            var change3 = string.Format("{0} DROP NOT NULL", QuoteColumnNameIfRequired(mapper.Name));
            ChangeColumn(table, change3);
        }

        if (isUniqueSet)
        {
            AddUniqueConstraint(string.Format("UX_{0}_{1}", table, column.Name), table, [column.Name]);
        }
    }

    public override void CreateDatabases(string databaseName)
    {
        ExecuteNonQuery(string.Format("CREATE DATABASE {0}", _dialect.Quote(databaseName)));
    }

    public override void SwitchDatabase(string databaseName)
    {
        _connection.ChangeDatabase(_dialect.Quote(databaseName));
    }

    public override void DropDatabases(string databaseName)
    {
        ExecuteNonQuery(string.Format("DROP DATABASE {0}", _dialect.Quote(databaseName)));
    }

    public override string[] GetTables()
    {
        var tables = new List<string>();
        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'"))
        {
            while (reader.Read())
            {
                tables.Add((string)reader[0]);
            }
        }
        return tables.ToArray();
    }

    public override Column[] GetColumns(string table)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("SELECT");
        stringBuilder.AppendLine("  COLUMN_NAME,");
        stringBuilder.AppendLine("  IS_NULLABLE,");
        stringBuilder.AppendLine("  COLUMN_DEFAULT,");
        stringBuilder.AppendLine("  DATA_TYPE,");
        stringBuilder.AppendLine("  DATETIME_PRECISION,");
        stringBuilder.AppendLine("  CHARACTER_MAXIMUM_LENGTH,");
        stringBuilder.AppendLine("  NUMERIC_PRECISION,");
        stringBuilder.AppendLine("  NUMERIC_SCALE");
        stringBuilder.AppendLine($"FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'public' AND TABLE_NAME = lower('{table}');");

        var columns = new List<Column>();

        using (var cmd = CreateCommand())
        using (var reader = ExecuteQuery(cmd, stringBuilder.ToString()))
        {
            while (reader.Read())
            {
                var defaultValueOrdinal = reader.GetOrdinal("COLUMN_DEFAULT");
                var characterMaximumLengthOrdinal = reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH");
                var dateTimePrecisionOrdinal = reader.GetOrdinal("DATETIME_PRECISION");
                var numericPrecisionOrdinal = reader.GetOrdinal("NUMERIC_PRECISION");
                var numericScaleOrdinal = reader.GetOrdinal("NUMERIC_SCALE");

                var columnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME"));
                var isNullable = reader.GetString(reader.GetOrdinal("IS_NULLABLE")) == "YES";
                var defaultValueString = reader.IsDBNull(defaultValueOrdinal) ? null : reader.GetString(defaultValueOrdinal);
                var dataTypeString = reader.GetString(reader.GetOrdinal("DATA_TYPE"));
                var dateTimePrecision = reader.IsDBNull(dateTimePrecisionOrdinal) ? null : (int?)reader.GetInt32(dateTimePrecisionOrdinal);
                var characterMaximumLength = reader.IsDBNull(characterMaximumLengthOrdinal) ? null : (int?)reader.GetInt32(characterMaximumLengthOrdinal);
                var numericPrecision = reader.IsDBNull(numericPrecisionOrdinal) ? null : (int?)reader.GetInt32(numericPrecisionOrdinal);
                var numericScale = reader.IsDBNull(numericScaleOrdinal) ? null : (int?)reader.GetInt32(numericScaleOrdinal);

                DbType dbType = 0;
                int? precision = null;
                int? scale = null;
                int? size = null;

                if (new[] { "timestamptz", "timestamp with time zone" }.Contains(dataTypeString))
                {
                    dbType = DbType.DateTimeOffset;
                    precision = dateTimePrecision;
                }
                else if (dataTypeString == "timestamp" || dataTypeString == "timestamp without time zone")
                {
                    // 6 is the maximum in PostgreSQL
                    if (dateTimePrecision > 5)
                    {
                        dbType = DbType.DateTime2;
                    }
                    else
                    {
                        dbType = DbType.DateTime;
                    }

                    precision = dateTimePrecision;
                }
                else if (dataTypeString == "smallint")
                {
                    dbType = DbType.Int16;
                }
                else if (dataTypeString == "integer")
                {
                    dbType = DbType.Int32;
                }
                else if (dataTypeString == "bigint")
                {
                    dbType = DbType.Int64;
                }
                else if (dataTypeString == "numeric")
                {
                    dbType = DbType.Decimal;
                    precision = numericPrecision;
                    scale = numericScale;
                }
                else if (dataTypeString == "real")
                {
                    dbType = DbType.Single;
                }
                else if (dataTypeString == "money")
                {
                    dbType = DbType.Currency;
                }
                else if (dataTypeString == "date")
                {
                    dbType = DbType.Date;
                }
                else if (dataTypeString == "byte")
                {
                    dbType = DbType.Binary;
                }
                else if (dataTypeString == "uuid")
                {
                    dbType = DbType.Guid;
                }
                else if (dataTypeString == "xml")
                {
                    dbType = DbType.Xml;
                }
                else if (dataTypeString == "time")
                {
                    dbType = DbType.Time;
                }
                else if (dataTypeString == "interval")
                {
                    throw new NotImplementedException();
                }
                else if (dataTypeString == "boolean")
                {
                    dbType = DbType.Boolean;
                }
                else if (dataTypeString == "text" || dataTypeString == "character varying")
                {
                    dbType = DbType.String;
                    size = characterMaximumLength;
                }
                else if (dataTypeString == "character" || dataTypeString.StartsWith("character("))
                {
                    throw new NotSupportedException("Data type 'character' detected. We do not support 'character'. Use 'text' or 'character varying' instead");
                }
                else
                {
                    throw new NotImplementedException("The data type is not implemented. Please file an issue.");
                }

                var column = new Column(columnName, dbType)
                {
                    Precision = precision,
                    Scale = scale,
                    // Size should be nullable
                    Size = size ?? 0
                };

                column.ColumnProperty |= isNullable ? ColumnProperty.Null : ColumnProperty.NotNull;

                // if (defaultValueString != null && defaultValueString != DBNull.Value)
                // {
                //     column.DefaultValue = defaultValueString;
                // }

                if (column.DefaultValue != null)
                {
                    if (column.Type == DbType.Int16 || column.Type == DbType.Int32 || column.Type == DbType.Int64)
                    {
                        column.DefaultValue = long.Parse(column.DefaultValue.ToString());
                    }
                    else if (column.Type == DbType.UInt16 || column.Type == DbType.UInt32 || column.Type == DbType.UInt64)
                    {
                        column.DefaultValue = ulong.Parse(column.DefaultValue.ToString());
                    }
                    else if (column.Type == DbType.Double || column.Type == DbType.Single)
                    {
                        column.DefaultValue = double.Parse(column.DefaultValue.ToString());
                    }
                    else if (column.Type == DbType.Boolean)
                    {
                        column.DefaultValue = column.DefaultValue.ToString().Trim() == "1" || column.DefaultValue.ToString().Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase) || column.DefaultValue.ToString().Trim() == "YES";
                    }
                    else if (column.Type == DbType.DateTime || column.Type == DbType.DateTime2)
                    {
                        if (column.DefaultValue is string defVal)
                        {
                            var dt = defVal;
                            if (defVal.StartsWith("'"))
                            {
                                dt = defVal.Substring(1, defVal.Length - 2);
                            }

                            var d = DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                            column.DefaultValue = d;
                        }
                    }
                    else if (column.Type == DbType.Guid)
                    {
                        if (column.DefaultValue is string defVal)
                        {
                            var dt = defVal;

                            if (defVal.StartsWith("'"))
                            {
                                dt = defVal.Substring(1, defVal.Length - 2);
                            }

                            var d = Guid.Parse(dt);
                            column.DefaultValue = d;
                        }
                    }
                }

                columns.Add(column);
            }
        }

        return columns.ToArray();
    }

    public override string[] GetConstraints(string table)
    {
        var constraints = new List<string>();

        using (var cmd = CreateCommand())
        using (
            var reader =
                ExecuteQuery(
                    cmd, string.Format(@"select c.conname as constraint_name
from pg_constraint c
join pg_class t on c.conrelid = t.oid
where LOWER(t.relname) = LOWER('{0}')", table)))
        {
            while (reader.Read())
            {
                constraints.Add(reader.GetString(0));
            }
        }

        return constraints.ToArray();
    }

    public override Column GetColumnByName(string table, string columnName)
    {
        // Duplicate because of the lower case issue
        return Array.Find(GetColumns(table), x => x.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase) || x.Name == columnName);
    }

    public override bool IndexExists(string table, string name)
    {
        using var cmd = CreateCommand();
        using var reader =
            ExecuteQuery(cmd, string.Format("SELECT indexname FROM pg_catalog.pg_indexes WHERE indexname = lower('{0}')", name));

        return reader.Read();
    }

    protected override void ConfigureParameterWithValue(IDbDataParameter parameter, int index, object value)
    {
        if (value is ushort)
        {
            parameter.DbType = DbType.Int32;
            parameter.Value = Convert.ToInt32(value);
        }
        else if (value is uint)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = Convert.ToInt64(value);
        }
        else
        {
            base.ConfigureParameterWithValue(parameter, index, value);
        }
    }
}
