using System;

namespace DotNetProjects.Migrator.Framework;

/// <summary>An explicit, trusted SQL expression used as a column default.
/// Expressions are provider-specific and are never quoted as string literals.</summary>
public sealed record RawSql
{
    public string Sql { get; }
    private RawSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("A SQL expression is required.", nameof(sql));
        Sql = sql;
    }

    /// <summary>Insert an expression verbatim. Never pass untrusted input.</summary>
    public static RawSql Insert(string sql) => new(sql);
    public override string ToString() => Sql;
}
