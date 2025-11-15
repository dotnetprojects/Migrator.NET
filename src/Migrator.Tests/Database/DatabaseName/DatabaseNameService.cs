using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Migrator.Tests.Database.DatabaseName.Interfaces;

namespace Migrator.Test.Shared.Database;

public partial class DatabaseNameService(TimeProvider timeProvider) : IDatabaseNameService
{
    private const string TestDatabaseString = "T";
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

        var randomString = CreateRandomChars(7);

        return $"{dateTimePattern}{TestDatabaseString}{randomString}";
    }

    private string CreateRandomChars(int length)
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var stringChars = new char[length];

        for (var i = 0; i < length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(chars.Length);
            stringChars[i] = chars[index];
        }

        return new string(stringChars);
    }

    [GeneratedRegex(@"^([\d]+)(?=T.{7}$)")]
    private static partial Regex DateTimeRegex();
}
