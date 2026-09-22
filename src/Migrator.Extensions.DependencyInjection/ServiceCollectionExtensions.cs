using System;
using System.Linq;
using System.Reflection;
using DotNetProjects.Migrator.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace DotNetProjects.Migrator.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMigrator(this IServiceCollection services,
        Func<IServiceProvider, ITransformationProvider> providerFactory, Assembly migrations, Action<RunnerOptions> configure = null)
    {
        services.AddOptions<RunnerOptions>();
        if (configure != null) services.Configure(configure);
        services.AddScoped(providerFactory);
        foreach (var type in MigrationLoader.GetMigrationTypes(migrations)) services.TryAddTransient(type);
        services.AddScoped(sp =>
        {
            var provider = sp.GetRequiredService<ITransformationProvider>();
            var loader = new MigrationLoader(provider, migrations, false);
            var options = sp.GetRequiredService<IOptionsSnapshot<RunnerOptions>>().Value;
            options.Activator ??= type => (IMigration)sp.GetRequiredService(type);
            return new Migrator(provider, new MigrationLogger(sp.GetService<ILoggerFactory>()?.CreateLogger("Migrator.NET") ?? NullLogger.Instance), loader) { Options = options };
        });
        return services;
    }
}
