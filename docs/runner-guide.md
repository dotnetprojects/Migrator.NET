# Runner and fluent API

For step-by-step chapters and paired Classic/Fluent examples, use the [migration manual](https://dotnetprojects.github.io/Migrator.NET/guide/). This page is the compact runner reference.

Use imperative or fluent migrations with scope and tag filtering, profiles, ordered maintenance, transaction modes, planning, SQL preview and deployment locks. This guide describes the current repository API; check package compatibility when using an older release.

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
            .WithColumn("Id").AsInt32().WithPrimaryKey("PK_Id", "Id")
            .WithColumn("Name").AsString(255).NotNullable();
    }
}
```

Use `DotNetProjects.Migrator`, `.Framework` and `.Framework.Fluent`. A table definition is completed before execution. Existing imperative `Migration.Up/Down` classes keep working. `FluentMigration` supports authored `BuildDown`; `AutoReversingMigration` reverses supported create/rename operations in reverse order. Destructive changes, data, SQL and callbacks need explicit reverse operations. Automatic reversal never restores deleted data.

The builder has `Create`, `Alter`, `Delete`, `Rename`, `Insert`, `Update`, `Execute` and `Administration`. Schema inspection is exposed through `FluentMigration.Schema`, and the provider through `Context`. History and transaction methods remain explicit context operations. Administrative operations, views, data copying and updates from another table have typed operations; their SQL preview is currently unsupported. See the [operation coverage inventory](fluent-operation-coverage.md) for the normal API mappings and test limits.

Select an existing table explicitly when authoring an object:

```csharp
migration.Create.Column("Email").OnTable("Users").AsString(320).Nullable();
migration.Alter.Column("Name").OnTable("Users").AsString(200).NotNullable();
migration.Rename.Column("Name").OnTable("Users").To("DisplayName");
migration.Create.Index("IX_Users_Email").OnTable("Users").WithColumns("Email");
migration.Delete.Index("IX_Users_Email").FromTable("Users");
migration.Delete.Column("Email").FromTable("Users");
```

Filtered indexes can use columns outside the index keys on SQL Server (2008+), PostgreSQL and SQLite. For example, enforce unique identifiers only for active users with a non-null identifier:

```csharp
// FilterItem and FilterType are in Providers.Models.Indexes and its Enums namespace.
migration.Create.Index("UX_ActiveUsers").OnTable("Users")
    .WithColumns("IpaUserIdentifier").Unique()
    .WithFilter(
        new FilterItem { ColumnName = "IpaUserIdentifier", Filter = FilterType.NotEqualTo, Value = null },
        new FilterItem { ColumnName = "Archive", Filter = FilterType.EqualTo, Value = 0 })
    .OnUnsupportedFilter(UnsupportedIndexFilterBehavior.Throw);
