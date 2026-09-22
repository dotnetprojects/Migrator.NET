using System;
using System.IO;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;
namespace Migrator.Tests;
public class ScriptTests
{
    [Test]
    public void GoInsideMultilineStringsCommentsAndQuotedIdentifiersDoesNotSplit()
    {
        var batches = SqlScriptBatches.SplitSqlServer("SELECT 'line\nGO\n''quoted''';\nGO -- next batch\n/* outer\n/* nested */\nGO\n*/ SELECT [line\nGO\n]]name];\ngo\nSELECT 3;");
        Assert.That(batches.Count, Is.EqualTo(3));
        Assert.That(batches[0], Does.Contain("GO"));
        Assert.That(batches[1], Does.Contain("GO"));
        Assert.That(batches[2].Trim(), Is.EqualTo("SELECT 3;"));
    }
    [TestCase("GO 2")]
    [TestCase(":r other.sql")]
    [TestCase("!! echo value")]
    public void UnsupportedClientCommandsAreRejectedBeforeExecution(string command)
        => Assert.Throws<NotSupportedException>(() => SqlScriptBatches.SplitSqlServer("SELECT 1;\nGO\n" + command));

    [Test, Category("SQLite")]
    public void ExplicitFileAndEmbeddedResourceScriptsPersistData()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"); connection.Open();
        using var provider = ProviderFactory.Create(DotNetProjects.Migrator.Providers.ProviderTypes.SQLite, connection, null);
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "CREATE TABLE ScriptData (Id INTEGER); INSERT INTO ScriptData VALUES (1);");
            provider.ExecuteScript(file);
            provider.ExecuteResourceScript(typeof(ScriptTests).Assembly, "Migrator.Tests.ScriptResource.sql");
            Assert.That(Convert.ToInt64(provider.ExecuteScalar("SELECT SUM(Id) FROM ScriptData")), Is.EqualTo(3));
        }
        finally { File.Delete(file); }
    }
}
