using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.SqlServer;

internal static class SqlServerColumnDefault
{
    internal static void Apply(Column column, string defaultValueString)
    {
        if (defaultValueString != null)
        {
            var bracesStrippedString = defaultValueString.Replace("(", "").Replace(")", "").Trim();
            var bracesAndSingleQuoteStrippedString = bracesStrippedString.Replace("'", "");

            var parsedDefault = CatalogDefaultValue.Parse(defaultValueString, column.Type);
            if (parsedDefault == null)
            {
                column.DefaultValue = null;
                return;
            }
            if (CatalogLiteralConversions.IsText(column.Type)
                || (parsedDefault is RawSql && !System.Text.RegularExpressions.Regex.IsMatch(defaultValueString,
                    @"(?i)^\(*\s*(CONVERT\s*\(|0x[0-9a-f]+\)*)")))
                column.DefaultValue = parsedDefault;
            else if (CatalogLiteralConversions.TryParseNumber(bracesAndSingleQuoteStrippedString, column.Type, preserveSingle: false, out var number))
                column.DefaultValue = number;
            else if (column.Type == DbType.Time)
            {
                column.DefaultValue = TimeOnly.Parse(bracesAndSingleQuoteStrippedString, CultureInfo.InvariantCulture);
            }
            else if (column.Type == DbType.Boolean)
            {
                var truthy = new string[] { "'TRUE'", "1" };
                var falsy = new string[] { "'FALSE'", "0" };

                if (truthy.Contains(bracesStrippedString))
                {
                    column.DefaultValue = true;
                }
                else if (falsy.Contains(bracesStrippedString))
                {
                    column.DefaultValue = false;
                }
                else if (bracesStrippedString == "NULL")
                {
                    column.DefaultValue = null;
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse the boolean default value '{defaultValueString}' of column '{column.Name}'");
                }
            }
            else if (column.Type == DbType.DateTime || column.Type == DbType.DateTime2)
            {
                // (CONVERT([datetime],'2000-01-02 03:04:05.000',(121)))
                // 121 is a pattern: it contains milliseconds
                // Search for 121 here: https://learn.microsoft.com/de-de/sql/t-sql/functions/cast-and-convert-transact-sql?view=sql-server-ver17
                var regexDateTimeConvert121 = new Regex(@"(?<=^\(CONVERT\([\[]+datetime[\]]+,')[^']+(?='\s*,\s*\(121\s*\)\)\)$)");
                var match121 = regexDateTimeConvert121.Match(defaultValueString);

                if (match121.Success)
                {
                    // We convert to UTC since we restrict date time default values to UTC on default value definition.
                    column.DefaultValue = DateTime.ParseExact(match121.Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                }
                else if (defaultValueString is string defVal)
                {
                    // Not tested
                    var dt = defVal;
                    if (defVal.StartsWith("'"))
                    {
                        dt = defVal.Substring(1, defVal.Length - 2);
                    }

                    // We convert to UTC since we restrict date time default values to UTC on default value definition.
                    column.DefaultValue = DateTime.ParseExact(dt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                }
                else
                {
                    throw new NotImplementedException($"Cannot interpret {column.DefaultValue} in column '{column.Name}' unexpected pattern.");
                }
            }
            else if (column.Type == DbType.Guid)
            {
                column.DefaultValue = Guid.Parse(bracesAndSingleQuoteStrippedString);
            }
            else if (column.MigratorDbType == MigratorDbType.Binary)
            {
                if (bracesStrippedString.StartsWith("0x"))
                {
                    var hexString = bracesStrippedString.Substring(2);

                    column.DefaultValue = CatalogLiteralConversions.ParseHex(hexString);
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse the binary default value of '{column.Name}'. The value is '{defaultValueString}'");
                }
            }
            else
            {
                throw new NotImplementedException($"Cannot parse the default value of {column.Name} type '{column.MigratorDbType}'. It is not yet implemented - file an issue.");
            }
        }
    }
}
