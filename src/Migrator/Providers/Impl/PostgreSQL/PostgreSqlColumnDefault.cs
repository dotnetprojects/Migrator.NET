using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL;

internal static class PostgreSqlColumnDefault
{
    private static readonly Regex stripSingleQuoteRegEx = new("(?<=')[^']*(?=')");

    internal static void Apply(Column column, string source)
    {
        if (source != null)
        {
            // Catalog casts on literal values retain the existing CLR conversion.
            // All other expressions must survive inspection without evaluation or quoting.
            var parsedDefault = CatalogDefaultValue.Parse(source, column.Type);
            if (parsedDefault == null)
            {
                column.DefaultValue = null;
                return;
            }
            var isCastLiteral = Regex.IsMatch(source,
                @"\A'(?:[^']|'')*'(?:::[A-Za-z0-9_ .\[\](),]+)?\z");
            if (CatalogLiteralConversions.IsText(column.Type))
            {
                var literal = Regex.Match(source, @"\A('(?:[^']|'')*')(?:::[A-Za-z0-9_ .\[\](),]+)?\z");
                column.DefaultValue = literal.Success
                    ? CatalogDefaultValue.Parse(literal.Groups[1].Value, column.Type) : parsedDefault;
            }
            else if (parsedDefault is RawSql && !isCastLiteral)
                column.DefaultValue = parsedDefault;
            else if (IsNumeric(column.Type))
            {
                var match = stripSingleQuoteRegEx.Match(source);
                var literal = match.Success ? match.Value : source;
                if (CatalogLiteralConversions.TryParseNumber(literal, column.Type, preserveSingle: false, out var number))
                    column.DefaultValue = number;
            }
            else if (column.MigratorDbType == MigratorDbType.Time)
            {
                var match = stripSingleQuoteRegEx.Match(source);
                if (!match.Success || !TimeOnly.TryParse(match.Value, CultureInfo.InvariantCulture, out var time))
                    throw new NotSupportedException("Cannot parse PostgreSQL time default: " + source);
                column.DefaultValue = time;
            }
            else if (column.MigratorDbType == MigratorDbType.Interval)
                ApplyInterval(column, source);
            else if (column.MigratorDbType == MigratorDbType.Boolean)
            {
                var truthy = new[] { "TRUE", "YES", "'true'", "on", "'on'", "t", "'t'" };
                var falsy = new[] { "FALSE", "NO", "'false'", "off", "'off'", "f", "'f'" };

                if (truthy.Any(x => x.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    column.DefaultValue = true;
                }
                else if (falsy.Any(x => x.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    column.DefaultValue = false;
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                }
            }
            else if (column.MigratorDbType == MigratorDbType.DateTime || column.MigratorDbType == MigratorDbType.DateTime2)
            {
                if (source.StartsWith("'"))
                {
                    var match = stripSingleQuoteRegEx.Match(source);

                    if (!match.Success)
                    {
                        throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                    }

                    var timeString = match.Value;

                    // We convert to UTC since we restrict date time default values to UTC on default value definition.
                    var dateTimeExtracted = DateTime.ParseExact(timeString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                    column.DefaultValue = dateTimeExtracted;
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                }
            }
            else if (column.MigratorDbType == MigratorDbType.Guid)
            {
                if (source.StartsWith("'"))
                {
                    var match = stripSingleQuoteRegEx.Match(source);

                    if (!match.Success)
                    {
                        throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                    }

                    column.DefaultValue = Guid.Parse(match.Value);
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                }
            }
            else if (column.MigratorDbType == MigratorDbType.Binary)
            {
                if (source.StartsWith("'"))
                {
                    var match = stripSingleQuoteRegEx.Match(source);

                    if (!match.Success)
                    {
                        throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                    }

                    var singleQuoteString = match.Value;

                    if (!singleQuoteString.StartsWith("\\x"))
                    {
                        throw new Exception(@"Postgre \x notation expected.");
                    }

                    var hexString = singleQuoteString.Substring(2);

                    column.DefaultValue = CatalogLiteralConversions.ParseHex(hexString);
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
                }
            }
            else if (column.MigratorDbType == MigratorDbType.DateTimeOffset)
                ApplyDateTimeOffset(column, source);
            else
            {
                throw new NotImplementedException($"{nameof(DbType)} {column.MigratorDbType} not implemented.");
            }
        }
    }
    private static bool IsNumeric(DbType type) => type is DbType.Int16 or DbType.Int32 or DbType.Int64
        or DbType.UInt16 or DbType.UInt32 or DbType.UInt64 or DbType.Single or DbType.Double or DbType.Decimal;

    private static void ApplyInterval(Column column, string source)
    {
        if (source.StartsWith("'"))
        {
            var match = stripSingleQuoteRegEx.Match(source);

            if (!match.Success)
            {
                throw new Exception("Postgre default value for interval: Single quotes around the interval string are expected.");
            }

            var interval = Regex.Match(match.Value, @"^([+-]?)(\d+):(\d{2}):(\d{2}(?:\.\d{1,7})?)$");
            if (!interval.Success) throw new NotSupportedException("Cannot parse interval default: " + source);
            var ticks = decimal.Parse(interval.Groups[2].Value, CultureInfo.InvariantCulture) * TimeSpan.TicksPerHour
                + decimal.Parse(interval.Groups[3].Value, CultureInfo.InvariantCulture) * TimeSpan.TicksPerMinute
                + decimal.Parse(interval.Groups[4].Value, CultureInfo.InvariantCulture) * TimeSpan.TicksPerSecond;
            if (interval.Groups[1].Value == "-") ticks = -ticks;
            column.DefaultValue = TimeSpan.FromTicks(checked((long)ticks));
        }
        else
        {
            // We assume that the value was added using this migrator so we do not interpret things like '2 days 01:02:03' if you
            // added such format you will run into this exception.
            throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}' unexpected pattern.");
        }
    }

    private static void ApplyDateTimeOffset(Column column, string source)
    {
        if (source.StartsWith("'"))
        {
            var match = stripSingleQuoteRegEx.Match(source);

            if (!match.Success)
            {
                throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
            }

            var singleQuoteString = match.Value;

            // 1) Normalize "Z" at the end → "+00:00"
            singleQuoteString = Regex.Replace(singleQuoteString, @"Z$", "+00:00");

            // 2) Normalize offset at the end of the string
            // Cases handled:
            //   +HH       → +HH:00
            //   +HHMM     → +HH:MM
            //   +HH:MM    → stays unchanged
            //   -HH / -HHMM → same logic
            singleQuoteString = Regex.Replace(
                singleQuoteString,
                @"([+-])(\d{2})(?::?(\d{2}))?$",
                m =>
                {
                    var sign = m.Groups[1].Value;      // "+" or "-"
                    var hh = m.Groups[2].Value;        // hours
                    var hasMm = m.Groups[3].Success;   // minutes present?
                    var mm = hasMm ? m.Groups[3].Value : "00";
                    return $"{sign}{hh}:{mm}";
                }
            );

            // 3) Parse using multiple possible formats
            // Supports both space and "T" separator, with/without milliseconds
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss.fffzzz",  // space separator, with ms
                "yyyy-MM-dd HH:mm:sszzz",      // space separator, no ms
                "yyyy-MM-ddTHH:mm:ss.fffzzz",  // ISO8601, with ms
                "yyyy-MM-ddTHH:mm:sszzz"       // ISO8601, no ms
            };

            var dateTimeOffset = DateTimeOffset.ParseExact(
                singleQuoteString,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            );

            column.DefaultValue = dateTimeOffset;
        }
        else
        {
            throw new NotImplementedException($"Cannot parse {source} in column '{column.Name}'");
        }
    }
}
