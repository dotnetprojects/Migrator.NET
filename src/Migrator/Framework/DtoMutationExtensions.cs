using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using DotNetProjects.Migrator.Framework.TypedSelect;

namespace DotNetProjects.Migrator.Framework;

/// <summary>Parameterized DTO updates and deletes with required predicates.</summary>
public static class DtoMutationExtensions
{
    /// <summary>Delete matching rows using the same predicates as Select&lt;T&gt;. A predicate is required.</summary>
    public static int Delete<T>(this ITransformationProvider provider, string table,
        Expression<Func<T, bool>> predicate) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(predicate);
        if (provider is Providers.NoOpTransformationProvider) return 0;
        var mapping = DtoMapping.For(typeof(T));
        var filter = new PredicateTranslator(mapping, predicate.Parameters[0]).Translate(predicate.Body);
        var values = new List<object>();
        var sql = "DELETE FROM " + provider.QuoteTableNameIfRequired(table) + " WHERE " + SelectQuery.Render(provider, filter, values);
        return Execute(provider, sql, values);
    }

    /// <summary>
    /// Update only properties assigned in a DTO initializer, using constants/captured scalar values.
    /// Row-dependent assignments and empty initializers are unsupported. A predicate is required.
    /// </summary>
    public static int Update<T>(this ITransformationProvider provider, string table,
        Expression<Func<T, T>> changes, Expression<Func<T, bool>> predicate) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(predicate);
        if (provider is Providers.NoOpTransformationProvider) return 0;
        var mapping = DtoMapping.For(typeof(T));
        var filter = new PredicateTranslator(mapping, predicate.Parameters[0]).Translate(predicate.Body);
        if (changes.Body is not MemberInitExpression initializer || initializer.NewExpression.Arguments.Count != 0 || initializer.Bindings.Count == 0)
            throw new NotSupportedException("Use a non-empty DTO initializer with a parameterless constructor for update assignments.");
        var assignments = new List<string>();
        var values = new List<object>();
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in initializer.Bindings)
        {
            if (binding is not MemberAssignment assignment || binding.Member is not PropertyInfo property)
                throw new NotSupportedException("Update assignments must target mapped writable DTO properties.");
            var mapped = mapping.Find(property);
            if (!assigned.Add(mapped.Column)) throw new ArgumentException($"Column '{mapped.Column}' is assigned more than once.", nameof(changes));
            var value = PredicateTranslator.ReadValue(assignment.Expression);
            if (value != null && !DtoMapping.IsScalar(value.GetType()))
                throw new NotSupportedException("Update values must be scalar constants or captured fields.");
            var parameter = value == null ? "NULL" : provider.GenerateParameterName(values.Count);
            if (value != null) values.Add(value);
            assignments.Add(provider.QuoteColumnNameIfRequired(mapped.Column) + " = " + parameter);
        }
        var sql = "UPDATE " + provider.QuoteTableNameIfRequired(table) + " SET " + string.Join(", ", assignments) +
            " WHERE " + SelectQuery.Render(provider, filter, values);
        return Execute(provider, sql, values);
    }

    private static int Execute(ITransformationProvider provider, string sql, List<object> values)
    {
        using var command = provider.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < values.Count; i++) provider.AddQueryParameter(command, i, values[i]);
        provider.Logger?.Trace(sql);
        provider.Logger?.ApplyingDBChange(sql);
        return command.ExecuteNonQuery();
    }
}
