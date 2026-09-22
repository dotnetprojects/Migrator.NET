using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
namespace DotNetProjects.Migrator.Extensions.DependencyInjection;

/// <summary>Logs lifecycle events. SQL and exception messages may contain secrets and are omitted.</summary>
public sealed class MigrationLogger(Microsoft.Extensions.Logging.ILogger logger) : Framework.ILogger
{
    public void Started(List<long> currentVersion, long finalVersion) => logger.LogInformation("Migration run started; target {Version}", finalVersion);
    public void Finished(List<long> currentVersion, long finalVersion) => logger.LogInformation("Migration run completed; target {Version}", finalVersion);
    public void MigrateUp(long version, string migrationName) => logger.LogInformation("Applying migration {Version} ({Name})", version, migrationName);
    public void MigrateDown(long version, string migrationName) => logger.LogInformation("Reverting migration {Version} ({Name})", version, migrationName);
    public void Skipping(long version) => logger.LogWarning("Skipping migration {Version}", version);
    public void RollingBack(long originalVersion) => logger.LogWarning("Rolling back migration {Version}", originalVersion);
    public void ApplyingDBChange(string sql) => logger.LogDebug("Executing a database change");
    public void Exception(long version, string migrationName, Exception ex) => logger.LogError("Migration {Version} failed: {ExceptionType}", version, ex.GetType().Name);
    public void Exception(string message, Exception ex) => logger.LogError("Migration operation failed: {ExceptionType}", ex.GetType().Name);
    public void Log(string format, params object[] args) => logger.LogInformation("{Message}", string.Format(CultureInfo.InvariantCulture, format, args));
    public void Warn(string format, params object[] args) => logger.LogWarning("{Message}", string.Format(CultureInfo.InvariantCulture, format, args));
    public void Trace(string format, params object[] args) { } // Provider traces commonly contain SQL values.
}
