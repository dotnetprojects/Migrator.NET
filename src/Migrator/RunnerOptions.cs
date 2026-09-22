using System;
using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;
namespace DotNetProjects.Migrator;

public enum TagMatchMode { Any, All }
public enum MigrationTransactionMode { PerMigration, None, WholeSession }
public enum MaintenanceStage { BeforeRun, BeforeMigration, AfterMigration, AfterRun }

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class TagsAttribute(params string[] tags) : Attribute
{ public IReadOnlyList<string> Tags { get; } = Array.AsReadOnly((string[])tags.Clone()); }
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ProfileAttribute(string name) : Attribute
{ public string Name { get; } = name; public int Order { get; set; } public string Scope { get; set; } }
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MaintenanceAttribute(MaintenanceStage stage) : Attribute
{ public MaintenanceStage Stage { get; } = stage; public int Order { get; set; } public string Scope { get; set; } }

public sealed class RunnerOptions
{
    public ISet<string> Tags { get; } = new HashSet<string>(StringComparer.Ordinal);
    public TagMatchMode TagMatch { get; set; } = TagMatchMode.Any;
    public ISet<string> Profiles { get; } = new HashSet<string>(StringComparer.Ordinal);
    public MigrationTransactionMode TransactionMode { get; set; } = MigrationTransactionMode.PerMigration;
    public Func<Type, IMigration> Activator { get; set; }
    public IMigrationLock Lock { get; set; }
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Acquire before any history read. The lease must release its lock in Dispose.</summary>
public interface IMigrationLock
{
    IDisposable Acquire(ITransformationProvider provider, string scope, TimeSpan timeout);
}

public sealed class UnsupportedMigrationFeatureException : NotSupportedException
{
    public UnsupportedMigrationFeatureException(string message, Exception inner = null) : base(message, inner) { }
}
public sealed class MigrationLockTimeoutException : TimeoutException
{
    public MigrationLockTimeoutException(Exception inner) : base("Timed out acquiring the migration lock.", inner) { }
}
