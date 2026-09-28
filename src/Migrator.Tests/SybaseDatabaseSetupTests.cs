using System;
using System.Collections.Generic;
using System.Data.Common;
using Migrator.Tests.Providers.Live;
using NUnit.Framework;

namespace Migrator.Tests;

public class SybaseDatabaseSetupTests
{
    private sealed class DatabaseError(string message) : DbException(message);

    [Test]
    public void BusyModelIsRetriedBeforeRunningTheTest()
    {
        var calls = 0;
        var delays = new List<TimeSpan>();
        LiveDatabaseTests.CreateSybaseDatabase(() =>
        {
            if (++calls < 3) throw new DatabaseError("MODEL database in use, cannot create new database.");
        }, delays.Add);
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }));
    }

    [Test]
    public void BusyModelStillFailsAfterTheBoundedRetries()
    {
        var error = new DatabaseError("MODEL database in use, cannot create new database.");
        var calls = 0;
        var delays = new List<TimeSpan>();
        Assert.That(Assert.Throws<DatabaseError>(() => LiveDatabaseTests.CreateSybaseDatabase(() => { calls++; throw error; }, delays.Add)), Is.SameAs(error));
        Assert.That(calls, Is.EqualTo(5));
        Assert.That(delays, Has.Count.EqualTo(4));
    }

    [TestCase("Database already exists")]
    [TestCase("Permission denied")]
    public void OtherDatabaseErrorsFailImmediately(string message)
    {
        var calls = 0;
        Assert.Throws<DatabaseError>(() => LiveDatabaseTests.CreateSybaseDatabase(() => { calls++; throw new DatabaseError(message); }, _ => Assert.Fail("Must not delay")));
        Assert.That(calls, Is.EqualTo(1));
    }
}
