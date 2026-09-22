using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;

internal sealed class CliUsageException(string option) : Exception
{
    public string Option { get; } = option;
}

internal sealed class CliOptions
{
    public string Command { get; private init; }
    public string AssemblyPath { get; private init; }
    public ProviderTypes Provider { get; private init; }
    public string ConnectionEnvironment { get; private init; }
    public string Schema { get; private init; }
    public string Scope { get; private init; }
    public long? Target { get; private init; }
    public string[] Tags { get; private init; }
    public TagMatchMode TagMatch { get; private init; }
    public string[] Profiles { get; private init; }
    public bool ProfilesSpecified { get; private init; }
    public MigrationTransactionMode Transaction { get; private init; }
    public int? CommandTimeout { get; private init; }
    public int LockTimeout { get; private init; }
    public bool UseLock { get; private init; }
    public bool Offline { get; private init; }
    public bool AllowLegacyPreview { get; private init; }
    public string OutputPath { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        var command = args[0];
        if (command is not ("list" or "status" or "validate" or "migrate" or "rollback" or "plan" or "sql"))
            throw new CliUsageException("command");

        var values = ReadArguments(args);
        string Value(string key, string fallback = null) => values.GetValueOrDefault(key, fallback);
        var provider = ParseEnum<ProviderTypes>(Value("--provider", "none"), "--provider");
        if (provider == ProviderTypes.none) throw new CliUsageException("--provider");
        var assembly = Value("--assembly") ?? throw new CliUsageException("--assembly");
        long? target = null;
        if (Value("--target") is { } targetValue)
        {
            if (!long.TryParse(targetValue, out var parsed) || parsed < 0) throw new CliUsageException("--target");
            target = parsed;
        }
        if (command == "rollback" && target == null) throw new CliUsageException("--target");

        return new CliOptions
        {
            Command = command,
            AssemblyPath = Path.GetFullPath(assembly),
            Provider = provider,
            ConnectionEnvironment = Value("--connection-env", "MIGRATOR_CONNECTION"),
            Scope = Value("--scope", "default"),
            Schema = Value("--schema"),
            Target = target,
            Tags = Split(Value("--tags", "")),
            TagMatch = ParseEnum<TagMatchMode>(Value("--tag-match", "Any"), "--tag-match"),
            Profiles = Split(Value("--profiles", "")),
            ProfilesSpecified = values.ContainsKey("--profiles"),
            Transaction = ParseEnum<MigrationTransactionMode>(Value("--transaction", "PerMigration"), "--transaction"),
            CommandTimeout = Value("--timeout") is { } timeout ? ParseSeconds(timeout, "--timeout") : null,
            LockTimeout = ParseSeconds(Value("--lock-timeout", "30"), "--lock-timeout"),
            UseLock = values.ContainsKey("--lock"),
            Offline = values.ContainsKey("--offline"),
            AllowLegacyPreview = values.ContainsKey("--allow-legacy-preview"),
            OutputPath = Value("--output")
        };
    }

    private static Dictionary<string, string> ReadArguments(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var key = args[i];
            if (values.ContainsKey(key)) throw new CliUsageException(key);
            switch (key)
            {
                case "--lock" or "--offline" or "--allow-legacy-preview":
                    values.Add(key, "true");
                    break;
                case "--assembly" or "--provider" or "--connection-env" or "--scope" or "--schema" or "--target"
                    or "--tags" or "--tag-match" or "--profiles" or "--transaction" or "--timeout" or "--lock-timeout" or "--output":
                    if (i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        throw new CliUsageException(key);
                    values.Add(key, args[++i]);
                    break;
                default:
                    throw new CliUsageException(key);
            }
        }
        return values;
    }

    private static T ParseEnum<T>(string value, string option) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result) ? result : throw new CliUsageException(option);

    private static int ParseSeconds(string value, string option) =>
        int.TryParse(value, out var seconds) && seconds >= 0 ? seconds : throw new CliUsageException(option);

    private static string[] Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries);
}
