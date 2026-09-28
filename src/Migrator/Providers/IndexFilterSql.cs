using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;

namespace DotNetProjects.Migrator.Providers;

internal static class IndexFilterSql
{
    internal static List<FilterItem> ParseCreateIndex(string sql, Column[] columns)
    {
        var where = Regex.Match(TopLevel(sql), @"\bWHERE\b", RegexOptions.IgnoreCase);
        return where.Success ? Parse(sql[(where.Index + where.Length)..].TrimEnd().TrimEnd(';'), columns, sqliteIntegers: true) : [];
    }

    internal static List<FilterItem> Parse(string expression, Column[] columns, bool sqliteIntegers = false)
    {
        if (string.IsNullOrWhiteSpace(expression)) return [];
        expression = Unwrap(expression.Trim(), stripCasts: false);
        var visible = TopLevel(expression);
        if (Regex.IsMatch(visible, @"\bOR\b", RegexOptions.IgnoreCase))
            throw new NotSupportedException("Index FilterItems cannot represent OR predicates: " + expression);
        var and = Regex.Match(visible, @"\bAND\b", RegexOptions.IgnoreCase);
        if (and.Success)
            return [.. Parse(expression[..and.Index], columns, sqliteIntegers), .. Parse(expression[(and.Index + and.Length)..], columns, sqliteIntegers)];

        if (TryParseNull(expression, out var nullFilter))
        {
            nullFilter.ColumnName = FindColumn(nullFilter.ColumnName, columns).Name;
            return [nullFilter];
        }

        var comparison = Regex.Match(visible, @"<>|!=|>=|<=|=|>|<");
        if (!comparison.Success)
        {
            // PostgreSQL may simplify boolean equality to the column or NOT column.
            var negated = expression.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase);
            var booleanColumn = FindColumn(negated ? expression[4..] : expression, columns);
            if (booleanColumn.MigratorDbType != MigratorDbType.Boolean)
                throw new NotSupportedException("Unsupported index filter: " + expression);
            return [new FilterItem { ColumnName = booleanColumn.Name, Filter = FilterType.EqualTo, Value = !negated }];
        }