```

Classic migrations use the same `FilterItems` list on `Index`, with
`UnsupportedFilterBehavior = UnsupportedIndexFilterBehavior.Throw` (the default).
Choose `Ignore` to omit all filters when the provider cannot apply them. The resulting
index is unfiltered; a unique index then constrains all rows. Supported providers
still apply the filters in Ignore mode, and database errors are never swallowed.
Oracle retains its limited non-unique, key-column expression emulation; unique or
non-key filters use the unsupported behavior. Other providers without implemented
filter support, including the SQL Server 2005 dialect, also use that behavior.

Read the definition back with `Database.GetIndexes("Users")` or
`Schema.Table("Users").Indexes()` inside a fluent migration. SQL Server, PostgreSQL
and SQLite return supported filter predicates, including null checks, together with
the index name, key order, included columns and flags. Those definitions can be used
to recreate the index. Predicates outside the `FilterItems` model (such as `OR`) fail
explicitly. Oracle expression-index metadata remains outside this read-back support.
The unsupported-filter policy is not stored in the database and reads back as the
default. SQL preview still rejects filtered indexes, including Ignore mode.

Table definitions, column additions and column alterations share the same type
and option methods. Each named column must specify its type. `AsDateTime()` maps
to `DbType.DateTime`, while `AsDateTime2()` maps to `DbType.DateTime2`.
Complete update/delete expressions with `Where(...)` or `AllRows()`; updates
also support `WhereSql(...)`. An unfinished chain causes `Build`, `Apply` and
`Preview` to throw before any queued operation executes. Each insert expression
describes one row. Use `IfProvider(name, configure)` for provider conditions and
`Schema.Table(table).Select(...)` / `.SelectScalar(...)` for table reads.

## Scripts and provider-specific cleanup

`Execute.Script(path)` and `Execute.EmbeddedScript(assembly, resourceName)` capture script text as dedicated operations. Imperative callers can use `ExecuteScript(path)`, `ExecuteResourceScript(assembly, name)` and `ExecuteSqlScript(text)`. SQL Server splits standalone `GO` lines, including an optional `--` comment, while respecting strings, quoted identifiers and nested comments. GO repetition and SQLCMD directives fail explicitly before executing batches. Ordinary `ExecuteNonQuery` and fluent `Execute.Sql` never split client separators. Other providers receive the script as one command unless they implement `IScriptBatchProvider`; this is not a complete SQL*Plus, mysql-client or isql interpreter.

Oracle `RemoveTable` leaves unrelated sequences intact and relies on Oracle to remove table-owned triggers and native identity objects. For legacy sequences you explicitly own, use `OracleTransformationProvider.RemoveTableWithOwnedSequences(table, sequenceNames)` through an explicit provider context/callback. It accepts simple unquoted sequence names, validates existence before dropping the table, and propagates cleanup failures. Oracle DDL is not atomic. Column changes preserve explicit unique constraints and indexes. Use `AddUniqueConstraint`, `RemoveConstraint` or `RemoveIndex` to manage them independently. SQL Server no longer uses implicit ownership markers or exposes `AdoptColumnUniqueConstraint`; old markers do not cause constraints to be deleted.

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

## Consolidated history

A baseline migration can call `Database.MigrationApplied(version, scope)` for older versions whose schema it includes. Before each planned step, the runner rechecks the active scope's history. It skips versions already covered by the baseline, including their `AfterUp` callbacks, and does not record the baseline's own version twice. Downward runs similarly skip versions already removed by an earlier `Down`. Other scopes do not affect these decisions. History and schema changes follow the selected transaction mode.

## Transactions and locks

`PerMigration` commits each successful migration. `None` leaves transaction behavior to the provider/operations. `WholeSession` is accepted for SQLite, PostgreSQL and SQL Server dialects; history-table initialization occurs before that transaction. Other dialects fail explicitly because transactional DDL has not been verified. Arbitrary imperative SQL can still violate transaction assumptions; database administration and implicit-commit statements require separate runs.

`AfterUp`/`AfterDown` run after commit. In whole-session mode they are deferred until the complete session commits. Their failure reports an error after durable changes; it cannot undo a successful commit. Caller-owned connections remain caller-owned.

`new DatabaseMigrationLock()` uses SQL Server application locks, PostgreSQL advisory locks or MySQL/MariaDB named locks. Locks are session-owned, keyed by database/history table/scope, and remain held across migration commits. Do not switch databases, replace/close the connection or manipulate the native lock inside a migration. Unsupported providers, including SQLite, reject this lock implementation. Supply a custom `IMigrationLock` where another coordination mechanism is required. MySQL named locks coordinate one server, not an entire distributed cluster.

## Planning and SQL preview

`runner.Plan(target)` and `DryRun` inspect history without creating/upgrading it and do not invoke migration bodies, callbacks, transactions or SQLite PRAGMA changes. Custom providers must implement `IMigrationHistory` for these paths.

`runner.PreviewSql(target, providerType)` connects for history/schema reads. `MigrationSqlPreview.Generate(providerType, migrations)` can generate SQL offline. Earlier structured operations update a planned schema so later operations can refer to newly created/renamed tables. SQL preview currently supports a subset: basic tables/columns, supported renames, simple indexes, inserts and raw SQL. Unsupported alterations, constraints, filters, callbacks and schema dependencies fail explicitly. Output is operation SQL, not an idempotent history-managed deployment bundle.

Imperative bodies require `allowLegacyBodies: true`. Provider calls are captured through a rejecting proxy: direct connections, commands and unsupported reads/callbacks are blocked. **Arbitrary C# cannot be sandboxed**: constructors, fluent authoring and opted-in imperative bodies can still access files, networks or external state. Use trusted migration code. Migrations overriding `InitializeOnce` are rejected before their body runs, because skipping initialization could produce misleading SQL. Post-commit callbacks do not run during preview. Raw SQL invalidates planned schema knowledge, so later structured schema dependencies fail explicitly.

## CLI from source

```sh
dotnet pack src/Migrator.Tool -o artifacts/packages
dotnet tool install DotNetProjects.Migrator.Tool --add-source artifacts/packages --tool-path artifacts/tools
```

On Windows, use a short tool installation directory (or the default global-tool directory): the bundled SQLite native library failed to load from this review workspace's deeply nested tool path, while the same package passed from a short temporary path.

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

Use `--connection-env NAME`, `--schema`, `--tags a,b`, `--tag-match Any|All`, `--profiles a,b`, `--timeout SECONDS`, `--lock` and `--lock-timeout SECONDS` where applicable. `rollback` requires an explicit lower target and rejects any plan containing upward steps. Target validation runs after acquiring the configured lock and refreshing history. Offline SQL assumes empty history and currently rejects profiles/maintenance. `validate` validates version planning, not arbitrary migration-body behavior. The packaged drivers cover SQLite, SQL Server, PostgreSQL, MySQL/MariaDB, Oracle and Firebird. Other library providers need a custom host.

Exit codes: `0` success, `1` execution/load failure, `2` invalid arguments, `3` unsupported operation/provider, `4` lock timeout. SQL output may contain migration data; exception and provider trace details are omitted from CLI diagnostics.

## Optional DI and logging

The optional package `DotNetProjects.Migrator.Extensions.DependencyInjection` provides `services.AddMigrator(providerFactory, migrationAssembly, configureOptions)`. Resolve `Migrator` inside a service scope; migration constructors use that scope's services. Options are scoped snapshots. Provider disposal follows the DI scope. Microsoft logging records lifecycle events while omitting SQL text and raw exception messages; the core retains its lightweight logger API.

## Validation

Build before using the test scripts (they intentionally use `--no-build`):

```sh
dotnet build Migrator.slnx
pwsh .github/scripts/test.ps1 -Database Unit
pwsh .github/scripts/test.ps1 -Database SQLite
```

See [live database tests](live-database-tests.md) for the full matrix and [data-type boundary tests](data-type-boundary-tests.md) for supported mappings and precision, range and size limits. Provider-specific changes need live provider evidence.

An auxiliary-only `MigrateToLastVersion()` run preserves existing version history while executing selected profiles and maintenance. A completely empty run does not create a history table. Post-commit callbacks receive their migration context in both per-migration and whole-session modes; callback failure cannot undo a committed migration.

PostgreSQL column and constraint metadata resolves the requested relation through the database, including schema-qualified or explicitly quoted names and the connection search path. The lookup is parameterized and distinguishes same-named tables in different schemas. This does not imply complete schema qualification for every provider operation. Native `time without time zone` metadata and literal defaults map to `TimeOnly`.


### Time of day and intervals

`DbType.Time` / `MigratorDbType.Time` is a time of day. Use `TimeOnly` for defaults and values passed to `Insert`/`Update`. `MigratorDbType.Interval` is a duration; use `TimeSpan`, including negative and multi-day values. A `TimeSpan` default on a Time column is rejected instead of silently treating a duration as a clock time.

```csharp
new Column("job_time", DbType.Time, new TimeOnly(12, 34, 56));
new Column("elapsed", MigratorDbType.Interval, TimeSpan.FromDays(2));
```

PostgreSQL and Oracle use native intervals. SQL Server, SQLite, MySQL and MariaDB represent intervals as signed .NET ticks (100 ns units). Integer catalog metadata cannot distinguish an interval from an ordinary integer column, so retain the migration definition when that semantic distinction matters. Other dialects without an Interval mapping reject it.

Oracle's Time representation remains DATE with a fixed 1970-01-01 date and whole-second precision; fractional defaults/parameters are rejected. SQL Server 2005 uses DATETIME with its native precision. Informix Time now uses DATETIME HOUR TO SECOND (whole seconds), not INTERVAL. Existing Informix columns created with the old mapping require an explicit migration. SQLite stores clock times as invariant text. Raw ADO.NET scalar results retain driver-specific CLR types; a driver may return SQL TIME as TimeSpan or DateTime even though the public input is TimeOnly.

This changes the old shared parameter inference: TimeSpan now means Interval. Migrate time-of-day inputs with `TimeOnly.FromTimeSpan(value)`; it rejects negative or multi-day durations. Do not convert genuine intervals this way.

ASE 16.0 key constraints with dots or apostrophes in their names are rejected before DDL. The tested server can create a punctuated name but cannot reliably resolve its backing index when removing the constraint. Use a key name without those characters; this restriction applies to primary and unique keys.

## SQLite schema and value behavior

SQLite alterations use native rename/drop-column paths when eligible and live-schema reconstruction for supported changes that need a replacement table. Rebuilds retain column collations, named/composite keys, independent foreign-key update/delete actions, supported indexes/triggers and AUTOINCREMENT high-water state. They validate foreign-key integrity before committing owned transactions and restore the prior enforcement setting. Configure foreign-key settings before starting a caller-owned transaction.

`MATCH FULL` and `MATCH PARTIAL` are rejected because SQLite does not enforce their semantics. Generated columns, `STRICT`, `WITHOUT ROWID`, and indexes with explicit collations are unsupported for reconstruction. Hidden rowid values are not preserved. See the [operation and preservation matrices](migration-framework-comparison.md#sqlite-emulation-comparison).

`Collation.AsciiIgnoreCase` maps to SQLite's ASCII-only `NOCASE`; semantic Unicode case-insensitivity is not substituted with ASCII folding. Use `Collation.Named` for a registered custom collation. Changing a column collation rebuilds the table and rolls back on a uniqueness violation.

CLR `Guid` defaults and inserted GUID parameters both use blobs from `Guid.ToByteArray()`. Existing text GUID defaults remain SQL expressions during unrelated rebuilds. Converting existing mixed text/blob identifiers requires an explicit migration of related keys. See the [GUID and identity guidance](migration-guide-12.1-to-13.md#sqlite-defaults-and-identity).
