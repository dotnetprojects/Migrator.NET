using DotNetProjects.Migrator.Providers.Impl.SQLite;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLite.SQLiteReader;

[TestFixture]
[Category("SQLite")]
public class SQLiteReaderTests
{
    private SQLiteCreateTableScriptReader _sqliteCreateTableScriptReader = new SQLiteCreateTableScriptReader();

    [Test]
    public void GetParenthesisContent()
    {
        // Arrange 
        var testScript = "CREATE TABLE \"TestTwo\" (Id INTEGER NOT NULL PRIMARY KEY, TestId INTEGER NULL, CONSTRAINT FKName FOREIGN KEY (TestId) REFERENCES Test(IdNew))";

        // Act 
        var parenthesisContent = _sqliteCreateTableScriptReader.GetParenthesisContent(testScript);

        // Assert
        Assert.That(parenthesisContent, Is.EqualTo("Id INTEGER NOT NULL PRIMARY KEY, TestId INTEGER NULL, CONSTRAINT FKName FOREIGN KEY (TestId) REFERENCES Test(IdNew)"));
    }
}