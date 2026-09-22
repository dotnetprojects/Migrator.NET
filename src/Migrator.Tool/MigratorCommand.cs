using System.Data.Common;
using System.Reflection;
using System.Runtime.Loader;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Loggers;
using DotNetProjects.Migrator.Providers;

public static class MigratorCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length == 0 || args.Contains("--help")) WriteHelp(output);
            else Execute(CliOptions.Parse(args), output);
            return 0;
        }
        catch (CliUsageException ex) { error.WriteLine("Invalid arguments: " + ex.Option + ". Use --help."); return 2; }
        catch (UnsupportedMigrationFeatureException) { error.WriteLine("The requested operation is unsupported by this provider or preview mode."); return 3; }
        catch (MigrationLockTimeoutException) { error.WriteLine("Migration lock acquisition timed out."); return 4; }
        catch (Exception ex) { error.WriteLine("Migration command failed (" + ex.GetType().Name + "). Exception details are omitted because they may contain credentials or SQL values."); return 1; }
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("migrator <list|status|validate|migrate|rollback|plan|sql> --assembly PATH --provider NAME");
        output.WriteLine("--connection-env NAME (default MIGRATOR_CONNECTION), --scope NAME, --schema NAME, --target VERSION");
        output.WriteLine("--tags a,b --tag-match Any|All --profiles a,b --transaction PerMigration|None|WholeSession");
        output.WriteLine("--timeout SECONDS --lock --lock-timeout SECONDS --output PATH --offline --allow-legacy-preview");
        output.WriteLine("rollback requires --target. Offline SQL assumes empty history. Legacy preview executes trusted arbitrary C#.");
    }

    private static void Execute(CliOptions options, TextWriter output)
    {
        var resolver = new AssemblyDependencyResolver(options.AssemblyPath);
        Assembly Resolving(AssemblyLoadContext context, AssemblyName name)
        {
            var path = resolver.ResolveAssemblyToPath(name);
            return path == null ? null : context.LoadFromAssemblyPath(path);
        }

        AssemblyLoadContext.Default.Resolving += Resolving;
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(options.AssemblyPath);
            var types = MigrationLoader.GetMigrationTypes(assembly).Where(t => InScope(t, options.Scope)).ToArray();
            var versioned = types.Where(t => t.GetCustomAttribute<MigrationAttribute>() != null && Selected(t, options))
                .OrderBy(MigrationLoader.GetMigrationVersion).ToArray();
            var target = options.Target ?? versioned.Select(MigrationLoader.GetMigrationVersion).DefaultIfEmpty(0).Max();

            if (options.Command == "list")
            {
                foreach (var type in versioned) output.WriteLine(MigrationLoader.GetMigrationVersion(type) + " " + type.FullName);
            }
            else if (options.Offline) WriteOfflineSql(options, types, versioned, target, output);
            else ExecuteConnected(options, types, target, output);
        }
        finally { AssemblyLoadContext.Default.Resolving -= Resolving; }
    }

    private static bool InScope(Type type, string scope) =>
        (type.GetCustomAttribute<MigrationAttribute>()?.Scope ?? type.GetCustomAttribute<ProfileAttribute>()?.Scope
            ?? type.GetCustomAttribute<MaintenanceAttribute>()?.Scope) is not string ownScope || ownScope == scope;

    private static bool Selected(Type type, CliOptions options)
    {
        var tags = type.GetCustomAttribute<TagsAttribute>()?.Tags ?? Array.Empty<string>();
        return options.Tags.Length == 0 || (options.TagMatch == TagMatchMode.All ? options.Tags.All(tags.Contains) : options.Tags.Any(tags.Contains));
    }

    private static void WriteOfflineSql(CliOptions options, Type[] types, Type[] versioned, long target, TextWriter output)
    {
        if (options.Command != "sql" || options.ProfilesSpecified || types.Any(t => t.GetCustomAttribute<MaintenanceAttribute>() != null))
            throw new UnsupportedMigrationFeatureException("CLI operation is unsupported.");
        var plan = MigrationPlanner.Create(versioned.Select(MigrationLoader.GetMigrationVersion), Array.Empty<long>(), target);
        var migrations = plan.Select(step => ((IMigration)Activator.CreateInstance(versioned.Single(t => MigrationLoader.GetMigrationVersion(t) == step.Version)), step.IsUp));
        WriteSql(options, MigrationSqlPreview.Generate(options.Provider, migrations, options.AllowLegacyPreview), output);
    }

    private static void ExecuteConnected(CliOptions options, Type[] types, long target, TextWriter output)
    {
        var connectionString = Environment.GetEnvironmentVariable(options.ConnectionEnvironment) ?? throw new CliUsageException("--connection-env");
        var providerName = RegisterDriver(options.Provider);
        using var provider = ProviderFactory.Create(options.Provider, connectionString, options.Schema, options.Scope, providerName);
        if (options.CommandTimeout is { } timeout) provider.CommandTimeout = timeout;
        var runner = new Migrator(provider, false, new Logger(false), types);
        runner.Options.Tags.UnionWith(options.Tags);
        runner.Options.TagMatch = options.TagMatch;
        runner.Options.Profiles.UnionWith(options.Profiles);
        runner.Options.TransactionMode = options.Transaction;
        if (options.UseLock) runner.Options.Lock = new DatabaseMigrationLock();
        runner.Options.LockTimeout = TimeSpan.FromSeconds(options.LockTimeout);

        switch (options.Command)
        {
            case "status":
                foreach (var applied in ((IMigrationHistory)provider).ReadAppliedMigrations()) output.WriteLine(applied + " applied");
                break;
            case "validate":
                _ = runner.Plan(target);
                output.WriteLine("Migration plan is valid.");
                break;
            case "plan":
                foreach (var step in runner.Plan(target)) output.WriteLine(step.Version + (step.IsUp ? " up" : " down"));
                break;
            case "sql":
                WriteSql(options, runner.PreviewSql(target, options.Provider, options.AllowLegacyPreview), output);
                break;
            case "rollback":
                runner.RollbackTo(target);
                output.WriteLine("Rollback completed.");
                break;
            case "migrate":
                runner.MigrateTo(target);
                output.WriteLine("Migration completed.");
                break;
        }
    }

    private static string RegisterDriver(ProviderTypes provider)
    {
        (string name, DbProviderFactory factory) = provider switch
        {
            ProviderTypes.SQLite => ("Microsoft.Data.Sqlite", (DbProviderFactory)Microsoft.Data.Sqlite.SqliteFactory.Instance),
            ProviderTypes.SqlServer or ProviderTypes.SqlServer2005 => ("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance),
            ProviderTypes.PostgreSQL or ProviderTypes.PostgreSQL82 => ("Npgsql", Npgsql.NpgsqlFactory.Instance),
            ProviderTypes.Mysql or ProviderTypes.MariaDB => ("MySql.Data.MySqlClient", MySql.Data.MySqlClient.MySqlClientFactory.Instance),
            ProviderTypes.Oracle => ("Oracle.ManagedDataAccess.Client", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance),
            ProviderTypes.Firebird => ("FirebirdSql.Data.FirebirdClient", FirebirdSql.Data.FirebirdClient.FirebirdClientFactory.Instance),
            _ => throw new UnsupportedMigrationFeatureException("CLI operation is unsupported.")
        };
        System.Data.Common.DbProviderFactories.RegisterFactory(name, factory);
        return name;
    }

    private static void WriteSql(CliOptions options, string sql, TextWriter output)
    {
        if (options.OutputPath is { } path) File.WriteAllText(path, sql);
        else output.WriteLine(sql);
    }
}
