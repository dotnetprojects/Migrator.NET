using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DotNetProjects.Migrator.Framework.Loggers;
using NUnit.Framework;

namespace Migrator.Tests;

public class LoggerBehaviorTests
{
    private sealed class Writer : ILogWriter
    {
        public readonly StringBuilder Output = new();
        public void Write(string message, params object[] args) => Output.Append(args.Length == 0 ? message : string.Format(CultureInfo.InvariantCulture, message, args));
        public void WriteLine(string message, params object[] args) { Write(message, args); Output.AppendLine(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TraceRespectsConfigurationAndDetachedWritersStopReceivingMessages(bool trace)
    {
        var first = new Writer(); var second = new Writer();
        var logger = new Logger(trace, first);
        logger.Attach(second);
        logger.Trace("trace {0}", 7);
        logger.Warn("warning {0}", 8);
        logger.Detach(first);
        logger.Log("after detach");
        Assert.That(first.Output.ToString().Contains("trace 7"), Is.EqualTo(trace));
        Assert.That(second.Output.ToString().Contains("trace 7"), Is.EqualTo(trace));
        Assert.That(first.Output.ToString(), Does.Contain("Warning! : warning 8").And.Not.Contain("after detach"));
        Assert.That(second.Output.ToString(), Does.Contain("after detach"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ErrorOutputRetainsEveryNestedCause(bool versioned)
    {
        var writer = new Writer(); var logger = new Logger(false, writer);
        var cause = new Exception("inner-most");
        var error = new InvalidOperationException("outer", new Exception("middle", cause));
        if (versioned) logger.Exception(42, "Migration", error);
        else logger.Exception("Migration failed", error);
        var text = writer.Output.ToString();
        Assert.That(text, Does.Contain(versioned ? "Error in migration: 42" : "Error: Migration failed"));
        Assert.That(text, Does.Contain("outer").And.Contain("middle").And.Contain("inner-most"));
    }

    [Test]
    public void ProgressOutputDistinguishesEmptyHistoryFromAppliedVersions()
    {
        var writer = new Writer(); var logger = new Logger(false, writer);
        logger.Started(new List<long>(), 10);
        logger.Started(new List<long> { 2, 7 }, 10);
        logger.Started(7, 10);
        logger.MigrateUp(10, "AddUsers");
        logger.MigrateDown(7, "RemoveOld");
        logger.Skipping(8); logger.RollingBack(7); logger.Finished(7, 10);
        Assert.That(writer.Output.ToString(), Does.Contain("No migrations applied yet!")
            .And.Contain("Latest version applied : 7").And.Contain("Current version : 7")
            .And.Contain("Applying 10: AddUsers").And.Contain("Removing 7: RemoveOld")
            .And.Contain("8 <Migration not found>").And.Contain("Rolling back to migration 7").And.Contain("Migrated to version 10"));
    }
}
