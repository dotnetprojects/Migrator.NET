using System;

namespace DotNetProjects.Migrator.Framework.Extensions;

public static class LinqExtensions
{
    /// <summary>
    /// Is equal to the Contains method in .NET 9. Please remove it after .NET upgrade.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="toBeChecked"></param>
    /// <param name="stringComparison"></param>
    /// <returns></returns>
    public static bool Contains(this string source, string toBeChecked, StringComparison stringComparison)
    {
        return source?.IndexOf(toBeChecked, stringComparison) >= 0;
    }
}