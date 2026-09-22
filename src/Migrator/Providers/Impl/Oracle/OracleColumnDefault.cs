using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle;

internal static class OracleColumnDefault
{
    internal static void Apply(Column column, string dataDefaultString)
    {
        var timestampRegex = new Regex(@"(?<=^TIMESTAMP\s+')[^']+(?=')", RegexOptions.IgnoreCase);
        var hexToRawRegex = new Regex(@"(?<=^HEXTORAW\s*\(')[^']+(?=')", RegexOptions.IgnoreCase);
        var timestampBaseFormat = "yyyy-MM-dd HH:mm:ss";

        // dataDefaultString contains ISEQ$$ if the column is an identity column
        if (
            !string.IsNullOrWhiteSpace(dataDefaultString) &&
            !dataDefaultString.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) &&
            !dataDefaultString.Contains("ISEQ$$") &&
            !dataDefaultString.Contains(".nextval"))
        {
            // This is only necessary because older versions of this migrator added single quotes for numerics.
            var singleQuoteStrippedString = dataDefaultString.Replace("'", "");

            var parsedDefault = CatalogDefaultValue.Parse(dataDefaultString, column.Type);
            if (CatalogLiteralConversions.IsText(column.Type)
                || (parsedDefault is RawSql && !Regex.IsMatch(dataDefaultString,
                    @"(?i)^\s*(TO_TIMESTAMP\s*\(|TIMESTAMP\s*'|HEXTORAW\s*\()")))
                column.DefaultValue = parsedDefault;
            else if (CatalogLiteralConversions.TryParseNumber(singleQuoteStrippedString, column.Type, preserveSingle: true, out var number))
                column.DefaultValue = number;
            else if (column.Type == DbType.Boolean)
            {
                column.DefaultValue = dataDefaultString == "1" || dataDefaultString.ToUpper() == "TRUE";
            }
            else if (column.Type == DbType.DateTime || column.Type == DbType.DateTime2)
            {
                if (dataDefaultString.StartsWith("TO_TIMESTAMP("))
                {
                    var expectedOracleToTimestampPattern = "YYYY-MM-DD HH24:MI:SS";

                    if (!dataDefaultString.Contains(expectedOracleToTimestampPattern))
                    {
                        throw new NotSupportedException($"Not supported 'TO_TIMESTAMP' pattern. Expected pattern: {expectedOracleToTimestampPattern}");
                    }

                    var toTimestampRegex = new Regex(@"(?<=^TO_TIMESTAMP\(')[^']+(?=')", RegexOptions.IgnoreCase);
                    var toTimestampMatch = toTimestampRegex.Match(dataDefaultString);
                    var toTimestampDateTimeString = toTimestampMatch.Value;

                    List<string> formats = [];

                    // add formats with .F, .FF, .FFF etc.
                    formats = Enumerable.Range(0, 20).Select((x, y) => $"{timestampBaseFormat}.{new string('F', y + 1)}").ToList();
                    formats.Add(timestampBaseFormat);

                    column.DefaultValue = DateTime.ParseExact(toTimestampDateTimeString, [.. formats], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                }
                else if (timestampRegex.Match(dataDefaultString) is Match timestampMatch && timestampMatch.Success)
                {
                    var millisecondsPattern = column.Size == 0 ? string.Empty : $".{new string('F', column.Size)}";
                    column.DefaultValue = DateTime.ParseExact(timestampMatch.Value, $"yyyy-MM-dd HH:mm:ss{millisecondsPattern}", CultureInfo.InvariantCulture);
                }
                else
                {
                    // Could be system time in many variants
                    column.DefaultValue = dataDefaultString;
                }
            }
            else if (column.Type == DbType.Guid)
            {
                if (hexToRawRegex.Match(dataDefaultString) is Match hexToRawMatch && hexToRawMatch.Success)
                {
                    var bytes = CatalogLiteralConversions.ParseHex(hexToRawMatch.Value);

                    // Oracle uses Big-Endian
                    Array.Reverse(bytes, 0, 4);
                    Array.Reverse(bytes, 4, 2);
                    Array.Reverse(bytes, 6, 2);

                    column.DefaultValue = new Guid(bytes);
                }
                else if (dataDefaultString.StartsWith("'"))
                {
                    var guidString = dataDefaultString.Substring(1, dataDefaultString.Length - 2);

                    column.DefaultValue = Guid.Parse(guidString);
                }
                else
                {
                    column.DefaultValue = dataDefaultString;
                }
            }
            else if (column.Type == DbType.Binary)
            {
                if (hexToRawRegex.Match(dataDefaultString) is Match hexToRawMatch && hexToRawMatch.Success)
                {
                    column.DefaultValue = CatalogLiteralConversions.ParseHex(hexToRawMatch.Value);
                }
                else
                {
                    throw new NotImplementedException($"Cannot parse default value in column '{column.Name}'");
                }
            }
            else
            {
                column.DefaultValue = dataDefaultString;
            }
        }
    }
}
