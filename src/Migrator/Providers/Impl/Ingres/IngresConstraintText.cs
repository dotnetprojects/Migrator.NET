using System;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Ingres;

// iiref_constraints describes key pairs, but referential actions are recorded
// only in iiconstraints.text_segment. Never treat quoted names/literals as SQL.
internal static class IngresConstraintText
{
    private static Match[] Tokens(string text) => Regex.Matches(text,
        "--[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|'(?:''|[^'])*'|\"(?:\"\"|[^\"])*\"|[A-Za-z_][A-Za-z_0-9]*|\\S")
        .Cast<Match>().Where(m => !m.Value.StartsWith("--") && !m.Value.StartsWith("/*")).ToArray();

    internal static (string Delete, string Update) Actions(string text)
    {
        var tokens = Tokens(text).Select(m => m.Value.ToUpperInvariant()).ToArray();
        var delete = "NO ACTION";
        var update = "NO ACTION";
        for (var i = 0; i + 2 < tokens.Length; i++)
        {
            if (tokens[i] != "ON" || tokens[i + 1] is not ("DELETE" or "UPDATE")) continue;
            var action = tokens[i + 2];
            if (action is "NO" or "SET" && i + 3 < tokens.Length) action += " " + tokens[i + 3];
            if (action is not ("CASCADE" or "RESTRICT" or "NO ACTION" or "SET NULL"))
                throw new MigrationException("Unknown Ingres referential action: " + action);
            if (tokens[i + 1] == "DELETE") delete = action; else update = action;
        }
        return (delete, update);
    }

    internal static string CheckExpression(string text)
    {
        var check = Tokens(text).FirstOrDefault(m => m.Value.Equals("CHECK", StringComparison.OrdinalIgnoreCase));
        if (check == null) throw new MigrationException("Missing CHECK expression in Ingres catalog.");
        return ConstraintMetadataReader.CheckExpression(text[check.Index..]);
    }
}
