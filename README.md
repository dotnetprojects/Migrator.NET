# DotNetProjects.Migrator

**Versioned database migrations in C#, independent of your ORM.**

[![NuGet version](https://img.shields.io/nuget/v/DotNetProjects.Migrator.svg)](https://www.nuget.org/packages/DotNetProjects.Migrator/)
[![NuGet downloads](https://img.shields.io/nuget/dt/DotNetProjects.Migrator.svg)](https://www.nuget.org/packages/DotNetProjects.Migrator/)
[![Build and tests](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/dotnetpull.yml/badge.svg?branch=master)](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/dotnetpull.yml)
[![GitHub Pages](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/pages.yml/badge.svg?branch=master)](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/pages.yml)
[![Source target: .NET 9](https://img.shields.io/badge/source_target-.NET_9-512BD4)](src/Migrator/DotNetProjects.Migrator.csproj)
[![License: MPL-1.1](https://img.shields.io/badge/license-MPL--1.1-blue.svg)](https://www.mozilla.org/en-US/MPL/1.1/)

[Homepage](https://dotnetprojects.github.io/Migrator.NET/) · [Documentation](https://dotnetprojects.github.io/Migrator.NET/guide/) · [NuGet](https://www.nuget.org/packages/DotNetProjects.Migrator/) · [Releases](https://github.com/dotnetprojects/Migrator.NET/releases) · [Issues](https://github.com/dotnetprojects/Migrator.NET/issues) · [Feature comparison](https://dotnetprojects.github.io/Migrator.NET/#compare) · [CI test results & coverage](https://dotnetprojects.github.io/Migrator.NET/#test-results)

DotNetProjects.Migrator is a fork of [Migrator.NET](https://github.com/migratordotnet/Migrator.NET). Write each schema change as a numbered C# class, commit it alongside your application, and use the runner to bring a database to the required version. The database records which migrations have already been applied.

## Contents

- [Why use it?](#why-use-it)
- [Installation and requirements](#installation-and-requirements)
- [Quick start](#quick-start)
- [Migration versions and rollback](#migration-versions-and-rollback)
- [Multiple modules and migration scopes](#multiple-modules-and-migration-scopes)
- [Fluent API and deployment tooling](#fluent-api-and-deployment-tooling)
- [Schema and data operations](#schema-and-data-operations)
- [Database providers](#database-providers)
- [Comparison with other .NET frameworks](#comparison-with-other-net-frameworks)
- [Building and testing](#building-and-testing)
- [Documentation and GitHub Pages](#documentation-and-github-pages)
- [Contributing and project history](#contributing-and-project-history)
- [License](#license)

## Why use it?

- **Imperative or fluent C# migrations.** Use `Migration.Up/Down` or `FluentMigration.BuildUp/BuildDown`; review both like application code.
- **No ORM dependency.** Use it alongside EF, Dapper, another data layer, or plain ADO.NET.
- **Database transformation API.** Work with tables, columns, keys, indexes and data, with raw SQL available for provider-specific operations.
- **Version tracking.** Apply pending migrations or target a specific version using database-backed history.
- **Scoped histories.** Track multiple modules in one database when each runner is given the appropriate migration set.
- **Bring your database driver.** The library does not directly reference database-driver packages; supply an ADO.NET connection or configure the driver factory.
- **SQLite schema changes without an ORM model.** Automatically rebuild existing tables to change column types, defaults and nullability or add/remove primary, foreign, unique and check constraints. Migrator reads the live schema and copies existing rows for supported changes.

Runner options include tags, profiles, ordered maintenance, transaction modes, planning, a SQL-preview subset and native locking. Use the CLI or optional Microsoft DI/logging integration in your own host. See the [runner and fluent guide](docs/runner-guide.md) and [detailed framework comparison](docs/migration-framework-comparison.md). EF-style model scaffolding and migration-content checksums remain outside the implementation.

**SQLite is a particular strength:** FluentMigrator requires manual reconstruction for general column alterations and adding/removing foreign keys on existing tables; DbUp and Evolve leave reconstruction to your scripts. EF Core also rebuilds SQLite tables, using model metadata. Migrator supplies this automation from the live database without an ORM model. See the sourced [SQLite operation comparison and preservation limits](docs/migration-framework-comparison.md#sqlite-emulation-comparison).

## Installation and requirements

```sh
dotnet add package DotNetProjects.Migrator
```

Install the ADO.NET driver for your database separately. For the SQLite example below:

```sh
dotnet add package Microsoft.Data.Sqlite --version 9.0.7
```

The library targets **.NET 9**. The SQLite driver version above matches the repository's test dependency. See the [installation guide](https://dotnetprojects.github.io/Migrator.NET/guide/installation.html) for driver choices and optional packages.

Building the `.slnx` solution requires an SDK that understands that format, such as .NET SDK 9.0.200 or later, and the .NET 9 runtime.

## Quick start

Create a host with the .NET 9 SDK. Choose one of the two migration styles below; both use the same runner. The [interactive quick start](https://dotnetprojects.github.io/Migrator.NET/guide/quick-start.html) has Classic/Fluent tabs, and the [manual](https://dotnetprojects.github.io/Migrator.NET/guide/) covers each operation in both styles. When updating older migrations, see the [migration guide](docs/migration-guide-12.1-to-13.md).

### 1. Create a migration host

```sh
dotnet new console -n MigrationDemo -f net9.0
cd MigrationDemo
dotnet add package DotNetProjects.Migrator
dotnet add package Microsoft.Data.Sqlite --version 9.0.7
```

### 2. Add `CreateUsers.cs`

Migrations must be public classes implementing the migration contract, decorated with `[Migration(version)]`. Each version must be unique within the set loaded by one runner. Copy either the Classic or Fluent class, not both.

**Classic**

```csharp
using System.Data;
using DotNetProjects.Migrator.Framework;

[Migration(1)]
public class CreateUsers : Migration
{
    public override void Up()
    {
        Database.AddTable("Users",
            new Column("Id", DbType.Int32) { IsNullable = false },
            new Column("Name", DbType.String, 255),
            new PrimaryKeyConstraint("PK_Users", "Id"));
    }

    public override void Down()
    {
        Database.RemoveTable("Users");
    }
}
```

**Fluent — the equivalent `CreateUsers.cs`**

```csharp
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;

[Migration(1)]
public class CreateUsers : FluentMigration
{
    public override void BuildUp(MigrationBuilder migration)
    {
        migration.Create.Table("Users")
            .WithColumn("Id").AsInt32().NotNullable()
            .WithColumn("Name").AsString(255)
            .WithPrimaryKey("PK_Users", "Id");
    }

    public override void BuildDown(MigrationBuilder migration)
        => migration.Delete.Table("Users");
}
```

### 3. Replace `Program.cs` (shared host for both styles)

```csharp
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=app.db");
connection.Open();

using var provider = ProviderFactory.Create(
    ProviderTypes.SQLite, connection, defaultSchema: null);

var migrator = new Migrator(
    provider, typeof(CreateUsers).Assembly, trace: false);

if (migrator.LastAppliedMigrationVersion is long applied
    && applied > migrator.AssemblyLastMigrationVersion)
{
    throw new InvalidOperationException(
        "Database version is newer than this application.");
}

migrator.MigrateToLastVersion();
```

### 4. Run it

```sh
dotnet run
```

This creates a local SQLite database containing `Users` and the migration history table. Running the application again skips version `1` because it has already been recorded. Add a new class with `[Migration(2)]` for the next change.

The example supplies an **open** `IDbConnection`. The caller owns that connection and disposes it after the provider. If you use the connection-string overload instead, the selected provider must be able to resolve the appropriate ADO.NET factory.

## Migration versions and rollback

Both Classic and Fluent classes use increasing numeric versions, or the attribute's date-based constructor:

```csharp
[Migration(2026, 9, 22, 12, 0, 0)]
```

Keep applied migration classes in source control. Change the schema with a new migration instead of editing an already applied one: history records the version, not a checksum of the migration's content.

| API                            | Purpose                                                                          |
| ------------------------------ | -------------------------------------------------------------------------------- |
| `MigrateToLastVersion()`       | Apply through the latest version in the loaded migration set.                    |
| `MigrateTo(version)`           | Move to a chosen version, invoking `Up()` or `Down()` as required.               |
| `AppliedMigrations`            | List the versions recorded for the provider's scope.                             |
| `LastAppliedMigrationVersion`  | Highest applied version, or `null` when none are applied.                        |
| `AssemblyLastMigrationVersion` | Highest version in the loaded migration set.                                     |
| `SchemaInfoTableName`          | Customize the history table name before accessing history or running migrations. |

With the runner above, `migrator.MigrateTo(0)` reverses all applied migrations in its set. In this example that drops `Users`, including its data. A `Down()` implementation is a reverse schema operation, not a backup restore.

By default, migration execution starts a transaction for each migration and attempts rollback on failure. `None` and `WholeSession` transaction modes are also available; whole-session support is limited to SQLite, PostgreSQL and SQL Server. Actual atomicity depends on the database, driver and operation; some databases implicitly commit DDL. `AfterUp()` and `AfterDown()` run **after commit** (after the session commit in whole-session mode), so a failure in those hooks cannot undo the committed migration.

For deployment, run a dedicated migration host before the application needs the new schema. Coordinate it so competing instances do not migrate the same database concurrently. Review and test both directions against your actual database engine.

## Multiple modules and migration scopes

The default history table is `SchemaInfo`, with version, scope and timestamp information. The default scope is `"default"`. You can use separate scopes for modules sharing a database.

In the shared Classic/Fluent host with an open `connection`, select the module's migration types explicitly:

```csharp
using var billingProvider = ProviderFactory.Create(
    ProviderTypes.SQLite,
    connection,
    defaultSchema: null,
    scope: "billing");

var billingMigrator = new Migrator(
    billingProvider,
    false,
    typeof(Billing001),
    typeof(Billing002));

billingMigrator.MigrateToLastVersion();
```

`Billing001` and `Billing002` represent your own public migration classes. Alternatively, give the runner an assembly that contains only that module's migrations.

Important details:

- Explicit scopes filter discovery; unscoped migrations inherit the runner scope. A scope partitions history, not database objects.
- Leave `MigrationAttribute.Scope` unset to inherit the provider scope; set it to select a migration for one specific scope.
- Duplicate versions are checked within the effective scope. Duplicate versions in distinct explicit scopes are independent.
- Scopes do not isolate tables or data. Module migrations still need compatible table names and coordinated schema ownership.

Consolidated baseline migrations can record included versions with `Database.MigrationApplied(version, scope)`. The runner rechecks scope history before each step, skipping versions already covered by that baseline. See [consolidated history](docs/runner-guide.md#consolidated-history).

See [ProviderFactory](src/Migrator/ProviderFactory.cs), [MigrationLoader](src/Migrator/MigrationLoader.cs) and [history implementation](src/Migrator/Providers/TransformationProvider.cs).

## Fluent API and deployment tooling

`FluentMigration` collects operations in `BuildUp` and uses your explicit `BuildDown`. `AutoReversingMigration` derives reverse operations for supported create/rename changes; it cannot recover deleted data.

Run the [compiled fluent example](examples/FluentQuickStart/Program.cs):

```sh
dotnet run --project examples/FluentQuickStart
```

The example creates a complete table definition, previews it without changing history, runs a whole-session migration, then verifies automatic reversal. The [runner guide](docs/runner-guide.md) covers CLI commands, tags/profiles, maintenance, transactions, optional DI/logging, locks and preview limitations, including local tool installation.

## Schema and data operations

Inside a migration, `Database` implements [`ITransformationProvider`](src/Migrator/Framework/ITransformationProvider.cs). It includes:

| Area               | Examples                                                                              |
| ------------------ | ------------------------------------------------------------------------------------- |
| Tables and columns | `AddTable`, `RemoveTable`, `RenameTable`, `AddColumn`, `ChangeColumn`, `RemoveColumn` |
| Keys and indexes   | `AddPrimaryKey`, `AddForeignKey`, `AddIndex` and corresponding removal operations     |
| Schema inspection  | `TableExists`, `ColumnExists`, `GetTables`, `GetColumns`                              |
| Data and SQL       | `Insert`, `Update`, `Delete`, `ExecuteNonQuery`, `ExecuteQuery`, `ExecuteScalar`      |

For example, a new migration can add a column.

**Classic**

```csharp
public override void Up()
{
    Database.AddColumn("Users", new Column("Email", DbType.String, 320));
}

public override void Down()
{
    Database.RemoveColumn("Users", "Email");
}
```

**Fluent**

```csharp
public override void BuildUp(MigrationBuilder migration)
{
    migration.Create.Column("Email", "Users").AsString(320);
}

public override void BuildDown(MigrationBuilder migration)
{
    migration.Delete.Column("Email", "Users");
}
```

Provider implementations determine which operations are available and how they map to SQL. Use `Database.ExecuteNonQuery(...)` or `migration.Execute.Sql(...)` for custom SQL and keep dialect-specific statements explicit. The [Classic/Fluent API map](https://dotnetprojects.github.io/Migrator.NET/guide/api-map.html) lists the corresponding operations.

### Explicit constraints, SQL defaults and collations

Columns describe type, size, precision, nullability and identity. Define primary, unique, foreign-key and check constraints as named table objects; inspect them with `GetTableConstraints`. Changing a column preserves explicit constraints. `RawSql.Insert` marks a trusted SQL default expression, while `Collation` provides semantic presets and installed provider names.

**Classic — inside `Up()`**

```csharp
Database.AddTable("Events", new Column("Id", DbType.String, 27)
    { DefaultValue = RawSql.Insert("ksuid_new()") });
Database.AddTable("Names", new Column("Name", DbType.String, 100)
    { Collation = Collation.AsciiIgnoreCase });
```

**Fluent — inside `BuildUp(MigrationBuilder migration)`**

```csharp
migration.Create.Table("Events").WithColumn("Id").AsString(27)
    .WithDefaultValue(RawSql.Insert("ksuid_new()"));
migration.Create.Table("Names").WithColumn("Name").AsString(100)
    .WithCollation(Collation.AsciiIgnoreCase);
```

SQL expressions are trusted migration code and must exist on the target database.
Ordinary string defaults remain quoted literals. Semantic collations have explicit
provider limits; SQLite's `AsciiIgnoreCase` never substitutes for Unicode folding.
Use `Collation.Named("provider_name")` for a specific language or installed collation.
See the [mapping and migration guide](docs/migration-guide-12.1-to-13.md#explicit-sql-defaults-and-semantic-collations).

### SQLite reconstruction and data types

Supported rebuilds retain mapped data, named/composite keys, declared column collations, supported indexes and triggers, and the AUTOINCREMENT high-water mark. Generated columns, `STRICT`, `WITHOUT ROWID` and indexes with explicit collations are rejected for reconstruction; hidden rowid values are not preserved. Foreign keys retain separate update/delete actions; unsupported `MATCH FULL`/`PARTIAL` requests fail explicitly. See the [SQLite preservation matrix](docs/migration-framework-comparison.md#what-survives-reconstructionand-what-is-not-guaranteed).

CLR `Guid` defaults use the same blob representation as inserted GUID parameters. Existing text GUID defaults remain unchanged during unrelated rebuilds; converting mixed storage requires an explicit data migration. SQLite's `AsciiIgnoreCase` preset selects ASCII-only `NOCASE`; Unicode case-insensitive requests need a suitable custom collation, registered on the connection and selected by name.

Use `TimeOnly` for time-of-day values and `TimeSpan` for intervals. Storage and precision depend on the provider. The [runner guide](docs/runner-guide.md#time-of-day-and-intervals) and [data-type support and boundary tests](docs/data-type-boundary-tests.md) describe unsigned ranges, large text/binary, decimal precision and engine-specific limits.

## Database providers

The [provider factory](src/Migrator/ProviderFactory.cs) contains these database families:

| Database     | `ProviderTypes` value(s)     |
| ------------ | ---------------------------- |
| SQL Server   | `SqlServer`, `SqlServer2005` |
| PostgreSQL   | `PostgreSQL`, `PostgreSQL82` |
| SQLite       | `SQLite`, `MonoSQLite`       |
| MySQL        | `Mysql`                      |
| MariaDB      | `MariaDB`                    |
| Oracle       | `Oracle`, `MsOracle`         |
| IBM Db2      | `IBM_DB2`                    |
| IBM Informix | `IBM_Informix`               |
| Firebird     | `Firebird`                   |
| Ingres       | `Ingres`                     |
| SAP HANA | `Hana` |
| Sybase       | `Sybase`                     |

This is an inventory of dialects present in source, **not a guarantee that every server version, driver or operation is supported**. Some entries are legacy variants. Verify the combination you deploy against the [provider implementations](src/Migrator/Providers/Impl) and [provider tests](src/Migrator.Tests/Providers).

## Comparison with other .NET frameworks

Reviewed **23 September 2026**. Migrator's column describes this repository; the alternatives summarize their official documentation. These are workflow differences, not performance benchmarks or a ranking.

| Capability                   | Migrator.NET (this fork)          | FluentMigrator                               | EF Core                              | DbUp                       | Evolve                            |
| ---------------------------- | --------------------------------- | -------------------------------------------- | ------------------------------------ | -------------------------- | --------------------------------- |
| Authoring                    | Imperative C# + structured fluent API | Handwritten C# fluent DSL                    | C# scaffolded from model differences | SQL or C# scripts          | Versioned SQL files               |
| ORM-independent workflow     | Yes                               | Yes                                          | Uses EF model / DbContext            | Yes                        | Yes                               |
| Model-difference scaffolding | No built-in generator             | Hand-authored                                | Yes, with model snapshots            | Hand-authored              | Hand-authored                     |
| Downgrade applied migrations | Authored `Down()` / `BuildDown()`; supported automatic reversal | `Down()`; supported auto-reverse expressions | Generated/editable `Down()`          | Custom undo or forward fix | Forward fix; no Down command      |
| Separate histories           | Scope + selected assembly/types   | Custom version table + filtering             | Contexts + custom history table      | Journals + script filters  | Metadata table/schema + locations |
| Execution                    | Library / CLI | Library + CLI                                | CLI, scripts, bundles, runtime       | Library / custom host      | Library, .NET tool, CLI           |
| Recurring work               | Ordered maintenance / named profiles                       | Maintenance migrations / profiles            | Seeding APIs (EF 9+)                 | `RunAlways` scripts        | Checksum-based repeatable SQL     |
| Automatic SQLite reconstruction | Live-schema rebuilds; no ORM model | Manual for general column/FK alterations | Rebuilds for model-represented artifacts | Author scripts | Author scripts |

All five can execute raw SQL. Transaction support depends on database capabilities: Migrator defaults to per-migration transactions, with none or whole-session options (SQLite, PostgreSQL and SQL Server); DbUp makes transactions opt-in; the others have configurable transaction behavior. Reversing a completed migration is different from rolling back a failed transaction. Evolve's checksum-based repeatables also differ from always-run scripts or lifecycle hooks.

- Choose **Migrator** for imperative or fluent C# schema operations, scoped history, tags/profiles and the CLI or your own host.
- **FluentMigrator** also offers fluent C# authoring, tags and profiles. Compare provider operations, especially automatic SQLite reconstruction, and deployment requirements.
- Consider **EF Core migrations** when your EF model drives the schema and you want scaffolding and deployment artifacts.
- Consider **DbUp** for a SQL-oriented runner composed in .NET, or **Evolve** for convention-based SQL with checksum validation and repeatables.

Sources: [Migrator runner](src/Migrator/Migrator.cs), [FluentMigrator quick start](https://fluentmigrator.github.io/intro/quick-start.html) and [configuration](https://fluentmigrator.github.io/intro/configuration.html), [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) and [deployment](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [DbUp documentation](https://dbup.readthedocs.io/en/latest/) and [script types](https://dbup.readthedocs.io/en/latest/more-info/script-types/), [Evolve concepts](https://evolve-db.netlify.app/concepts/). The [full homepage comparison](https://dotnetprojects.github.io/Migrator.NET/#compare) includes transaction, provider and source details; its source is available in [docs/index.html](docs/index.html).

## Building and testing

```sh
dotnet restore Migrator.slnx
dotnet build Migrator.slnx --configuration Release --no-restore
```

Tests use NUnit. Run a focused runner test fixture without provisioning external databases:

```sh
dotnet test src/Migrator.Tests/Migrator.Tests.csproj --configuration Release --filter "FullyQualifiedName~Migrator.Tests.MigratorTest"
```

The full suite includes database integration tests:

```sh
dotnet test src/Migrator.Tests/Migrator.Tests.csproj --configuration Release
```

Use disposable test databases: integration tests create, alter and remove schema objects. Configure connections in `src/Migrator.Tests/appsettings.Development.json` using the structure and identifiers in [appsettings.json](src/Migrator.Tests/appsettings.json), and set `ASPNETCORE_ENVIRONMENT=Development`. The development settings file is gitignored; keep credentials there rather than committing them.

The [.NET workflow](.github/workflows/dotnetpull.yml) documents CI database services and commands. Provider coverage varies; a passing build alone does not validate every supported database family.

### Live database testing

See [live database testing](docs/live-database-tests.md) for the CI matrix, pinned versions, local commands, coverage, engine limitations and excluded candidates.

## Documentation and GitHub Pages

The [migration manual](https://dotnetprojects.github.io/Migrator.NET/guide/) includes 38 chapters with paired Classic/Fluent examples, chapter search, provider details and deployment guidance. Start with [tables](https://dotnetprojects.github.io/Migrator.NET/guide/creating-tables.html), [runner configuration](https://dotnetprojects.github.io/Migrator.NET/guide/configuration.html), or [SQLite](https://dotnetprojects.github.io/Migrator.NET/guide/sqlite.html). The homepage includes a sourced feature comparison.

The site uses static HTML, CSS and JavaScript. A Python standard-library generator builds it from `docs/_src/`; generated pages are committed. See the [site maintenance guide](docs/README.md) for regeneration and sample validation.

Preview locally from the repository root:

```sh
python -m http.server 8766 --directory docs --bind 127.0.0.1
```

Open [localhost:8766](http://localhost:8766). To publish, select **GitHub Actions** under **Settings → Pages → Build and deployment**, then merge the site into `master`. The [Pages workflow](.github/workflows/pages.yml) deploys changes to `docs/` at [dotnetprojects.github.io/Migrator.NET](https://dotnetprojects.github.io/Migrator.NET/). The workflow can also be dispatched manually on `master`.

## Contributing and project history

Bug reports, provider fixes, tests and documentation improvements are welcome through [issues](https://github.com/dotnetprojects/Migrator.NET/issues) and [pull requests](https://github.com/dotnetprojects/Migrator.NET/pulls). Include the package version, database/driver versions, a minimal migration that reproduces the problem, and expected versus actual behavior. Add a focused regression test for a behavior change and run the relevant provider tests.

This project continues the original [Migrator.NET](https://github.com/migratordotnet/Migrator.NET), which began on Google Code. This fork incorporates contributions from other forks and work on SQLite schema reading and recreation, composite primary keys, SQL Server index inspection, reserved identifiers, provider independence and migration scopes.

## License

The package declares **Mozilla Public License 1.1 (MPL-1.1)** in its [project metadata](src/Migrator/DotNetProjects.Migrator.csproj). See the [license text](https://www.mozilla.org/en-US/MPL/1.1/) and source-file notices.
