using System;
using System.Data;
using System.Globalization;

namespace DotNetProjects.Migrator.Providers;

// Catalogs contain SQL, whereas Column.DefaultValue distinguishes CLR literals
// from expression objects. Keep expressions unquoted when a column is recreated.
internal static class CatalogDefaultValue
{
    private sealed record Expression(string Sql)
    {
        public override string ToString() => Sql;
    }

    internal static object Parse(string source, DbType type)
    {
        var value = source.Trim();
        while (HasOuterParentheses(value)) value = value[1..^1].Trim();
        if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;
        if (value.StartsWith("'") && value.EndsWith("'"))
        {
            var literal = value[1..^1].Replace("''", "'");
            if (type is DbType.Date or DbType.DateTime or DbType.DateTime2 && DateTime.TryParse(literal, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return literal;
        }
        if (type == DbType.Boolean)
        {
            if (bool.TryParse(value, out var boolean)) return boolean;
            if (value is "0" or "1") return value == "1";
        }
        if (type == DbType.Byte && byte.TryParse(value, CultureInfo.InvariantCulture, out var tiny)) return tiny;
        if (type == DbType.Int16 && short.TryParse(value, CultureInfo.InvariantCulture, out var small)) return small;
        if (type == DbType.Int32 && int.TryParse(value, CultureInfo.InvariantCulture, out var integer)) return integer;
        if (type == DbType.Int64 && long.TryParse(value, CultureInfo.InvariantCulture, out var large)) return large;
        if (type is DbType.Decimal or DbType.VarNumeric or DbType.Currency && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
        if (type == DbType.Double && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)) return floating;
        if (type == DbType.Single && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)) return real;
        return new Expression(value);
    }

    private static bool HasOuterParentheses(string value)
    {
        if (!value.StartsWith('(') || !value.EndsWith(')')) return false;
        var depth = 0;
        var quoted = false;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\'')
            {
                if (quoted && i + 1 < value.Length && value[i + 1] == '\'') { i++; continue; }
                quoted = !quoted;
            }
            if (quoted) continue;
            if (value[i] == '(') depth++;
            if (value[i] == ')' && --depth == 0) return i == value.Length - 1;
        }
        return false;
    }
}
