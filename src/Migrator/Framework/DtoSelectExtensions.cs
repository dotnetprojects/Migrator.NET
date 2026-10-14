using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DotNetProjects.Migrator.Framework.TypedSelect;

namespace DotNetProjects.Migrator.Framework;

/// <summary>Materialized, single-table DTO queries for migrations.</summary>
public static class DtoSelectExtensions
{
    /// <summary>
    /// Select public writable DTO properties by column name. Supports comparisons, boolean operators,
    /// null checks, string Contains/StartsWith/EndsWith and captured scalar fields;
    /// unsupported expressions throw before execution.
    /// Comparisons follow database collation and SQL null semantics.
    /// </summary>
    public static IReadOnlyList<T> Select<T>(this ITransformationProvider provider, string table,
        Expression<Func<T, bool>> predicate = null) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        if (provider is Providers.NoOpTransformationProvider) return Array.Empty<T>();
        var mapping = DtoMapping.For(typeof(T));
        var filter = predicate == null ? null : new PredicateTranslator(mapping, predicate.Parameters[0]).Translate(predicate.Body);
        return SelectQuery.Execute<T>(provider, table, mapping, filter);
    }
}
