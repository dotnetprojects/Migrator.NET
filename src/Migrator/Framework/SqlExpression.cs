using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Framework.Fluent;

namespace DotNetProjects.Migrator.Framework;

/// <summary>A composable server expression. Raw SQL is an explicit provider-specific escape hatch.</summary>
public class SqlExpression
{
    private readonly Func<SqlExpressionContext, string> render;
    internal SqlExpression(Func<SqlExpressionContext, string> render) => this.render = render;
    public string ToSql(SqlExpressionContext context) => render(context);
    public static SqlExpression Column(string name, string alias = null) => new(c =>
        (alias == null ? "" : c.Qualifier(alias) + ".") + c.Dialect.QuoteColumnNameIfRequired(name));
    public static SqlExpression Old(string name) => new(c => c.Row("OLD", name));
    public static SqlExpression New(string name) => new(c => c.Row("NEW", name));
    public static SqlExpression Value(object value) => new(c => c.Literal(value));
    public static SqlExpression Parameter(object value)
    {
        var snapshot = value is byte[] bytes ? bytes.ToArray() : value;
        return new(c => c.Bind(snapshot));
    }
    public static SqlExpression Case(SqlExpression condition, SqlExpression whenTrue, SqlExpression whenFalse) => new(c =>
        "(CASE WHEN " + condition.ToSql(c) + " THEN " + whenTrue.ToSql(c) + " ELSE " + whenFalse.ToSql(c) + " END)");
    public static SqlExpression Coalesce(params SqlExpression[] expressions)
    {
        var copy = expressions.ToArray();
        if (copy.Length < 2) throw new ArgumentException("COALESCE requires two expressions.");
        return new(c => "COALESCE(" + string.Join(", ", copy.Select(e => e.ToSql(c))) + ")");
    }
    public SqlExpression Max() => new(c => "MAX(" + ToSql(c) + ")");
    public static SqlExpression Scalar(SelectQuery query) => new(c => "(" + query.ToSql(c) + ")");
    public static SqlExpression RowNumber(SqlExpression[] partitionBy, params QueryOrder[] orderBy)
    {
        var partitions = partitionBy?.ToArray() ?? [];
        var orders = orderBy?.ToArray() ?? [];
        if (orders.Length == 0) throw new ArgumentException("ROW_NUMBER requires explicit deterministic ordering.");
        return new(c =>
        {
            if (c.Dialect is Providers.Impl.Sybase.SybaseDialect or Providers.Impl.Ingres.IngresDialect)
                throw new NotSupportedException("No window-function implementation for this provider.");
            return "ROW_NUMBER() OVER (" + (partitions.Length == 0 ? "" : "PARTITION BY " + string.Join(", ", partitions.Select(x => x.ToSql(c))) + " ")
                + "ORDER BY " + string.Join(", ", orders.Select(x => x.Expression.ToSql(c) + (x.Descending ? " DESC" : " ASC"))) + ")";
        });
    }
    public static SqlExpression Raw(string sql) => new(_ => sql ?? throw new ArgumentNullException(nameof(sql)));
    public SqlExpression EqualTo(SqlExpression other) => Binary("=", other);
    public SqlExpression NotEqualTo(SqlExpression other) => Binary("<>", other);
    public SqlExpression GreaterThan(SqlExpression other) => Binary(">", other);
    public SqlExpression GreaterOrEqual(SqlExpression other) => Binary(">=", other);
    public SqlExpression LessThan(SqlExpression other) => Binary("<", other);
    public SqlExpression LessOrEqual(SqlExpression other) => Binary("<=", other);
    public SqlExpression And(SqlExpression other) => Binary("AND", other);
    public SqlExpression Or(SqlExpression other) => Binary("OR", other);
    public SqlExpression Add(SqlExpression other) => Binary("+", other);
    public SqlExpression IsNull() => new(c => "(" + ToSql(c) + " IS NULL)");
    public SqlExpression IsNotNull() => new(c => "(" + ToSql(c) + " IS NOT NULL)");
    public SqlExpression Not() => new(c => "(NOT " + ToSql(c) + ")");
    public SqlExpression ChangedFrom(SqlExpression other) =>
        IsNotNull().And(other.IsNotNull()).And(NotEqualTo(other)).Or(IsNull().And(other.IsNotNull())).Or(IsNotNull().And(other.IsNull()));
    private SqlExpression Binary(string op, SqlExpression other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(c => "(" + ToSql(c) + " " + op + " " + other.ToSql(c) + ")");
    }
    public static SqlExpression Exists(string table, string alias, SqlExpression predicate) => new(c =>
        "EXISTS (SELECT 1 FROM " + c.Table(table) + " " + c.Qualifier(alias) + " WHERE " + predicate.ToSql(c) + ")");
}

