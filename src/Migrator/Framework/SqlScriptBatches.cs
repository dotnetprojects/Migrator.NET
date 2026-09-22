using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
namespace DotNetProjects.Migrator.Framework;

/// <summary>Optional provider capability for client-side script separators.</summary>
public interface IScriptBatchProvider
{
    IReadOnlyList<string> SplitScript(string sql);
}

public static class SqlScriptBatches
{
    /// <summary>Split standalone SQL Server GO lines. SQLCMD directives and GO counts are unsupported.</summary>
    public static IReadOnlyList<string> SplitSqlServer(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        var batches = new List<string>();
        var batch = new StringBuilder();
        var quote = '\0';
        var comments = 0;
        using var reader = new StringReader(sql);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (quote == '\0' && comments == 0)
            {
                if (Regex.IsMatch(line, @"^\s*GO\s*(?:--.*)?$", RegexOptions.IgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(batch.ToString())) batches.Add(batch.ToString());
                    batch.Clear();
                    continue;
                }
                if (Regex.IsMatch(line, @"^\s*GO(?:\s|;|$)", RegexOptions.IgnoreCase))
                    throw new NotSupportedException("Only standalone GO with an optional -- comment is supported; counts and other suffixes are not.");
                var command = line.TrimStart();
                if (command.StartsWith(":", StringComparison.Ordinal) || command.StartsWith("!!", StringComparison.Ordinal))
                    throw new NotSupportedException("SQLCMD directives are not supported by script execution.");
            }
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                var next = i + 1 < line.Length ? line[i + 1] : '\0';
                if (comments != 0)
                {
                    if (ch == '/' && next == '*') { comments++; i++; }
                    else if (ch == '*' && next == '/') { comments--; i++; }
                }
                else if (quote != '\0')
                {
                    if (ch == quote)
                    {
                        if (next == quote) i++;
                        else quote = '\0';
                    }
                }
                else if (ch == '-' && next == '-') break;
                else if (ch == '/' && next == '*') { comments++; i++; }
                else if (ch == '\'' || ch == '"') quote = ch;
                else if (ch == '[') quote = ']';
            }
            batch.AppendLine(line);
        }
        if (quote != '\0' || comments != 0) throw new FormatException("The script contains an unterminated string, identifier or comment.");
        if (!string.IsNullOrWhiteSpace(batch.ToString())) batches.Add(batch.ToString());
        return batches;
    }
}
