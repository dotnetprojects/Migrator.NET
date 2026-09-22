using System;
using System.Collections.Generic;
using System.Linq;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

// Tokenize DDL rather than matching identifiers/expressions with regular expressions.
internal static class SQLiteConstraintParser
{
    private sealed record Token(string Text, int Start, int End, bool Quoted = false)
    {
        public bool Is(string value) => !Quoted && Text.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    public static TableConstraint[] Parse(string sql)
    {
        var result = new List<TableConstraint>();
        foreach (var definition in Definitions(Tokenize(sql))) ParseDefinition(sql, definition, result);
        return result.ToArray();
    }

    internal static Dictionary<string, Collation> ColumnCollations(string sql)
    {
        var result = new Dictionary<string, Collation>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(sql)) return result;
        foreach (var definition in Definitions(Tokenize(sql)))
        {
            if (definition.Count == 0 || IsTableConstraint(definition[0])) continue;
            for (var i = 1; i < definition.Count; i++)
            {
                // Defaults and CHECK expressions may contain their own COLLATE operators.
                if (definition[i].Is("(")) i = Close(definition, i);
                else if (definition[i].Is("COLLATE"))
                {
                    if (++i >= definition.Count) throw new MigrationException("Missing SQLite collation name.");
                    result[definition[0].Text] = Collation.Named(definition[i].Text);
                }
            }
        }
        return result;
    }

    internal static bool HasKeyword(string sql, string keyword) => Tokenize(sql).Any(t => t.Is(keyword));

