using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace DotNetProjects.Migrator.Framework.TypedSelect;

internal static class SelectQuery
{
    internal static IReadOnlyList<T> Execute<T>(ITransformationProvider provider, string table, DtoMapping mapping,
        SelectPredicate predicate) where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        var values = new List<object>();
        var sql = "SELECT " + string.Join(", ", mapping.Properties.Select(p => provider.QuoteColumnNameIfRequired(p.Column))) +
            " FROM " + provider.QuoteTableNameIfRequired(table);
        if (predicate != null) sql += " WHERE " + Render(provider, predicate, values);
        using var command = provider.CreateCommand();
        for (var i = 0; i < values.Count; i++) provider.AddQueryParameter(command, i, values[i]);
        using var reader = provider.ExecuteQuery(command, sql);
        return Materialize<T>(reader, mapping);
    }

    internal static IReadOnlyList<T> Materialize<T>(IDataReader reader, DtoMapping mapping) where T : class, new()
    {
        var ordinals = mapping.Properties.Select(p =>
        {
            try { return reader.GetOrdinal(p.Column); }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                throw new DataException($"Query result is missing column '{p.Column}' for '{typeof(T).Name}.{p.Property.Name}'.", ex);
            }
        }).ToArray();
        var result = new List<T>();
        while (reader.Read())
        {
            var dto = new T();
            for (var i = 0; i < mapping.Properties.Length; i++)
            {
                var property = mapping.Properties[i];
                try
                {
                    property.Property.SetValue(dto, DtoMapping.ConvertValue(reader.GetValue(ordinals[i]), property.Property.PropertyType));
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
                {
                    throw new DataException($"Cannot map column '{property.Column}' to '{typeof(T).Name}.{property.Property.Name}' ({property.Property.PropertyType.Name}).", ex);
                }
            }
            result.Add(dto);
        }
        return result;
    }

    internal static string Render(ITransformationProvider provider, SelectPredicate predicate, List<object> values)
    {
        if (predicate is LogicalPredicate logical)
        {
            var left = Render(provider, logical.Left, values);
            if (logical.Operator == ExpressionType.Not) return "(NOT " + left + ")";
            return "(" + left + (logical.Operator == ExpressionType.AndAlso ? " AND " : " OR ") +
                Render(provider, logical.Right, values) + ")";
        }
        if (predicate is StringPredicate text)
        {
            var pattern = provider.Dialect.EscapeLikePattern(text.Value);
            if (text.Method != nameof(string.StartsWith)) pattern = "%" + pattern;
            if (text.Method != nameof(string.EndsWith)) pattern += "%";
            var name = provider.GenerateParameterName(values.Count);
            values.Add(pattern);
            return "(" + provider.QuoteColumnNameIfRequired(text.Property.Column) + " LIKE " + name + " ESCAPE '~')";
        }
        var comparison = (ComparisonPredicate)predicate;
        var column = provider.QuoteColumnNameIfRequired(comparison.Property.Column);
        if (comparison.Value == null)
            return "(" + column + (comparison.Operator == ExpressionType.Equal ? " IS NULL)" : " IS NOT NULL)");
        var operation = comparison.Operator switch
        {
            ExpressionType.Equal => " = ", ExpressionType.NotEqual => " <> ",
            ExpressionType.LessThan => " < ", ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.GreaterThan => " > ", ExpressionType.GreaterThanOrEqual => " >= ",
            _ => throw new NotSupportedException("Unsupported comparison.")
        };
        var parameter = provider.GenerateParameterName(values.Count);
        values.Add(comparison.Value);
        return "(" + column + operation + parameter + ")";
    }
}
