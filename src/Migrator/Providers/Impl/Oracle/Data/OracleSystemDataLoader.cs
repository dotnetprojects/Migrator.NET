using System.Collections.Generic;
using System.Text;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Data.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Models;
using DotNetProjects.Migrator.Providers.Models;
using DotNetProjects.Migrator.Providers.Models.Indexes;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle.Data;

public class OracleSystemDataLoader(IOracleTransformationProvider oracleTransformationProvider) : IOracleSystemDataLoader
{
    private readonly IOracleTransformationProvider _oracleTransformationProvider = oracleTransformationProvider;

    public List<UserTabIdentityCols> GetUserTabIdentityCols(string tableName)
    {
        List<UserTabIdentityCols> userTabIdentityCols = [];

        var tablePredicate = OracleCatalog.Predicate(_oracleTransformationProvider, tableName);

        var sql = "SELECT TABLE_NAME, COLUMN_NAME, GENERATION_TYPE, SEQUENCE_NAME FROM ALL_TAB_IDENTITY_COLS WHERE " + tablePredicate;

        using var cmd = _oracleTransformationProvider.CreateCommand();
        using var reader = _oracleTransformationProvider.ExecuteQuery(cmd, sql);

        while (reader.Read())
        {
            var tableNameOrdinal = reader.GetOrdinal("TABLE_NAME");
            var columnNameOrdinal = reader.GetOrdinal("COLUMN_NAME");
            var generationTypeOrdinal = reader.GetOrdinal("GENERATION_TYPE");
            var sequenceNameOrdinal = reader.GetOrdinal("SEQUENCE_NAME");

            var userTablIdentityColsItem = new UserTabIdentityCols
            {
                ColumnName = reader.GetString(columnNameOrdinal),
                GenerationType = reader.GetString(generationTypeOrdinal),
                SequenceName = reader.GetString(sequenceNameOrdinal),
                TableName = reader.GetString(tableNameOrdinal),
            };

            userTabIdentityCols.Add(userTablIdentityColsItem);
        }

        return userTabIdentityCols;
    }

    public List<ForeignKeyConstraintItem> GetForeignKeyConstraintItems(string tableName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SELECT");
        sb.AppendLine("  a.OWNER AS TABLE_SCHEMA,");
        sb.AppendLine("  c.CONSTRAINT_NAME AS FK_KEY,");
        sb.AppendLine("  a.TABLE_NAME AS CHILD_TABLE,");
        sb.AppendLine("  a.COLUMN_NAME AS CHILD_COLUMN,");
        sb.AppendLine("  c_pk.TABLE_NAME AS PARENT_TABLE,");
        sb.AppendLine("  col_pk.COLUMN_NAME AS PARENT_COLUMN");
        sb.AppendLine("FROM ");
        sb.AppendLine("  ALL_CONS_COLUMNS a ");
        sb.AppendLine("JOIN ALL_CONSTRAINTS c");
        sb.AppendLine("  ON a.owner = c.owner AND a.CONSTRAINT_NAME = c.CONSTRAINT_NAME");
        sb.AppendLine("JOIN ALL_CONSTRAINTS c_pk");
        sb.AppendLine("  ON c.R_OWNER = c_pk.OWNER AND c.R_CONSTRAINT_NAME = c_pk.CONSTRAINT_NAME");
        sb.AppendLine("JOIN ALL_CONS_COLUMNS col_pk");
        sb.AppendLine("  ON c_pk.CONSTRAINT_NAME = col_pk.CONSTRAINT_NAME AND c_pk.OWNER = col_pk.OWNER AND a.POSITION = col_pk.POSITION");
        sb.AppendLine("WHERE " + OracleCatalog.Predicate(_oracleTransformationProvider, tableName, "a.TABLE_NAME", "a.OWNER") + " AND c.CONSTRAINT_TYPE='R'");
        sb.AppendLine("ORDER BY a.POSITION");

        var sql = sb.ToString();
        List<ForeignKeyConstraintItem> foreignKeyConstraintItems = [];

        using var cmd = _oracleTransformationProvider.CreateCommand();
        using var reader = _oracleTransformationProvider.ExecuteQuery(cmd, sql);

        while (reader.Read())
        {
            var constraintItem = new ForeignKeyConstraintItem
            {
                SchemaName = reader.GetString(reader.GetOrdinal("TABLE_SCHEMA")),
                ForeignKeyName = reader.GetString(reader.GetOrdinal("FK_KEY")),
                ChildTableName = reader.GetString(reader.GetOrdinal("CHILD_TABLE")),
                ChildColumnName = reader.GetString(reader.GetOrdinal("CHILD_COLUMN")),
                ParentTableName = reader.GetString(reader.GetOrdinal("PARENT_TABLE")),
                ParentColumnName = reader.GetString(reader.GetOrdinal("PARENT_COLUMN"))
            };

            foreignKeyConstraintItems.Add(constraintItem);
        }

        return foreignKeyConstraintItems;
    }