    internal static bool HasUnsupportedRebuildFeatures(string sql)
    {
        var tokens = Tokenize(sql);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Is("STRICT") || tokens[i].Is("GENERATED") || tokens[i].Is("DEFERRABLE")) return true;
            if (i + 1 < tokens.Count &&
                ((tokens[i].Is("WITHOUT") && tokens[i + 1].Is("ROWID")) ||
                 (tokens[i].Is("CREATE") && tokens[i + 1].Is("VIRTUAL")) ||
                 (tokens[i].Is("ON") && tokens[i + 1].Is("CONFLICT")))) return true;
        }
        return false;
    }

    private static bool IsTableConstraint(Token token) =>
        token.Is("CONSTRAINT") || token.Is("PRIMARY") || token.Is("UNIQUE") || token.Is("FOREIGN") || token.Is("CHECK");

    private static IEnumerable<List<Token>> Definitions(List<Token> tokens)
    {
        var start = tokens.FindIndex(t => t.Is("("));
        if (start < 0) throw new MigrationException("SQLite CREATE TABLE has no column definition list.");
        var end = Close(tokens, start);
        var first = start + 1;
        var depth = 0;
        for (var i = first; i <= end; i++)
        {
            if (i == end || (depth == 0 && tokens[i].Is(",")))
            {
                yield return tokens.GetRange(first, i - first);
                first = i + 1;
            }
            else if (tokens[i].Is("(")) depth++;
            else if (tokens[i].Is(")")) depth--;
        }
    }

    private static void ParseDefinition(string sql, List<Token> tokens, List<TableConstraint> result)
    {
        if (tokens.Count == 0) return;
        var tableLevel = IsTableConstraint(tokens[0]);
        var column = tableLevel ? null : tokens[0].Text;
        string name = null;
        for (var i = tableLevel ? 0 : 1; i < tokens.Count; i++)
        {
            if (tokens[i].Is("CONSTRAINT"))
            {
                if (++i >= tokens.Count) throw new MigrationException("Missing SQLite constraint name.");
                name = tokens[i].Text;
            }
            else if (tokens[i].Is("PRIMARY") && i + 1 < tokens.Count && tokens[i + 1].Is("KEY"))
            {
                i++;
                var keys = column == null ? Columns(tokens, ref i) : new[] { column };
                result.Add(new PrimaryKeyConstraint(name, keys)); name = null;
            }
            else if (tokens[i].Is("UNIQUE"))
            {
                var keys = column == null ? Columns(tokens, ref i) : new[] { column };
                result.Add(new UniqueConstraint(name, keys)); name = null;
            }
            else if (tokens[i].Is("CHECK"))
            {
                if (i + 1 >= tokens.Count || !tokens[i + 1].Is("(")) throw new MigrationException("Missing CHECK expression.");
                var close = Close(tokens, i + 1);
                result.Add(new CheckConstraint(name, sql[tokens[i + 1].End..tokens[close].Start]));
                i = close; name = null;
            }
            else if (tokens[i].Is("FOREIGN") && i + 1 < tokens.Count && tokens[i + 1].Is("KEY"))
            {
                i++;
                var children = Columns(tokens, ref i);
                if (++i >= tokens.Count || !tokens[i].Is("REFERENCES")) throw new MigrationException("Missing foreign-key reference.");
                result.Add(Reference(tokens, ref i, name, children)); name = null;
            }
            else if (tokens[i].Is("REFERENCES") && column != null)
            {
                result.Add(Reference(tokens, ref i, name, new[] { column })); name = null;
            }
            else if (tokens[i].Is("(")) i = Close(tokens, i);
        }
    }

    private static ForeignKeyConstraint Reference(List<Token> tokens, ref int index, string name, string[] children)
    {
        if (++index >= tokens.Count) throw new MigrationException("Missing referenced table.");
        var parent = tokens[index].Text;
        string[] parents = [];
        if (index + 1 < tokens.Count && tokens[index + 1].Is("(")) parents = Columns(tokens, ref index);
        var fk = new ForeignKeyConstraint(name, parent, parents, null, children) { OnDelete = "NO ACTION", OnUpdate = "NO ACTION" };
        while (index + 1 < tokens.Count)
        {
            if (tokens[index + 1].Is("ON"))
            {
                index += 2;
                if (index + 1 >= tokens.Count) throw new MigrationException("Incomplete foreign-key action.");
                var delete = tokens[index].Is("DELETE");
                if (!delete && !tokens[index].Is("UPDATE")) throw new MigrationException("Unknown foreign-key action.");
                var action = tokens[++index].Text.ToUpperInvariant();
                if (action is "SET" or "NO")
                {
                    if (++index >= tokens.Count) throw new MigrationException("Incomplete foreign-key action.");
                    action += " " + tokens[index].Text.ToUpperInvariant();
                }
                if (delete) fk.OnDelete = action; else fk.OnUpdate = action;
            }
            else if (tokens[index + 1].Is("MATCH"))
            {
                index += 2;
                if (index >= tokens.Count) throw new MigrationException("Incomplete MATCH clause.");
                fk.Match = tokens[index].Text;
            }
            else break;
        }
        return fk;
    }

    private static string[] Columns(List<Token> tokens, ref int index)
    {
        if (++index >= tokens.Count || !tokens[index].Is("(")) throw new MigrationException("Missing constraint column list.");
        var end = Close(tokens, index);
        var columns = new List<string>();
        for (var i = index + 1; i < end; i += 2)
        {
            columns.Add(tokens[i].Text);
            if (i + 1 < end && !tokens[i + 1].Is(",")) throw new NotSupportedException("Constraint column modifiers require explicit schema support.");
        }
        index = end;
        if (columns.Count == 0) throw new MigrationException("Constraint column list is empty.");
        return columns.ToArray();
    }

    private static int Close(List<Token> tokens, int open)
    {
        var depth = 0;
        for (var i = open; i < tokens.Count; i++)
        {
            if (tokens[i].Is("(")) depth++;
            if (tokens[i].Is(")") && --depth == 0) return i;
        }
        throw new MigrationException("Unbalanced SQLite definition.");
    }

    private static List<Token> Tokenize(string sql)
    {
        var result = new List<Token>();
        for (var i = 0; i < sql.Length;)
        {
            if (char.IsWhiteSpace(sql[i])) { i++; continue; }
            if (i + 1 < sql.Length && sql[i] == '-' && sql[i + 1] == '-') { while (i < sql.Length && sql[i] != '\n') i++; continue; }
            if (i + 1 < sql.Length && sql[i] == '/' && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) throw new MigrationException("Unterminated SQL comment.");
                i = end + 2; continue;
            }
            var start = i;
            if (sql[i] is '\'' or '"' or '`' or '[')
            {
                var close = sql[i++] == '[' ? ']' : sql[start];
                var text = new System.Text.StringBuilder(); var closed = false;
                while (i < sql.Length)
                {
                    var ch = sql[i++];
                    if (ch == close)
                    {
                        if (i < sql.Length && sql[i] == close) { text.Append(close); i++; }
                        else { closed = true; break; }
                    }
                    else text.Append(ch);
                }
                if (!closed) throw new MigrationException("Unterminated quoted SQL token.");
                result.Add(new Token(text.ToString(), start, i, true));
            }
            else if (sql[i] is '(' or ')' or ',' or '.') { result.Add(new Token(sql[i++].ToString(), start, i)); }
            else
            {
                while (i < sql.Length && !char.IsWhiteSpace(sql[i]) && sql[i] is not ('(' or ')' or ',' or '.' or '\'' or '"' or '`' or '[') &&
                    !(i + 1 < sql.Length && ((sql[i] == '/' && sql[i + 1] == '*') || (sql[i] == '-' && sql[i + 1] == '-')))) i++;
                result.Add(new Token(sql[start..i], start, i));
            }
        }
        return result;
    }
}
