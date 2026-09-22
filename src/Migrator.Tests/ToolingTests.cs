using System;
using System.Data;
using System.IO;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Extensions.DependencyInjection;
using DotNetProjects.Migrator.Providers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
    [Test] public void CliCanListWithoutOpeningDatabase()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exit = MigratorCommand.Run(new[] { "list", "--assembly", typeof(ToolingTests).Assembly.Location, "--provider", "SQLite", "--scope", "tooling-spec" }, output, error);
        Assert.That(exit, Is.Zero, error.ToString());
        Assert.That(output.ToString(), Does.Contain("900001"));
    }
}