/// <summary>Provider syntax plus the row bindings available in a trigger.</summary>
public sealed class SqlExpressionContext
{
    private readonly Func<int, string> parameterName;
    internal List<object> Parameters { get; } = new();
    private readonly System.Collections.Generic.Dictionary<(string Row, string Column), string> hanaRows = new();
    internal System.Collections.Generic.List<string> HanaDeclarations { get; } = new();
    public IDialect Dialect { get; }
    internal Func<string, string> TableResolver { get; set; }
    internal Func<string, string> RowTypeResolver { get; set; }
    public string Table(string name) => TableResolver?.Invoke(name) ?? Dialect.QuoteTableNameIfRequired(name);
    internal TriggerEvent? Event { get; }
    internal bool SqlServer => Dialect is Providers.Impl.SqlServer.SqlServerDialect or Providers.Impl.Sybase.SybaseDialect;
    public SqlExpressionContext(IDialect dialect) => Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    internal SqlExpressionContext(ITransformationProvider p) : this(p.Dialect)
    { parameterName = p.GenerateParameterName; TableResolver = p.QuoteTableNameIfRequired; }
    internal string Bind(object value)
    {
        if (parameterName == null) throw new NotSupportedException("Bind parameters are available in data operations, not schema DDL. Use Value for DDL constants.");
        var name = parameterName(Parameters.Count); Parameters.Add(value);
        if (Dialect is Providers.Impl.Firebird.FirebirdDialect or Providers.Impl.DB2.DB2Dialect or Providers.Impl.Informix.InformixDialect)
        {
            if (value is decimal number)
            {
                var scale = (decimal.GetBits(number)[3] >> 16) & 255;
                var precision = 29; // Every CLR decimal fits without discarding its scale.
                if (Dialect is Providers.Impl.Firebird.FirebirdDialect)
                {
                    var digits = decimal.Abs(number).ToString(System.Globalization.CultureInfo.InvariantCulture).Replace(".", "").TrimStart('0').Length;
                    precision = Math.Max(scale, Math.Max(1, digits)) <= 18 ? 18 : 38;
                }
                var type = string.Format(System.Globalization.CultureInfo.InvariantCulture, "DECIMAL({0},{1})", precision, scale);
                return "CAST(" + name + " AS " + type + ")";
            }
            if (value is DateTime)
                return "CAST(" + name + " AS " + (Dialect is Providers.Impl.DB2.DB2Dialect ? "TIMESTAMP(7)" : Dialect.GetTypeName(DbType.DateTime)) + ")";
            // A standalone SELECT marker has no type even when INSERT names a
            // typed target column. Give fixed-width CLR scalars their SQL type.
            DbType? scalarType = Type.GetTypeCode(value?.GetType()) switch
            {
                TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 => DbType.Int16,
                TypeCode.UInt16 or TypeCode.Int32 => DbType.Int32,
                TypeCode.UInt32 or TypeCode.Int64 => DbType.Int64,
                TypeCode.Single => DbType.Single, TypeCode.Double => DbType.Double,
                TypeCode.Boolean => DbType.Boolean, _ => null
            };
            if (scalarType.HasValue) return "CAST(" + name + " AS " + Dialect.GetTypeName(scalarType.Value) + ")";
        }
        // These engines infer a CASE marker's width from the other arm, which
        // can truncate a longer bound string unless its SQL type is explicit.
        if (value is string text)
        {
            if (Dialect is Providers.Impl.Firebird.FirebirdDialect)
            {
                // UTF8 VARCHAR has at most 8191 characters. Larger markers need
                // a text BLOB descriptor, never the dialect's default VARCHAR(255).
                var type = text.Length <= 8191 ? "VARCHAR(" + Math.Max(1, text.Length) + ") CHARACTER SET UTF8" : "BLOB SUB_TYPE TEXT CHARACTER SET UTF8";
                return "CAST(" + name + " AS " + type + ")";
            }
            if (Dialect is Providers.Impl.DB2.DB2Dialect)
            {
                var bytes = Math.Max(1, System.Text.Encoding.UTF8.GetByteCount(text));
                var type = bytes <= 32672 ? "VARCHAR" : "CLOB";
                return "CAST(" + name + " AS " + type + "(" + bytes + " OCTETS))";
            }
            if (Dialect is Providers.Impl.Informix.InformixDialect)
                return "CAST(" + name + " AS " + Dialect.GetTypeName(System.Data.DbType.String, Math.Max(1, text.Length)) + ")";
        }
        return name;
    }
    internal SqlExpressionContext(IDialect dialect, TriggerEvent operation) : this(dialect) => Event = operation;
    public string Qualifier(string alias)
    {
        if (Event != null)
        {
            var reserved = SqlServer ? new[] { "i", "d" }
                : Dialect is Providers.Impl.DB2.DB2Dialect or Providers.Impl.Informix.InformixDialect or Providers.Impl.Hana.HanaDialect
                    ? new[] { "oldrow", "newrow" } : new[] { "OLD", "NEW" };
            if (reserved.Contains(alias, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("This alias is reserved for trigger row bindings; use another related-table alias.");
        }
        return Dialect.QuoteColumnNameIfRequired(alias);
    }
    internal string Row(string row, string name)
    {
        if (Event == null || row == "OLD" && Event == TriggerEvent.Insert || row == "NEW" && Event == TriggerEvent.Delete)
            throw new ArgumentException(row + " is unavailable in this expression context.");
        if (Dialect is Providers.Impl.Hana.HanaDialect)
        {
            if (!hanaRows.TryGetValue((row, name), out var local))
            {
                local = "guard_row" + hanaRows.Count;
                hanaRows.Add((row, name), local);
                var type = RowTypeResolver?.Invoke(name) ?? throw new NotSupportedException("HANA trigger row bindings require native column types from a connected provider; offline preview is unavailable.");
                HanaDeclarations.Add("DECLARE " + local + " " + type + " = :" + (row == "OLD" ? "oldrow" : "newrow") + "." + Dialect.QuoteColumnNameIfRequired(name) + ";");
            }
            return ":" + local;
        }
        var binding = Dialect switch
        {
            Providers.Impl.Oracle.OracleDialect => ":" + row,
            Providers.Impl.Hana.HanaDialect => ":" + (row == "OLD" ? "oldrow" : "newrow"),
            Providers.Impl.DB2.DB2Dialect or Providers.Impl.Informix.InformixDialect => row == "OLD" ? "oldrow" : "newrow",
            _ => SqlServer ? (row == "OLD" ? "d" : "i") : row
        };
        return binding + "." + Dialect.QuoteColumnNameIfRequired(name);
    }
    public string Literal(object value)
    {
        if (value is null or DBNull && Dialect is Providers.Impl.Informix.InformixDialect)
            return "CAST(NULL AS VARCHAR(1))";
        if (value is string text)
        {
            if (Dialect is Providers.Impl.Mysql.MysqlDialect)
                return "CONVERT(X'" + Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(text)) + "' USING utf8mb4)";
            if (Dialect is Providers.Impl.SqlServer.SqlServerDialect)
                return "N'" + text.Replace("'", "''") + "'";
            if (Dialect is Providers.Impl.PostgreSQL.PostgreSQLDialect)
                return "E'" + text.Replace("\\", "\\\\").Replace("'", "''") + "'";
        }
        if (value is bool boolean)
        {
            if (Dialect is Providers.Impl.Firebird.FirebirdDialect or Providers.Impl.Hana.HanaDialect or Providers.Impl.DB2.DB2Dialect)
                return boolean ? "TRUE" : "FALSE";
            if (Dialect is Providers.Impl.Informix.InformixDialect) return boolean ? "'t'" : "'f'";
        }
        // Keep literal semantics identical to structured SQL previews.
        var type = Dialect switch
        {
            Providers.Impl.PostgreSQL.PostgreSQLDialect => ProviderTypes.PostgreSQL,
            Providers.Impl.Oracle.OracleDialect => ProviderTypes.Oracle,
            _ => ProviderTypes.SQLite
        };
        return new SqlGenerationContext(type).Literal(value);
    }
}
