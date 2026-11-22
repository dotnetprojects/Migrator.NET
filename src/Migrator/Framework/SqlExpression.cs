using System;
using System.Linq;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Framework.Fluent;

namespace DotNetProjects.Migrator.Framework;

/// <summary>A composable server expression. Raw SQL is an explicit provider-specific escape hatch.</summary>
public sealed class SqlExpression
{
    private readonly Func<SqlExpressionContext, string> render;
    internal SqlExpression(Func<SqlExpressionContext, string> render) => this.render = render;
    public string ToSql(SqlExpressionContext context) => render(context);
    public static SqlExpression Column(string name, string alias = null) => new(c =>
        (alias == null ? "" : c.Qualifier(alias) + ".") + c.Dialect.QuoteColumnNameIfRequired(name));
    public static SqlExpression Old(string name) => new(c => c.Row("OLD", name));
    public static SqlExpression New(string name) => new(c => c.Row("NEW", name));
    public static SqlExpression Value(object value) => new(c => c.Literal(value));
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
    private readonly System.Collections.Generic.Dictionary<(string Row, string Column), string> hanaRows = new();
    internal System.Collections.Generic.List<string> HanaDeclarations { get; } = new();
    public IDialect Dialect { get; }
    internal Func<string, string> TableResolver { get; set; }
    internal Func<string, string> RowTypeResolver { get; set; }
    public string Table(string name) => TableResolver?.Invoke(name) ?? Dialect.QuoteTableNameIfRequired(name);
    internal TriggerEvent? Event { get; }
    internal bool SqlServer => Dialect is Providers.Impl.SqlServer.SqlServerDialect or Providers.Impl.Sybase.SybaseDialect;
    public SqlExpressionContext(IDialect dialect) => Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
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
