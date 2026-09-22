using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace DotNetProjects.Migrator.Providers;

public static class DbProviderFactoriesHelper
{
    public static DbProviderFactory GetFactory(string providerName, string assemblyName, string factoryProviderType)
    {
        try
        {
            var factory = DbProviderFactories.GetFactory(providerName);
            if (factory != null)
            {
                return factory;
            }
        }
        catch (Exception)
        { }


#if !NETSTANDARD
        try
        {
            var factory = System.Data.Common.DbProviderFactories.GetFactory(providerName);
            if (factory != null)
            {
                return factory;
            }
        }
        catch (Exception)
        { }
#endif

#if NETSTANDARD
        return null;
#else
        var type = Assembly.Load(assemblyName).GetType(factoryProviderType, throwOnError: true);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        // ADO.NET factories commonly expose a singleton and have a private constructor.
        if (type.GetField("Instance", flags)?.GetValue(null) is DbProviderFactory fieldFactory)
            return fieldFactory;
        if (type.GetProperty("Instance", flags)?.GetValue(null) is DbProviderFactory propertyFactory)
            return propertyFactory;
        return (DbProviderFactory)Activator.CreateInstance(type);
#endif
    }
}

public abstract class DbProviderFactories
{

    internal static readonly Dictionary<string, Func<DbProviderFactory>> _configs = new Dictionary<string, Func<DbProviderFactory>>();

    public static DbProviderFactory GetFactory(string providerInvariantName)
    {
        if (_configs.ContainsKey(providerInvariantName))
        {
            return _configs[providerInvariantName]();
        }

        throw new Exception("ConfigProviderNotFound");
    }

    public static void RegisterFactory(string providerInvariantName, Func<DbProviderFactory> factory)
    {
        _configs[providerInvariantName] = factory;
    }

    public static IEnumerable<string> GetFactoryProviderNames()
    {
        return _configs.Keys.ToArray();
    }
}
