using System;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DotNetProjects.Migrator.Providers.Impl.DB2;
using DotNetProjects.Migrator.Providers.Impl.Firebird;
using DotNetProjects.Migrator.Providers.Impl.Hana;
using DotNetProjects.Migrator.Providers.Impl.Sybase;

namespace DotNetProjects.Migrator.Framework;

public enum JsonShape { Any, Array, Object }

public static class CheckExpressions
{
    /// <summary>Byte length, including zero bytes and trailing spaces. SQL NULL remains NULL.</summary>
    public static SqlExpression BinaryLength(this SqlExpression value) => new(c =>
    {
        var function = c.Dialect switch
        {
            SQLiteDialect => "length", PostgreSQLDialect or MysqlDialect or DB2Dialect or FirebirdDialect => "OCTET_LENGTH",
            SqlServerDialect => "DATALENGTH", HanaDialect => "LENGTH",
            _ => throw new NotSupportedException("No CHECK-safe binary length implementation for " + c.Dialect.GetType().Name)
        };
        return function + "(" + value.ToSql(c) + ")";
    });
    /// <summary>Strict JSON validity/shape. SQL NULL is rejected unless allowNull is true.</summary>
    public static SqlExpression IsJson(this SqlExpression value, JsonShape shape = JsonShape.Any, bool allowNull = false)
    {
        if (!Enum.IsDefined(shape)) throw new ArgumentOutOfRangeException(nameof(shape));
        var valid = new SqlExpression(c =>
        {
            var sql = value.ToSql(c);
            var kind = shape.ToString().ToLowerInvariant();
            return c.Dialect switch
            {
                SQLiteDialect => shape == JsonShape.Any ? $"json_valid({sql})=1" : $"(CASE WHEN json_valid({sql})=1 THEN CASE WHEN json_type({sql})='{kind}' THEN 1 ELSE 0 END ELSE 0 END)=1",
                PostgreSQLDialect => shape == JsonShape.Any ? $"(json_typeof(CAST({sql} AS json)) IS NOT NULL)" : $"(json_typeof(CAST({sql} AS json))='{kind}')",
                SqlServerDialect => shape == JsonShape.Any ? $"ISJSON({sql}, VALUE)=1" : $"(ISJSON({sql})=1 AND LEFT(LTRIM(REPLACE(REPLACE(REPLACE({sql},CHAR(9),' '),CHAR(10),' '),CHAR(13),' ')),1)='{(shape == JsonShape.Array ? "[" : "{")}')",
                MysqlDialect => shape == JsonShape.Any ? $"JSON_VALID({sql})=1" : $"(CASE WHEN JSON_VALID({sql})=1 THEN CASE WHEN JSON_TYPE({sql})='{kind.ToUpperInvariant()}' THEN 1 ELSE 0 END ELSE 0 END)=1",
                OracleDialect => shape == JsonShape.Any ? $"({sql} IS JSON STRICT)" : $"(CASE WHEN {sql} IS JSON STRICT THEN CASE WHEN JSON_VALUE({sql}, '$.type()')='{kind}' THEN 1 ELSE 0 END ELSE 0 END)=1",
                _ => throw new NotSupportedException("No strict JSON CHECK implementation for " + c.Dialect.GetType().Name)
            };
        });
        return allowNull ? value.IsNull().Or(value.IsNotNull().And(valid)) : value.IsNotNull().And(valid);
    }
    public static SqlExpression Trim(this SqlExpression value) => new(c => c.Dialect is FirebirdDialect ? "TRIM(" + value.ToSql(c) + ")" : "LTRIM(RTRIM(" + value.ToSql(c) + "))");
    public static SqlExpression TextLength(this SqlExpression value) => new(c =>
        (c.Dialect is SqlServerDialect or SybaseDialect ? "LEN(" : c.Dialect is FirebirdDialect ? "CHAR_LENGTH(" : "LENGTH(") + value.ToSql(c) + ")");
    public static void AddCheckConstraint(this ITransformationProvider p, string name, string table, SqlExpression expression)
    {
        if (p is Providers.NoOpTransformationProvider) return;
        ArgumentNullException.ThrowIfNull(expression);
        p.AddCheckConstraint(name, table, expression.ToSql(new SqlExpressionContext(p.Dialect)));
    }
}
