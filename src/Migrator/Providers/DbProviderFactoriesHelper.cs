using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace DotNetProjects.Migrator.Providers;

public static class DbProviderFactoriesHelper
{
    public static DbProviderFactory GetFactory(string providerName, string assemblyName, string factoryProviderType)
    {
        if (DbProviderFactories.TryGetFactory(providerName, out var factory) && factory != null)
            return factory;

#if !NETSTANDARD
        if (System.Data.Common.DbProviderFactories.TryGetFactory(providerName, out factory) && factory != null)
            return factory;
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

    private static readonly ConcurrentDictionary<string, Func<DbProviderFactory>> Factories = new(StringComparer.Ordinal);

    public static DbProviderFactory GetFactory(string providerInvariantName)
    {
        if (TryGetFactory(providerInvariantName, out var factory))
        {
            return factory;
        }

        throw new Exception("ConfigProviderNotFound");
    }

    public static void RegisterFactory(string providerInvariantName, Func<DbProviderFactory> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Factories[providerInvariantName] = factory;
    }

    internal static bool TryGetFactory(string providerInvariantName, out DbProviderFactory factory)
    {
        if (Factories.TryGetValue(providerInvariantName, out var createFactory))
        {
            factory = createFactory();
            return true;
        }

        factory = null;
        return false;
    }

    public static IEnumerable<string> GetFactoryProviderNames()
    {
        return Factories.Keys.ToArray();
    }
}
