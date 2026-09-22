using System;
using System.Data;
using System.Globalization;

namespace DotNetProjects.Migrator.Providers;

// Compatibility metadata exposes signed/unsigned integers as Int64/UInt64.
// Oracle retains Single, while SQL Server and PostgreSQL expose it as Double.
internal static class CatalogLiteralConversions
{
    internal static bool IsText(DbType type) =>
        type is DbType.String or DbType.AnsiString or DbType.StringFixedLength or DbType.AnsiStringFixedLength;

    internal static bool TryParseNumber(string value, DbType type, bool preserveSingle, out object result)
    {
        result = type switch
        {
            DbType.Int16 or DbType.Int32 or DbType.Int64 => (object)long.Parse(value, CultureInfo.InvariantCulture),
            DbType.UInt16 or DbType.UInt32 or DbType.UInt64 => ulong.Parse(value, CultureInfo.InvariantCulture),
            DbType.Single when preserveSingle => float.Parse(value, CultureInfo.InvariantCulture),
            DbType.Single or DbType.Double => double.Parse(value, CultureInfo.InvariantCulture),
            DbType.Decimal => decimal.Parse(value, CultureInfo.InvariantCulture),
            DbType.Byte => byte.Parse(value, CultureInfo.InvariantCulture),
            _ => null
        };
        return result != null;
    }

    internal static byte[] ParseHex(string value) => Convert.FromHexString(value);
}
