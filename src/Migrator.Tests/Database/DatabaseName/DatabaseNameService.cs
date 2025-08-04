using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.GuidServices.Interfaces;

namespace Migrator.Test.Shared.Database;

public partial class DatabaseNameService(TimeProvider timeProvider, IGuidService guidService) : IDatabaseNameService
{
    private const string TestDatabaseString = "Test";
    private const string TimeStampPattern = "yyyyMMddHHmmssfff";

    public DateTime? ReadTimeStampFromString(string name)
    {
        name = Path.GetFileNameWithoutExtension(name);

        var regex = DateTimeRegex();
        var match = regex.Match(name);

        if (match.Success && DateTime.TryParseExact(match.Value, TimeStampPattern, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var res))
        {
            return res;
        }

        return null;
    }

    public string CreateDatabaseName()
    {
        var dateTimePattern = timeProvider.GetUtcNow()
            .ToString(TimeStampPattern);

        var randomString = string.Concat(guidService.NewGuid()
            .ToString("N")
            .Reverse()
            .Take(9));

        return $"{dateTimePattern}{TestDatabaseString}{randomString}";
    }

    [GeneratedRegex(@"^(\d+)(?=Test.{9}$)")]
    private static partial Regex DateTimeRegex();
}