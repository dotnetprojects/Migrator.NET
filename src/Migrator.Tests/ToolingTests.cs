using System;
using System.Data;
using System.IO;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Extensions.DependencyInjection;
using DotNetProjects.Migrator.Providers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NSubstitute;
namespace Migrator.Tests;

public class ToolingTests
{
    public sealed class Dependency { public bool Activated { get; set; } }
    [Migration(900001, Scope = "tooling-spec")]
    public class InjectedMigration(Dependency dependency) : Migration
    {
        public override void Up() { dependency.Activated = true; Database.AddTable("Injected", new Column("Id", DbType.Int32)); }
        public override void Down() => Database.RemoveTable("Injected");
    }
    [Test, Category("SQLite")]
    public void DependencyInjectionResolvesConstructorAndOptions()
    {
        var services = new ServiceCollection(); var dependency = new Dependency();
        services.AddSingleton(dependency);
        services.AddMigrator(_ => ProviderFactory.Create(ProviderTypes.SQLite, "Data Source=:memory:", null, "tooling-spec"), typeof(ToolingTests).Assembly,
            options => options.TransactionMode = MigrationTransactionMode.WholeSession);
        using var container = services.BuildServiceProvider(); using var scope = container.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<DotNetProjects.Migrator.Migrator>();
        runner.MigrateToLastVersion();
        Assert.That(dependency.Activated, Is.True);
        Assert.That(scope.ServiceProvider.GetRequiredService<ITransformationProvider>().TableExists("Injected"), Is.True);
    }
    [TestCase(new[] { "bad-command" }, 2)]
    [TestCase(new[] { "--help" }, 0)]
    [TestCase(new[] { "rollback", "--provider", "SQLite" }, 2)]
    public void CliReturnsMeaningfulArgumentExitCodes(string[] args, int exit)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.That(MigratorCommand.Run(args, output, error), Is.EqualTo(exit));
    }
    [Migration(900002, Scope = "cli-spec")]
    public class CliMigration : DotNetProjects.Migrator.Framework.Fluent.AutoReversingMigration
    {
        public override void BuildUp(DotNetProjects.Migrator.Framework.Fluent.MigrationBuilder migration)
            => migration.Create.Table("CliExample").WithColumn("Id").AsInt32();
    }
    [Test, Category("SQLite")]
    public void CliMigratesReadsStatusAndRollsBackWithPackagedDriver()
    {
        var file = Path.Combine(Path.GetTempPath(), "migrator-cli-" + Guid.NewGuid().ToString("N") + ".db");
        var environmentName = "MIGRATOR_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(environmentName, "Data Source=" + file + ";Pooling=False");
        try
        {
            foreach (var command in new[] { "migrate", "status", "rollback" })
            {
                using var output = new StringWriter(); using var error = new StringWriter();
                var args = new System.Collections.Generic.List<string> { command, "--assembly", typeof(ToolingTests).Assembly.Location, "--provider", "SQLite", "--scope", "cli-spec", "--connection-env", environmentName };
                if (command == "rollback") args.AddRange(new[] { "--target", "0" });
                Assert.That(MigratorCommand.Run(args.ToArray(), output, error), Is.Zero, error.ToString());
                if (command == "status") Assert.That(output.ToString(), Does.Contain("900002 applied"));
            }
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + file + ";Pooling=False"); connection.Open();
            using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null, "cli-spec");
            Assert.That(provider.TableExists("CliExample"), Is.False);
            Assert.That(((IMigrationHistory)provider).ReadAppliedMigrations(), Is.Empty);
        }
        finally { Environment.SetEnvironmentVariable(environmentName, null); File.Delete(file); }
    }
    [Migration(900003, Scope = "cli-errors")]
    public class FailingCliMigration : Migration
    {
        internal static int Kind;
        public override void Up() => throw Kind switch
        {
            1 => new ArgumentException("SECRET_VALUE"),
            2 => new TimeoutException("SECRET_VALUE"),
            _ => new NotSupportedException("SECRET_VALUE")
        };
        public override void Down() => throw new NotSupportedException();
    }
    [TestCase(1), TestCase(2), TestCase(3), Category("SQLite"), NonParallelizable]
    public void CliClassifiesMigrationBodyExceptionsAsExecutionFailure(int kind)
    {
        var environmentName = "MIGRATOR_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(environmentName, "Data Source=:memory:");
        FailingCliMigration.Kind = kind;
        try
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            var exit = MigratorCommand.Run(new[] { "migrate", "--assembly", typeof(ToolingTests).Assembly.Location, "--provider", "SQLite", "--scope", "cli-errors", "--connection-env", environmentName }, output, error);
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Not.Contain("SECRET_VALUE"));
        }
        finally { Environment.SetEnvironmentVariable(environmentName, null); }
    }
    [Test] public void LoggingAdapterOmitsProviderMessagesAndDoesNotFormatSqlBraces()
    {
        var sink = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger>();
        var logger = new MigrationLogger(sink);
        Assert.DoesNotThrow(() => logger.Log("SECRET_VALUE {"));
        logger.Warn("SECRET_VALUE"); logger.Trace("SECRET_VALUE"); logger.ApplyingDBChange("SECRET_VALUE");
        logger.Exception("SECRET_VALUE", new Exception("SECRET_VALUE"));
        foreach (var call in sink.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "Log"))
            Assert.That(call.GetArguments()[2].ToString(), Does.Not.Contain("SECRET_VALUE"));
    }
    [Test] public void CliCanListWithoutOpeningDatabase()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exit = MigratorCommand.Run(new[] { "list", "--assembly", typeof(ToolingTests).Assembly.Location, "--provider", "SQLite", "--scope", "tooling-spec" }, output, error);
        Assert.That(exit, Is.Zero, error.ToString());
        Assert.That(output.ToString(), Does.Contain("900001"));
    }
}
