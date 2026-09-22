using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetProjects.Migrator.Providers;

// SQL object paths and identifier atoms are different: a constraint named
// "a.b" is one atom, whereas a table named schema.table is a two-part path.
internal static class SqlIdentifier
{
    internal readonly record struct Part(string Value, bool Quoted);

    internal static bool IsSimple(string value) => Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_$#]*$");

    internal static Part[] Parse(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An object name is required.", nameof(name));
        var parts = new List<Part>();
        for (var i = 0; i < name.Length;)
        {
            while (i < name.Length && char.IsWhiteSpace(name[i])) i++;
            if (i == name.Length) throw new ArgumentException("Empty identifier component.", nameof(name));
            var quoted = name[i] is '"' or '[' or '`';
            var value = new StringBuilder();
            if (quoted)
            {
                var closing = name[i] == '[' ? ']' : name[i];
                i++;
                var closed = false;
                while (i < name.Length)
                {
                    var c = name[i++];
                    if (c != closing) { value.Append(c); continue; }
                    if (i < name.Length && name[i] == closing) { value.Append(c); i++; continue; }
                    closed = true;
                    break;
                }
                if (!closed) throw new ArgumentException("Unclosed identifier delimiter.", nameof(name));
                while (i < name.Length && char.IsWhiteSpace(name[i])) i++;
                if (i < name.Length && name[i] != '.') throw new ArgumentException("Unexpected text after quoted identifier.", nameof(name));
            }
            else
            {
                while (i < name.Length && name[i] != '.') value.Append(name[i++]);
            }
            var atom = quoted ? value.ToString() : value.ToString().Trim();
            if (atom.Length == 0) throw new ArgumentException("Empty identifier component.", nameof(name));
            parts.Add(new Part(atom, quoted));
            if (i < name.Length && ++i == name.Length) throw new ArgumentException("Empty identifier component.", nameof(name));
        }
        return parts.ToArray();
    }

    internal static string Render(Dialect dialect, string name, bool alwaysQuote) =>
        string.Join(".", Parse(name).Select(p => alwaysQuote || p.Quoted || !IsSimple(p.Value) || dialect.IsReservedWord(p.Value)
            ? dialect.QuoteIdentifier(p.Value) : p.Value));

    internal static (string Schema, string Name) Catalog(string name, bool upperCase = false)
    {
        var parts = Parse(name);
        if (parts.Length > 2) throw new NotSupportedException("Use a schema and object name, without a database/server prefix.");
        string Value(Part p) => upperCase && !p.Quoted ? p.Value.ToUpperInvariant() : p.Value;
        return (parts.Length == 2 ? Value(parts[0]) : null, Value(parts[^1]));
    }
}
