using System.Collections.Generic;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Data.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Interfaces;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Models;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Data;

public class PostgreSQLSystemDataLoader(IPostgreSQLTransformationProvider postgreTransformationProvider) : IPostgreSQLSystemDataLoader
{
    private readonly IPostgreSQLTransformationProvider _postgreSQLTransformationProvider = postgreTransformationProvider;

    public List<TableConstraint> GetTableConstraints(string tableName, string schemaName = "public")
    {
        var quotedTableName = _postgreSQLTransformationProvider.QuoteTableNameIfRequired(tableName);

        var sql = $@"
            SELECT
                tc.TABLE_SCHEMA,
                tc.TABLE_NAME,
                tc.CONSTRAINT_NAME,
                tc.CONSTRAINT_TYPE,
                kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
                AND tc.TABLE_NAME = kcu.TABLE_NAME
            WHERE 
                LOWER(tc.table_name) = '{quotedTableName.ToLowerInvariant()}' 
                AND tc.TABLE_SCHEMA = '{schemaName}'
        ";

        List<TableConstraint> tableConstraints = [];

        using var cmd = _postgreSQLTransformationProvider.CreateCommand();
        using var reader = _postgreSQLTransformationProvider.ExecuteQuery(cmd, sql);

        while (reader.Read())
        {
            var constraintNameOrdinal = reader.GetOrdinal("CONSTRAINT_NAME");
            var constraintTypeOrdinal = reader.GetOrdinal("CONSTRAINT_TYPE");
            var columnNameOrdinal = reader.GetOrdinal("COLUMN_NAME");
            var tableNameOrdinal = reader.GetOrdinal("TABLE_NAME");
            var tableSchemaOrdinal = reader.GetOrdinal("TABLE_SCHEMA");

            var tableConstraint = new TableConstraint
            {
                ConstraintName = !reader.IsDBNull(constraintNameOrdinal) ? reader.GetString(constraintNameOrdinal) : null,
                ConstraintType = !reader.IsDBNull(constraintTypeOrdinal) ? reader.GetString(constraintTypeOrdinal) : null,
                ColumnName = reader.GetString(columnNameOrdinal),
                TableName = reader.GetString(tableNameOrdinal),
                TableSchema = reader.GetString(tableSchemaOrdinal),
            };

            tableConstraints.Add(tableConstraint);
        }

        return tableConstraints;
    }

    public List<ColumnInfo> GetColumnInfos(string tableName, string schemaName = "public")
    {
        var quotedTableName = _postgreSQLTransformationProvider.QuoteTableNameIfRequired(tableName);

        var sql = $@"
            SELECT
                c.CHARACTER_MAXIMUM_LENGTH,
                c.COLUMN_DEFAULT,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.DATETIME_PRECISION,
                c.IDENTITY_GENERATION,
                c.IS_IDENTITY,
                c.IS_NULLABLE,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.ORDINAL_POSITION,
                c.TABLE_SCHEMA,
                c.TABLE_NAME
            FROM information_schema.columns c
            WHERE 
                LOWER(c.table_name) = '{quotedTableName.ToLowerInvariant()}' AND
                c.TABLE_SCHEMA = '{schemaName}' 
        ";

        List<ColumnInfo> columns = [];

        using var cmd = _postgreSQLTransformationProvider.CreateCommand();
        using var reader = _postgreSQLTransformationProvider.ExecuteQuery(cmd, sql);

        while (reader.Read())
        {
            var characterMaximumLength = reader.GetOrdinal("CHARACTER_MAXIMUM_LENGTH");
            var columnDefaultOrdinal = reader.GetOrdinal("COLUMN_DEFAULT");
            var columnNameOrdinal = reader.GetOrdinal("COLUMN_NAME");
            var dataTypeOrdinal = reader.GetOrdinal("DATA_TYPE");
            var dateTimePrecisionOrdinal = reader.GetOrdinal("DATETIME_PRECISION");
            var identityGenerationOrdinal = reader.GetOrdinal("IDENTITY_GENERATION");
            var isIdentityOrdinal = reader.GetOrdinal("IS_IDENTITY");
            var isNullableOrdinal = reader.GetOrdinal("IS_NULLABLE");
            var numericPrecisionOrdinal = reader.GetOrdinal("NUMERIC_PRECISION");
            var numericScaleOrdinal = reader.GetOrdinal("NUMERIC_SCALE");
            var ordinalPositionOrdinal = reader.GetOrdinal("ORDINAL_POSITION");
            var tableNameOrdinal = reader.GetOrdinal("TABLE_NAME");
            var tableSchemaOrdinal = reader.GetOrdinal("TABLE_SCHEMA");

            var columnInfo = new ColumnInfo
            {
                CharacterMaximumLength = !reader.IsDBNull(characterMaximumLength) ? reader.GetInt32(characterMaximumLength) : null,
                ColumnDefault = !reader.IsDBNull(columnDefaultOrdinal) ? reader.GetString(columnDefaultOrdinal) : null,
                ColumnName = reader.GetString(columnNameOrdinal),
                DataType = reader.GetString(dataTypeOrdinal),
                DateTimePrecision = !reader.IsDBNull(dateTimePrecisionOrdinal) ? reader.GetInt32(dateTimePrecisionOrdinal) : null,
                IdentityGeneration = !reader.IsDBNull(identityGenerationOrdinal) ? reader.GetString(identityGenerationOrdinal) : null,
                IsIdentity = reader.GetString(isIdentityOrdinal),
                IsNullable = reader.GetString(isNullableOrdinal),
                NumericPrecision = !reader.IsDBNull(numericPrecisionOrdinal) ? reader.GetInt32(numericPrecisionOrdinal) : null,
                NumericScale = !reader.IsDBNull(numericScaleOrdinal) ? reader.GetInt32(numericScaleOrdinal) : null,
                OrdinalPosition = reader.GetInt32(ordinalPositionOrdinal),
                TableName = reader.GetString(tableNameOrdinal),
                TableSchema = reader.GetString(tableSchemaOrdinal),
            };

            columns.Add(columnInfo);
        }

        return columns;
    }
}