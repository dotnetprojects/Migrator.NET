using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests;

internal static class IntervalRegression
{
    internal static void Verify(ITransformationProvider provider, bool native)
    {
        var duration = -TimeSpan.FromDays(2) - new TimeSpan(3, 4, 5) - TimeSpan.FromTicks(1234560);
        provider.AddTable("DurationValues", new Column("Id", DbType.Int32), new Column("Elapsed", MigratorDbType.Interval, duration));
        provider.Insert("DurationValues", ["Id"], [1]);
        provider.Insert("DurationValues", ["Id", "Elapsed"], [2, duration]);
        foreach (var id in new[] { 1, 2 })
        {
            var stored = provider.ExecuteScalar("SELECT Elapsed FROM DurationValues WHERE Id=" + id);
            Assert.That(native ? (TimeSpan)stored : TimeSpan.FromTicks(Convert.ToInt64(stored)), Is.EqualTo(duration));
        }
    }
}
