using System;
using System.Linq;
using System.Reflection;

namespace Migrator.Framework.Support;

public static class TransformationProviderUtility
{
    public const int MaxLengthForForeignKeyInOracle = 30;
    //static readonly ILog log = LogManager.GetLogger(typeof (TransformationProviderUtility));
    private static readonly string[] CommonWords = new[] { "Test" };

    public static string CreateForeignKeyName(string tableName, string foreignKeyTableName)
    {
        var fkName = string.Format("FK_{0}_{1}", tableName, foreignKeyTableName);

        return AdjustNameToSize(fkName, MaxLengthForForeignKeyInOracle, true);
    }

    public static string AdjustNameToSize(string name, int totalCharacters, bool removeCommmonWords)
    {
        var adjustedName = name;

        if (adjustedName.Length > totalCharacters)
        {
            if (removeCommmonWords)
            {
                adjustedName = RemoveCommonWords(adjustedName);
            }
        }

        if (adjustedName.Length > totalCharacters)
        {
            adjustedName = adjustedName.Substring(0, totalCharacters);
        }

        if (name != adjustedName)
        {
            //log.WarnFormat("Name has been truncated from: {0} to: {1}", name, adjustedName);
        }

        return adjustedName;
    }

    private static string RemoveCommonWords(string adjustedName)
    {
        foreach (var word in CommonWords)
        {
            if (adjustedName.Contains(word))
            {
                adjustedName = adjustedName.Replace(word, string.Empty);
            }
        }
        return adjustedName;
    }

    public static string FormatTableName(string schema, string tableName)
    {
        return string.IsNullOrEmpty(schema) ? tableName : string.Format("{0}.{1}", schema, tableName);
    }

    public static string GetQualifiedResourcePath(Assembly assembly, string resourceName)
    {
        var resources = assembly.GetManifestResourceNames();

        //resource full name is in format `namespace.resourceName`
        var sqlScriptParts = resourceName.Split('.').Reverse().ToArray();
#if NETSTANDARD
			Func<string, bool> isNameMatch = x => x.Split('.').Reverse().Take(sqlScriptParts.Length).SequenceEqual(sqlScriptParts, StringComparer.CurrentCultureIgnoreCase);
#else
        Func<string, bool> isNameMatch = x => x.Split('.').Reverse().Take(sqlScriptParts.Length).SequenceEqual(sqlScriptParts, StringComparer.InvariantCultureIgnoreCase);
#endif

        //string result = null;
        var foundResources = resources.Where(isNameMatch).ToArray();

        if (foundResources.Length == 0)
        {
            throw new InvalidOperationException(string.Format("Could not find resource named {0} in assembly {1}", resourceName, assembly.FullName));
        }

        if (foundResources.Length > 1)
        {
            throw new InvalidOperationException(string.Format(@"Could not find unique resource named {0} in assembly {1}.Possible candidates are: {2}", resourceName, assembly.FullName, string.Join(Environment.NewLine + "\t", foundResources)));
        }

        return foundResources[0];
    }
}
