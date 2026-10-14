using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace DotNetProjects.Migrator.Framework.TypedSelect;

internal sealed class DtoMapping
{
    private static readonly ConcurrentDictionary<Type, DtoMapping> Cache = new();
    internal readonly MappedProperty[] Properties;

    private DtoMapping(Type type)
    {
        Properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true &&
                p.GetIndexParameters().Length == 0 && !p.IsDefined(typeof(NotMappedAttribute)))
            .Select(p => new MappedProperty(p, p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ?? p.Name)).ToArray();
        if (Properties.Length == 0)
            throw new ArgumentException($"DTO '{type.Name}' has no public writable mapped properties.");
        foreach (var property in Properties)
        {
            if (string.IsNullOrWhiteSpace(property.Column) || !IsScalar(property.Property.PropertyType))
                throw new ArgumentException($"DTO property '{type.Name}.{property.Property.Name}' must map to a named scalar column.");
        }
        if (Properties.GroupBy(p => p.Column, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new ArgumentException($"DTO '{type.Name}' maps multiple properties to the same column.");
    }

    internal static DtoMapping For(Type type) => Cache.GetOrAdd(type, t => new DtoMapping(t));

    internal MappedProperty Find(PropertyInfo property) => Properties.FirstOrDefault(p => p.Property == property)
        ?? throw new NotSupportedException($"Property '{property.Name}' is not a mapped writable DTO property.");

    internal static Type ScalarType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    internal static bool IsScalar(Type type)
    {
        type = ScalarType(type);
        return type.IsEnum || type == typeof(string) || type == typeof(byte[]) || type == typeof(Guid) ||
            type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
            type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) ||
            type == typeof(ulong) || type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeOnly) || type == typeof(TimeSpan);
    }

    internal static object ConvertValue(object value, Type target)
    {
        if (value == null || value == DBNull.Value)
        {
            if (!target.IsValueType || Nullable.GetUnderlyingType(target) != null) return null;
            throw new InvalidCastException($"SQL NULL cannot be assigned to '{target.Name}'.");
        }
        target = ScalarType(target);
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum)
            return Enum.ToObject(target, Convert.ChangeType(value, Enum.GetUnderlyingType(target), CultureInfo.InvariantCulture));
        if (target == typeof(Guid))
            return value is byte[] bytes ? new Guid(bytes) : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture));
        if (target == typeof(DateTime))
            return value is string date ? DateTime.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        if (target == typeof(DateTimeOffset))
            return value is DateTime time ? new DateTimeOffset(time)
                : DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (target == typeof(TimeOnly))
            return value is TimeSpan span ? TimeOnly.FromTimeSpan(span) : value is DateTime dateTime ? TimeOnly.FromDateTime(dateTime)
                : TimeOnly.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (target == typeof(TimeSpan))
            return value is long ticks ? TimeSpan.FromTicks(ticks)
                : TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}

internal sealed record MappedProperty(PropertyInfo Property, string Column);
