# Runner and fluent API upgrade

These APIs describe the source upgrade under review in PRs #173, #174, #175 and #177. They are not a statement about the currently released NuGet packages. Build the repository to try them; no package publication is part of this change.

## Fluent quick start

The [compiled quick-start project](../examples/FluentQuickStart/Program.cs) executes preview, migration and automatic reversal against SQLite:

```sh
dotnet run --project examples/FluentQuickStart
```

```csharp
[Migration(1, Scope = "demo"), Tags("core")]
public class CreateUsers : AutoReversingMigration
{
    public override void BuildUp(MigrationBuilder migration)
    {
        migration.Create.Table("Users")
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(255).NotNullable();
    }
}
```

Use `DotNetProjects.Migrator`, `.Framework` and `.Framework.Fluent`. A table definition is completed before execution. Existing imperative `Migration.Up/Down` classes keep working. `FluentMigration` supports authored `BuildDown`; `AutoReversingMigration` reverses supported create/rename operations in reverse order. Destructive changes, data, SQL and callbacks need explicit reverse operations. Automatic reversal never restores deleted data.

The builder has `Create`, `Alter`, `Delete`, `Rename`, `Insert`, `Update`, `Execute` and `Administration`. Schema inspection is exposed through `FluentMigration.Schema`, and the provider through `Context`. History and transaction methods remain explicit context operations. Some administrative/data-copy operations use provider callbacks and cannot generate SQL previews.

## Runner options

`runner.Options` supports:

| Option | Semantics |
| --- | --- |
| `Tags` / `TagMatch` | Ordinal names; explicit `Any` or `All`. No filter selects all versioned migrations. Filtered applied versions remain applied on downgrade. |
| `Profiles` | Explicit names of `[Profile("name")]` classes. Run after versioned migrations without recording versions; run again when selected again. |
| `TransactionMode` | `PerMigration` by default; `None` or `WholeSession` available. |
| `Activator` | Optional constructor activation delegate. |
| `Lock` / `LockTimeout` | Optional `IMigrationLock` lease; acquire before reading history and release on completion/failure. |

Unscoped migrations inherit the provider scope; explicitly scoped migrations run only in that scope. Discovery, duplicate validation and history reads use the effective scope. Scopes separate history, not tables. Legacy custom providers can adopt the additive `IMigrationHistory` interface for read-only planning and effective-scope selection.

Maintenance classes use `[Maintenance(MaintenanceStage.BeforeRun)]`, `BeforeMigration`, `AfterMigration` or `AfterRun`. Profiles and maintenance accept `Order` and `Scope`. Ordering uses `Order` then ordinal full type name. Hooks stop on failure; later hooks are not cleanup guarantees. Connection/transaction restoration and lock release do not depend on hooks running. Profiles and maintenance use `Up`; they do not acquire version records.

## Transactions and locks

`PerMigration` commits each successful migration. `None` leaves transaction behavior to the provider/operations. `WholeSession` is accepted for SQLite, PostgreSQL and SQL Server dialects; history-table initialization occurs before that transaction. Other dialects fail explicitly because transactional DDL has not been verified. Arbitrary imperative SQL can still violate transaction assumptions; database administration and implicit-commit statements require separate runs.

`AfterUp`/`AfterDown` run after commit. In whole-session mode they are deferred until the complete session commits. Their failure reports an error after durable changes; it cannot undo a successful commit. Caller-owned connections remain caller-owned.

`new DatabaseMigrationLock()` uses SQL Server application locks, PostgreSQL advisory locks or MySQL/MariaDB named locks. Locks are session-owned, keyed by database/history table/scope, and remain held across migration commits. Do not switch databases, replace/close the connection or manipulate the native lock inside a migration. Unsupported providers, including SQLite, reject this lock implementation. Supply a custom `IMigrationLock` where another coordination mechanism is required. MySQL named locks coordinate one server, not an entire distributed cluster.

## Planning and SQL preview

`runner.Plan(target)` and `DryRun` inspect history without creating/upgrading it and do not invoke migration bodies, callbacks, transactions or SQLite PRAGMA changes. Custom providers must implement `IMigrationHistory` for these paths.

`runner.PreviewSql(target, providerType)` connects for history/schema reads. `MigrationSqlPreview.Generate(providerType, migrations)` can generate SQL offline. Earlier structured operations update a planned schema so later operations can refer to newly created/renamed tables. SQL preview currently supports a subset: basic tables/columns, supported renames, simple indexes, inserts and raw SQL. Unsupported alterations, constraints, filters, callbacks and schema dependencies fail explicitly. Output is operation SQL, not an idempotent history-managed deployment bundle.

Imperative bodies require `allowLegacyBodies: true`. Provider calls are captured through a rejecting proxy: direct connections, commands and unsupported reads/callbacks are blocked. **Arbitrary C# cannot be sandboxed**: constructors, fluent authoring and opted-in imperative bodies can still access files, networks or external state. Use trusted migration code. `InitializeOnce` and post-commit callbacks do not run during preview.

## CLI from source

```sh
dotnet pack src/Migrator.Tool -o artifacts/packages
dotnet tool install DotNetProjects.Migrator.Tool --add-source artifacts/packages --tool-path artifacts/tools
```

Set `MIGRATOR_CONNECTION` in your environment; the tool does not print its value. Common commands:

```sh
migrator list --assembly MyMigrations.dll --provider SQLite
migrator status --assembly MyMigrations.dll --provider SQLite
migrator validate --assembly MyMigrations.dll --provider SQLite
migrator plan --assembly MyMigrations.dll --provider SQLite --target 10
migrator sql --assembly MyMigrations.dll --provider SQLite --output migration.sql
migrator sql --assembly MyMigrations.dll --provider SQLite --offline --output migration.sql
migrator migrate --assembly MyMigrations.dll --provider SQLite --scope billing --transaction WholeSession
migrator rollback --assembly MyMigrations.dll --provider SQLite --target 0
```

Use `--connection-env NAME`, `--schema`, `--tags a,b`, `--tag-match Any|All`, `--profiles a,b`, `--timeout SECONDS`, `--lock` and `--lock-timeout SECONDS` where applicable. `rollback` requires an explicit target. Offline SQL assumes empty history and currently rejects profiles/maintenance. `validate` validates version planning, not arbitrary migration-body behavior. The packaged drivers cover SQLite, SQL Server, PostgreSQL, MySQL/MariaDB, Oracle and Firebird. Other library providers need a custom host.

Exit codes: `0` success, `1` execution/load failure, `2` invalid arguments, `3` unsupported operation/provider, `4` lock timeout. SQL output may contain migration data; exception and provider trace details are omitted from CLI diagnostics.

## Optional DI and logging

The source package `DotNetProjects.Migrator.Extensions.DependencyInjection` provides `services.AddMigrator(providerFactory, migrationAssembly, configureOptions)`. Resolve `Migrator` inside a service scope; migration constructors use that scope's services. Options are scoped snapshots. Provider disposal follows the DI scope. Microsoft logging records lifecycle events while omitting SQL text and raw exception messages; the core retains its lightweight logger API.

## Validation

Build before using the test scripts (they intentionally use `--no-build`):

```sh
dotnet build Migrator.slnx
pwsh .github/scripts/test.ps1 -Database Unit
pwsh .github/scripts/test.ps1 -Database SQLite
```

See [live database tests](live-database-tests.md) for the full matrix. Provider-specific changes need live provider evidence. Check PR CI and review threads after every push; reply with implementation/test evidence and resolve fixed findings. Keep commits descriptive and merge the PR stack in dependency order only after review.
