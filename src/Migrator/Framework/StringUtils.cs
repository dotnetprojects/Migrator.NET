using System;
using System.Text;
using System.Text.RegularExpressions;

namespace DotNetProjects.Migrator.Framework;

public class StringUtils
{
    /// <summary>
    /// Convert a classname to something more readable.
    /// ex.: CreateATable => Create a table
    /// </summary>
    /// <param name="className"></param>
    /// <returns></returns>
    public static string ToHumanName(string className)
    {
        ArgumentNullException.ThrowIfNull(className);
        var name = Regex.Replace(className, "^[_0-9]*|[_0-9]*$", "");

        name = Regex.Replace(name, "([A-Z])", " $1").TrimStart();
        if (name.Length == 0) return name;

        return name.Substring(0, 1).ToUpper() + name.Substring(1).ToLower();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="template"></param>
    /// <param name="placeholder"></param>
    /// <param name="replacement"></param>
    /// <returns></returns>
    public static string ReplaceOnce(string template, string placeholder, string replacement)
    {
        var loc = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (loc < 0)
        {
            return template;
        }
        else
        {
            return new StringBuilder(template.Substring(0, loc))
                .Append(replacement)
                .Append(template.Substring(loc + placeholder.Length))
                .ToString();
        }
    }
}