    public List<PrimaryKeyItem> GetPrimaryKeyItems(string tableName)
    {
        var sql = $@"
            SELECT
                ucc.TABLE_NAME,
                ucc.COLUMN_NAME,
                ucc.POSITION,
                uc.CONSTRAINT_NAME,
                uc.STATUS
            FROM
                ALL_CONSTRAINTS uc
            JOIN
                ALL_CONS_COLUMNS ucc
                ON uc.OWNER = ucc.OWNER AND uc.CONSTRAINT_NAME = ucc.CONSTRAINT_NAME
            WHERE
                uc.CONSTRAINT_TYPE = 'P'
                {"AND " + OracleCatalog.Predicate(_oracleTransformationProvider, tableName, "ucc.TABLE_NAME", "ucc.OWNER")}
            ORDER BY
                ucc.POSITION
        ";

        List<PrimaryKeyItem> primaryKeyItems = [];

        using var cmd = _oracleTransformationProvider.CreateCommand();
        using var reader = _oracleTransformationProvider.ExecuteQuery(cmd, sql);

        while (reader.Read())
        {
            var constraintItem = new PrimaryKeyItem
            {
                TableName = reader.GetString(reader.GetOrdinal("TABLE_NAME")),
                ColumnName = reader.GetString(reader.GetOrdinal("COLUMN_NAME")),
                Position = reader.GetInt32(reader.GetOrdinal("POSITION")),
                ConstraintName = reader.GetString(reader.GetOrdinal("CONSTRAINT_NAME")),
                Status = reader.GetString(reader.GetOrdinal("STATUS"))
            };

            primaryKeyItems.Add(constraintItem);
        }

        return primaryKeyItems;
    }

    public List<IndexItem> GetIndexItems(string tableName)
    {
        var sql = @$"
            SELECT
                i.table_name,
                i.index_name,
                i.uniqueness,
                ic.column_position,
                ic.column_name,
                CASE WHEN c.constraint_type = 'P' THEN 'YES' ELSE 'NO' END AS is_primary_key,
                CASE WHEN c.constraint_type = 'U' THEN 'YES' ELSE 'NO' END AS is_unique_key
            FROM
                all_indexes i
                JOIN
                    all_ind_columns ic ON i.owner = ic.index_owner AND i.index_name = ic.index_name AND
                    i.table_name = ic.table_name
                LEFT JOIN
                    all_constraints c ON i.owner = c.index_owner AND i.index_name = c.index_name AND
                    i.table_name = c.table_name
            WHERE
                {OracleCatalog.Predicate(_oracleTransformationProvider, tableName, "i.table_name", "i.table_owner")}
            -- AND
            -- i.index_type = 'NORMAL'
            ORDER BY
                i.table_name, i.index_name, ic.column_position";

        List<IndexItem> indexItems = [];

        using var cmd = _oracleTransformationProvider.CreateCommand();
        using var reader = _oracleTransformationProvider.ExecuteQuery(cmd, sql);

        while (reader.Read())
        {
            var tableNameOrdinal = reader.GetOrdinal("table_name");
            var indexNameOrdinal = reader.GetOrdinal("index_name");
            var uniquenessOrdinal = reader.GetOrdinal("uniqueness");
            var columnPositionOrdinal = reader.GetOrdinal("column_position");
            var columnNameOrdinal = reader.GetOrdinal("column_name");
            var isPrimaryKeyOrdinal = reader.GetOrdinal("is_primary_key");
            var isUniqueConstraintOrdinal = reader.GetOrdinal("is_unique_key");

            var indexItem = new IndexItem
            {
                ColumnName = reader.GetString(columnNameOrdinal),
                ColumnOrder = reader.GetInt32(columnPositionOrdinal),
                Name = reader.GetString(indexNameOrdinal),
                PrimaryKey = reader.GetString(isPrimaryKeyOrdinal) == "YES",
                TableName = reader.GetString(tableNameOrdinal),
                Unique = reader.GetString(uniquenessOrdinal) == "UNIQUE",
                UniqueConstraint = reader.GetString(isUniqueConstraintOrdinal) == "YES"
            };

            indexItems.Add(indexItem);
        }

        return indexItems;
    }
}