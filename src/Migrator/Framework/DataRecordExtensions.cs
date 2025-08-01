using System;
using System.Data;

namespace Migrator.Framework;

public static class DataRecordExtensions
{
    public static T TryParse<T>(this IDataRecord record, string name)
    {
        return TryParse(record, name, () => default(T));
    }

    public static T TryParse<T>(this IDataRecord record, string name, Func<T> defaultValue)
    {
        var value = record[name];

        var type = typeof(T);

        if (value == null || value == DBNull.Value)
        {
            return defaultValue();
        }

        if (type == typeof(DateTime?) || type == typeof(DateTime))
        {
            return (T)(object)(Convert.ToDateTime(value));
        }

        if (type == typeof(Guid) || type == typeof(Guid?))
        {
            if (value is byte[])
            {
                return (T)(object)new Guid((byte[])value);
            }

            return (T)((object)new Guid(value.ToString()));
        }

        if (type == typeof(string))
        {
            return (T)((object)value.ToString());
        }

        if (type == typeof(int?) || type == typeof(int))
        {
            return (T)(object)Convert.ToInt32(value);
        }

        if (type == typeof(long?) || type == typeof(long))
        {
            return (T)(object)Convert.ToInt64(value);
        }

        if (type == typeof(bool) || type == typeof(bool?))
        {
            if (value is int || value is long || value is short || value is ushort || value is uint || value is ulong)
            {
                var intValue = Convert.ToInt64(value);
                return (T)(object)(intValue != 0);
            }

            if (value is string)
            {
                bool result;
                if (bool.TryParse((string)value, out result))
                {
                    return (T)(object)result;
                }
            }

            return (T)value;
        }

        try
        {
            return (T)value;
        }
        catch (InvalidCastException ex)
        {
            throw new MigrationException(string.Format("Invalid cast exception of value: {0} of type: {1} to type: {2} (field name: {3})", value, value.GetType(), typeof(T), name), ex);
        }
    }
}