using System;

namespace DotNetProjects.Migrator.Framework;

internal static class SqlLikePattern
{
    internal static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_");
    }
}
