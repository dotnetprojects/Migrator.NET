using System;
using System.Text.RegularExpressions;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

public class SQLiteCreateTableScriptReader
{
    /// <summary>
    /// Returns the content of the parenthesis. Ensures that the content between the first parenthesis and the last parenthesis is extracted.
    /// </summary>
    /// <param name="createTableScript"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string GetParenthesisContent(string createTableScript)
    {
        // No GeneratedRegexAttribute due to old .NET version
        var regEx = new Regex(@"(?<=\()[\s\S]*(?=\)(?![\s\S]*\)))");

        var match = regEx.Match(createTableScript);

        if (!match.Success)
        {
            throw new Exception("Cannot parse script");
        }

        return match.Value;
    }
}