        var column = FindColumn(expression[..comparison.Index], columns);
        var literal = Unwrap(expression[(comparison.Index + comparison.Length)..].Trim());
        var type = comparison.Value switch
        {
            "=" => FilterType.EqualTo, "<>" or "!=" => FilterType.NotEqualTo,
            ">" => FilterType.GreaterThan, ">=" => FilterType.GreaterThanOrEqualTo,
            "<" => FilterType.SmallerThan, "<=" => FilterType.SmallerThanOrEqualTo,
            _ => throw new NotSupportedException("Unsupported index filter: " + expression)
        };
        if (literal.StartsWith("N'", StringComparison.OrdinalIgnoreCase)) literal = literal[1..];
        var quotedLiteral = Regex.IsMatch(literal, @"^'(?:[^']|'')*'$", RegexOptions.Singleline);
        if (quotedLiteral) literal = literal[1..^1].Replace("''", "'");
        var culture = CultureInfo.InvariantCulture;
        object value = column.MigratorDbType switch
        {
            // SQLite INTEGER affinity does not retain the declared CLR integer width.
            MigratorDbType.Byte or MigratorDbType.SByte or MigratorDbType.Int16 or MigratorDbType.Int32 or MigratorDbType.Int64
                or MigratorDbType.UInt16 or MigratorDbType.UInt32 or MigratorDbType.UInt64 when sqliteIntegers
                => long.TryParse(literal, NumberStyles.Integer, culture, out var signed) ? (object)signed : ulong.Parse(literal, culture),
            MigratorDbType.String or MigratorDbType.AnsiString or MigratorDbType.StringFixedLength or MigratorDbType.AnsiStringFixedLength
                when quotedLiteral => literal,
            MigratorDbType.Boolean => literal.ToLowerInvariant() switch
            {
                "1" or "true" => true, "0" or "false" => false,
                _ => throw new NotSupportedException("Unsupported boolean index filter: " + expression)
            },
            MigratorDbType.Byte => byte.Parse(literal, culture),
            MigratorDbType.SByte => sbyte.Parse(literal, culture),
            MigratorDbType.Int16 => short.Parse(literal, culture),
            MigratorDbType.Int32 => int.Parse(literal, culture),
            MigratorDbType.Int64 => long.Parse(literal, culture),
            MigratorDbType.UInt16 => ushort.Parse(literal, culture),
            MigratorDbType.UInt32 => uint.Parse(literal, culture),
            MigratorDbType.UInt64 => ulong.Parse(literal, culture),
            MigratorDbType.Decimal => decimal.Parse(literal, culture),
            _ => throw new NotSupportedException("Unsupported index filter column type: " + column.MigratorDbType)
        };
        return [new FilterItem { ColumnName = column.Name, Filter = type, Value = value }];
    }

    private static Column FindColumn(string operand, Column[] columns)
    {
        var name = Unwrap(operand.Trim());
        if (name.StartsWith('[') && name.EndsWith(']')) name = name[1..^1].Replace("]]", "]");
        else if (name.StartsWith('"') && name.EndsWith('"')) name = name[1..^1].Replace("\"\"", "\"");
        return columns.FirstOrDefault(c => c.Name == name)
            ?? columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException("Unsupported index filter column: " + operand);
    }

    private static string Unwrap(string sql, bool stripCasts = true)
    {
        while (true)
        {
            var visible = TopLevel(sql);
            var cast = visible.IndexOf("::", StringComparison.Ordinal);
            if (stripCasts && cast >= 0)
            {
                if (!Regex.IsMatch(sql[cast..], @"^::(?:text|boolean|integer|bigint|smallint|numeric|character varying|character|bpchar|varchar)(?:\(\d+(?:,\s*\d+)?\))?$", RegexOptions.IgnoreCase))
                    throw new NotSupportedException("Unsupported index filter cast: " + sql);
                sql = sql[..cast].Trim();
                continue;
            }
            if (sql.StartsWith('(') && sql.EndsWith(')') && string.IsNullOrWhiteSpace(visible))
            { sql = sql[1..^1].Trim(); continue; }
            return sql;
        }
    }

    // Hide quoted tokens and parenthesized expressions when finding conjunctions/operators.
    private static string TopLevel(string sql)
    {
        var result = sql.ToCharArray();
        var depth = 0;
        char quote = '\0';
        for (var i = 0; i < sql.Length; i++)
        {
            var ch = sql[i];
            if (quote != '\0')
            {
                result[i] = ' ';
                if (ch != quote) continue;
                if (i + 1 < sql.Length && sql[i + 1] == quote) { result[++i] = ' '; continue; }
                quote = '\0';
                continue;
            }
            if (ch is '\'' or '"' or '[') { quote = ch == '[' ? ']' : ch; result[i] = ' '; continue; }
            if (ch == '(') depth++;
            if (depth > 0) result[i] = ' ';
            if (ch == ')') depth--;
            if (depth < 0) throw new NotSupportedException("Unbalanced index filter: " + sql);
        }
        if (quote != '\0' || depth != 0) throw new NotSupportedException("Unbalanced index filter: " + sql);
        return new string(result);
    }

    internal static string Format(Dialect dialect, FilterItem filter, bool numericBooleans)
    {
        var column = dialect.QuoteColumnNameIfRequired(filter.ColumnName);
        if (filter.Value is null or DBNull)
        {
            return filter.Filter switch
            {
                FilterType.EqualTo => $"{column} IS NULL",
                FilterType.NotEqualTo => $"{column} IS NOT NULL",
                _ => throw new ArgumentException("Null index filters require EqualTo or NotEqualTo.", nameof(filter))
            };
        }

        var comparison = dialect.GetComparisonStringByFilterType(filter.Filter);
        var value = filter.Value switch
        {
            bool b => numericBooleans ? (b ? "1" : "0") : (b ? "TRUE" : "FALSE"),
            string s => "'" + s.Replace("'", "''") + "'",
            byte or short or int or long or sbyte or ushort or uint or ulong => Convert.ToString(filter.Value, CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"Index filters do not support values of type {filter.Value.GetType().Name}.")
        };
        return $"{column} {comparison} {value}";
    }

    // Catalogs preserve different identifier quotes, but use the same NULL predicates.
    internal static bool TryParseNull(string expression, out FilterItem filter)
    {
        filter = null;
        var match = Regex.Match(expression.Trim(), @"^(?<column>.+?)\s+IS\s+(?<not>NOT\s+)?NULL$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        var column = match.Groups["column"].Value.Trim();
        if (column.StartsWith('[') && column.EndsWith(']')) column = column[1..^1].Replace("]]", "]");
        else if (column.StartsWith('"') && column.EndsWith('"')) column = column[1..^1].Replace("\"\"", "\"");
        filter = new FilterItem
        {
            ColumnName = column,
            Filter = match.Groups["not"].Success ? FilterType.NotEqualTo : FilterType.EqualTo,
            Value = null
        };
        return true;
    }
}
