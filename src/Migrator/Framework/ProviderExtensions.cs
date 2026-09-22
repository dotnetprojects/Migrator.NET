using System;
using System.Data;
using System.IO;
using System.Reflection;
namespace DotNetProjects.Migrator.Framework;

public static class ProviderExtensions
{
    public static T? ExecuteNullableScalar<T>(this ITransformationProvider provider, string sql) where T : struct
    {
        var value = provider.ExecuteScalar(sql);
        if (value == null || value == DBNull.Value) return null;
        if (value is T typed) return typed;
        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static int? GetNullableColumnContentSize(this ITransformationProvider provider, string table, string column)
    {
        // Preserve each dialect's length semantics, distinguishing empty/all-null input first.
        var count = provider.ExecuteScalar($"SELECT COUNT({provider.QuoteColumnNameIfRequired(column)}) FROM {provider.QuoteTableNameIfRequired(table)}");
        return Convert.ToInt64(count) == 0 ? null : provider.GetColumnContentSize(table, column);
    }

    public static void ExecuteResourceScript(this ITransformationProvider provider, Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException("SQL resource not found", name);
        using var reader = new StreamReader(stream);
        provider.ExecuteNonQuery(reader.ReadToEnd());
    }
}
