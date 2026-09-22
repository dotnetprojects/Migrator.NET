using System.Reflection;
using System.Runtime.Loader;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Loggers;
using DotNetProjects.Migrator.Providers;

return MigratorCommand.Run(args, Console.Out, Console.Error);

public static class MigratorCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try { return Execute(args, output); }
        catch (ArgumentException ex) { error.WriteLine("Invalid arguments: " + ex.ParamName + ". Use --help."); return 2; }
        catch (NotSupportedException) { error.WriteLine("The requested operation is unsupported by this provider or preview mode."); return 3; }
        catch (TimeoutException) { error.WriteLine("Migration lock acquisition timed out."); return 4; }
        catch (Exception ex) { error.WriteLine("Migration command failed (" + ex.GetType().Name + "). Exception details are omitted because they may contain credentials or SQL values."); return 1; }
    }
    private static int Execute(string[] args, TextWriter output)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            output.WriteLine("migrator <list|status|validate|migrate|rollback|plan|sql> --assembly PATH --provider NAME");
            output.WriteLine("--connection-env NAME (default MIGRATOR_CONNECTION), --scope NAME, --schema NAME, --target VERSION");
            output.WriteLine("--tags a,b --tag-match Any|All --profiles a,b --transaction PerMigration|None|WholeSession");
            output.WriteLine("--timeout SECONDS --lock --lock-timeout SECONDS --output PATH --offline --allow-legacy-preview");
            output.WriteLine("rollback requires --target. Offline SQL assumes empty history. Legacy preview executes trusted arbitrary C#.");
            return 0;
        }
        var command = args[0];
        if (!new[] { "list", "status", "validate", "migrate", "rollback", "plan", "sql" }.Contains(command)) throw new ArgumentException(null, "command");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string> { "--lock", "--offline", "--allow-legacy-preview" };
        var allowed = new HashSet<string> { "--assembly", "--provider", "--connection-env", "--scope", "--schema", "--target", "--tags", "--tag-match", "--profiles", "--transaction", "--timeout", "--lock-timeout", "--output" };
        for (var i = 1; i < args.Length; i++)
        {
            var key = args[i];
            if (values.ContainsKey(key)) throw new ArgumentException(null, key);
            if (flags.Contains(key)) values.Add(key, "true");
            else if (allowed.Contains(key) && i + 1 < args.Length && !args[i + 1].StartsWith("--")) values.Add(key, args[++i]);
            else throw new ArgumentException(null, key);
        }
        string Value(string key, string fallback = null) => values.GetValueOrDefault(key, fallback);
        T EnumValue<T>(string key, string fallback) where T : struct, Enum => Enum.TryParse<T>(Value(key, fallback), true, out var result) && Enum.IsDefined(result) ? result : throw new ArgumentException(null, key);
        var providerType = EnumValue<ProviderTypes>("--provider", "none");
        if (providerType == ProviderTypes.none) throw new ArgumentException(null, "--provider");
        var assemblyPath = Path.GetFullPath(Value("--assembly") ?? throw new ArgumentException(null, "--assembly"));
        var resolver = new AssemblyDependencyResolver(assemblyPath);
        Assembly Resolving(AssemblyLoadContext context, AssemblyName name)
        {
            var path = resolver.ResolveAssemblyToPath(name);
            return path == null ? null : context.LoadFromAssemblyPath(path);
        }
        AssemblyLoadContext.Default.Resolving += Resolving;
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            var scope = Value("--scope", "default");
            var types = MigrationLoader.GetMigrationTypes(assembly).Where(t =>
                (t.GetCustomAttribute<MigrationAttribute>()?.Scope ?? t.GetCustomAttribute<ProfileAttribute>()?.Scope ?? t.GetCustomAttribute<MaintenanceAttribute>()?.Scope) is not string ownScope || ownScope == scope).ToArray();
            var tags = Value("--tags", "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var tagMatch = EnumValue<TagMatchMode>("--tag-match", "Any");
            bool Selected(Type t)
            {
                var own = t.GetCustomAttribute<TagsAttribute>()?.Tags ?? Array.Empty<string>();
                return tags.Length == 0 || (tagMatch == TagMatchMode.All ? tags.All(own.Contains) : tags.Any(own.Contains));
            }
            var versioned = types.Where(t => t.GetCustomAttribute<MigrationAttribute>() != null && Selected(t)).OrderBy(MigrationLoader.GetMigrationVersion).ToArray();
            var target = Value("--target") is { } targetString ? long.TryParse(targetString, out var parsed) && parsed >= 0 ? parsed : throw new ArgumentException(null, "--target") : versioned.Select(MigrationLoader.GetMigrationVersion).DefaultIfEmpty(0).Max();
            if (command == "rollback" && !values.ContainsKey("--target")) throw new ArgumentException(null, "--target");
            if (command == "list")
            {
                foreach (var type in versioned) output.WriteLine(MigrationLoader.GetMigrationVersion(type) + " " + type.FullName);
                return 0;
            }
            if (values.ContainsKey("--offline"))
            {
                if (command != "sql" || values.ContainsKey("--profiles") || types.Any(t => t.GetCustomAttribute<MaintenanceAttribute>() != null)) throw new NotSupportedException();
                var plan = MigrationPlanner.Create(versioned.Select(MigrationLoader.GetMigrationVersion), Array.Empty<long>(), target);
                var migrations = plan.Select(step => ((IMigration)Activator.CreateInstance(versioned.Single(t => MigrationLoader.GetMigrationVersion(t) == step.Version)), step.IsUp));
                Write(MigrationSqlPreview.Generate(providerType, migrations, values.ContainsKey("--allow-legacy-preview")));
                return 0;
            }
            var connectionString = Environment.GetEnvironmentVariable(Value("--connection-env", "MIGRATOR_CONNECTION")) ?? throw new ArgumentException(null, "--connection-env");
            var providerName = providerType switch
            {
                ProviderTypes.SQLite => "Microsoft.Data.Sqlite", ProviderTypes.SqlServer or ProviderTypes.SqlServer2005 => "Microsoft.Data.SqlClient",
                ProviderTypes.PostgreSQL or ProviderTypes.PostgreSQL82 => "Npgsql", ProviderTypes.Mysql or ProviderTypes.MariaDB => "MySql.Data.MySqlClient",
                ProviderTypes.Oracle => "Oracle.ManagedDataAccess.Client", ProviderTypes.Firebird => "FirebirdSql.Data.FirebirdClient",
                _ => throw new NotSupportedException()
            };
            using var provider = ProviderFactory.Create(providerType, connectionString, Value("--schema"), scope, providerName);
            if (values.ContainsKey("--timeout")) provider.CommandTimeout = Seconds("--timeout", "30");
            var runner = new Migrator(provider, false, new Logger(false), types);
            runner.Options.Tags.UnionWith(tags); runner.Options.TagMatch = tagMatch;
            runner.Options.Profiles.UnionWith(Value("--profiles", "").Split(',', StringSplitOptions.RemoveEmptyEntries));
            runner.Options.TransactionMode = EnumValue<MigrationTransactionMode>("--transaction", "PerMigration");
            if (values.ContainsKey("--lock")) runner.Options.Lock = new DatabaseMigrationLock();
            runner.Options.LockTimeout = TimeSpan.FromSeconds(Seconds("--lock-timeout", "30"));
            switch (command)
            {
                case "status": foreach (var applied in ((IMigrationHistory)provider).ReadAppliedMigrations()) output.WriteLine(applied + " applied"); break;
                case "validate": _ = runner.Plan(target); output.WriteLine("Migration plan is valid."); break;
                case "plan": foreach (var step in runner.Plan(target)) output.WriteLine(step.Version + (step.IsUp ? " up" : " down")); break;
                case "sql": Write(runner.PreviewSql(target, providerType, values.ContainsKey("--allow-legacy-preview"))); break;
                default: runner.MigrateTo(target); output.WriteLine("Migration completed."); break;
            }
            return 0;
            int Seconds(string key, string fallback) => int.TryParse(Value(key, fallback), out var seconds) && seconds >= 0 ? seconds : throw new ArgumentException(null, key);
            void Write(string sql) { if (Value("--output") is { } path) File.WriteAllText(path, sql); else output.WriteLine(sql); }
        }
        finally { AssemblyLoadContext.Default.Resolving -= Resolving; }
    }
}
