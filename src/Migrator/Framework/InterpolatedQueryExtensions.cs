using System;
using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework.TypedSelect;

namespace DotNetProjects.Migrator.Framework;

/// <summary>Interpolated SQL queries with provider quoting, native parameters and materialized results.</summary>
public static class InterpolatedQueryExtensions
{
    /// <summary>Map columns to a DTO using the same property/column mapping as Select&lt;T&gt;.</summary>
    public static IReadOnlyList<T> Query<T>(this ITransformationProvider provider, FormattableString sql)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(sql);
        if (provider is Providers.NoOpTransformationProvider) return Array.Empty<T>();
        var mapping = DtoMapping.For(typeof(T));
        using var command = Prepare(provider, sql);
        using var reader = provider.ExecuteQuery(command, command.CommandText);
        return SelectQuery.Materialize<T>(reader, mapping);
    }

    /// <summary>Map each result row to a DTO, tuple or other value. The mapper does not own the reader.</summary>
    public static IReadOnlyList<T> Query<T>(this ITransformationProvider provider, FormattableString sql,
        Func<IDataReader, T> mapper)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(mapper);
        if (provider is Providers.NoOpTransformationProvider) return Array.Empty<T>();
        using var command = Prepare(provider, sql);
        using var reader = provider.ExecuteQuery(command, command.CommandText);
        var result = new List<T>();
        while (reader.Read()) result.Add(mapper(reader));
        return result;
    }

    private static IDbCommand Prepare(ITransformationProvider provider, FormattableString sql)
    {
        var formatter = new QueryFormatter(provider);
        // The framework parser handles escaped braces, alignment and repeated placeholders.
        // Every placeholder occurrence gets its own parameter for positional drivers.
        var text = string.Format(formatter, sql.Format, sql.GetArguments());
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var command = provider.CreateCommand();
        try
        {
            command.CommandText = text;
            for (var i = 0; i < formatter.Values.Count; i++) provider.AddQueryParameter(command, i, formatter.Values[i]);
            return command;
        }
        catch { command.Dispose(); throw; }
    }

    private sealed class QueryFormatter(ITransformationProvider provider) : IFormatProvider, ICustomFormatter
    {
        internal List<object> Values { get; } = new();
        public object GetFormat(Type formatType) => formatType == typeof(ICustomFormatter) ? this : null;

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg is SqlColumn column)
            {
                ValidateIdentifierFormat(format, "column");
                ArgumentException.ThrowIfNullOrWhiteSpace(column.Name);
                return provider.QuoteColumnNameIfRequired(column.Name);
            }
            if (arg is SqlTable table)
            {
                ValidateIdentifierFormat(format, "table");
                ArgumentException.ThrowIfNullOrWhiteSpace(table.Name);
                return provider.QuoteTableNameIfRequired(table.Name);
            }
            if (!string.IsNullOrEmpty(format))
            {
                if (format is not ("column" or "table"))
                    throw new FormatException($"Unsupported SQL interpolation format '{format}'. Use :column or :table for identifiers.");
                if (arg is not string identifier || string.IsNullOrWhiteSpace(identifier))
                    throw new ArgumentException("SQL identifier interpolation requires a non-empty string.");
                return format == "column" ? provider.QuoteColumnNameIfRequired(identifier) : provider.QuoteTableNameIfRequired(identifier);
            }
            if (arg != null && arg != DBNull.Value && !DtoMapping.IsScalar(arg.GetType()))
                throw new NotSupportedException($"SQL interpolation value type '{arg.GetType().Name}' is not a supported scalar.");
            var name = provider.GenerateParameterName(Values.Count);
            Values.Add(arg);
            return name;
        }

        private static void ValidateIdentifierFormat(string format, string expected)
        {
            if (!string.IsNullOrEmpty(format) && format != expected)
                throw new FormatException($"A SQL {expected} wrapper cannot use format '{format}'.");
        }
    }
}
