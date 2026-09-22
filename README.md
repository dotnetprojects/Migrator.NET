# DotNetProjects.Migrator

**Versioned database migrations in C#, independent of your ORM.**

[![NuGet version](https://img.shields.io/nuget/v/DotNetProjects.Migrator.svg)](https://www.nuget.org/packages/DotNetProjects.Migrator/)
[![NuGet downloads](https://img.shields.io/nuget/dt/DotNetProjects.Migrator.svg)](https://www.nuget.org/packages/DotNetProjects.Migrator/)
[![Build and tests](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/dotnetpull.yml/badge.svg?branch=master)](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/dotnetpull.yml)
[![GitHub Pages](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/pages.yml/badge.svg?branch=master)](https://github.com/dotnetprojects/Migrator.NET/actions/workflows/pages.yml)
[![Source target: .NET 9](https://img.shields.io/badge/source_target-.NET_9-512BD4)](src/Migrator/DotNetProjects.Migrator.csproj)
[![License: MPL-1.1](https://img.shields.io/badge/license-MPL--1.1-blue.svg)](https://www.mozilla.org/en-US/MPL/1.1/)

[Homepage & documentation](https://dotnetprojects.github.io/Migrator.NET/) · [NuGet](https://www.nuget.org/packages/DotNetProjects.Migrator/) · [Releases](https://github.com/dotnetprojects/Migrator.NET/releases) · [Issues](https://github.com/dotnetprojects/Migrator.NET/issues) · [Feature comparison](https://dotnetprojects.github.io/Migrator.NET/#compare)

DotNetProjects.Migrator is a fork of [Migrator.NET](https://github.com/migratordotnet/Migrator.NET). Write each schema change as a numbered C# class, commit it alongside your application, and use the runner to bring a database to the required version. The database records which migrations have already been applied.

## Contents

- [Why use it?](#why-use-it)
- [Installation and requirements](#installation-and-requirements)
- [Quick start](#quick-start)
- [Migration versions and rollback](#migration-versions-and-rollback)
- [Multiple modules and migration scopes](#multiple-modules-and-migration-scopes)
- [Schema and data operations](#schema-and-data-operations)
- [Database providers](#database-providers)
- [Comparison with other .NET frameworks](#comparison-with-other-net-frameworks)
- [Building and testing](#building-and-testing)
- [Documentation and GitHub Pages](#documentation-and-github-pages)
- [Contributing and project history](#contributing-and-project-history)
- [License](#license)

## Why use it?

- **Explicit C# migrations.** Define forward and reverse changes with `Up()` and `Down()`; review them like application code.
- **No ORM dependency.** Use it alongside EF, Dapper, another data layer, or plain ADO.NET.
- **Database transformation API.** Work with tables, columns, keys, indexes and data, with raw SQL available for provider-specific operations.
- **Version tracking.** Apply pending migrations or target a specific version using database-backed history.
- **Scoped histories.** Track multiple modules in one database when each runner is given the appropriate migration set.
- **Bring your database driver.** The library does not directly reference database-driver packages; supply an ADO.NET connection or configure the driver factory.
- **SQLite schema handling.** This fork includes schema inspection and table-recreation logic for operations SQLite cannot perform directly.

The source upgrade adds a structured fluent API, runner filtering/lifecycle options, SQL-preview subset, native locking, a CLI project and optional Microsoft DI/logging integration. These changes are under review and **are not a released NuGet feature claim**. See the [runner and fluent guide](docs/runner-guide.md) and [detailed framework comparison](docs/migration-framework-comparison.md). EF-style model scaffolding and migration-content checksums remain outside the implementation.

## Installation and requirements

```sh
dotnet add package DotNetProjects.Migrator
```

Install the ADO.NET driver for your database separately. For the SQLite example below:

```sh
dotnet add package Microsoft.Data.Sqlite --version 9.0.7
```

The **current source targets `net9.0`**. Check the [NuGet package's framework list](https://www.nuget.org/packages/DotNetProjects.Migrator/#supportedframeworks-body-tab) for the particular release you install; older package releases may target different frameworks. The SQLite driver version above matches the repository's test dependency.

Building the `.slnx` solution requires an SDK that understands that format, such as .NET SDK 9.0.200 or later. The runtime required by the current source is .NET 9.

## Quick start

This example targets **unreleased v13 source**. Clone/check out the upgrade branch before running these commands from the repository root. For published 12.1, follow its version-specific API; see the [migration guide](docs/migration-guide-12.1-to-13.md).

### 1. Create a migration host

```sh
dotnet new console -n MigrationDemo -f net9.0
cd MigrationDemo
dotnet add reference ../src/Migrator/DotNetProjects.Migrator.csproj
dotnet add package Microsoft.Data.Sqlite --version 9.0.7
```

### 2. Add `CreateUsers.cs`

Migrations must be public classes implementing the migration contract, decorated with `[Migration(version)]`. Each version must be unique within the set loaded by one runner.

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

### 3. Replace `Program.cs`

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

Use increasing numeric versions, or the attribute's date-based constructor:

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

Migration execution starts a transaction for each migration and attempts rollback on failure. Actual atomicity depends on the database, driver and operation; some databases implicitly commit DDL. `AfterUp()` and `AfterDown()` run **after commit**, so a failure in those hooks cannot undo the committed migration.

For deployment, run a dedicated migration host before the application needs the new schema. Coordinate it so competing instances do not migrate the same database concurrently. Review and test both directions against your actual database engine.

## Multiple modules and migration scopes

The default history table is `SchemaInfo`, with version, scope and timestamp information. The default scope is `"default"`. You can use separate scopes for modules sharing a database.

Within a host with an open `connection`, select the module's migration types explicitly:

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

- In the upgrade source, explicit scopes filter discovery; unscoped migrations inherit the runner scope. A scope partitions history, not database objects.
- Leave `MigrationAttribute.Scope` unset to inherit the provider scope; set it to select a migration for one specific scope.
- Duplicate versions are checked within the effective scope. Duplicate versions in distinct explicit scopes are independent.
- Scopes do not isolate tables or data. Module migrations still need compatible table names and coordinated schema ownership.

See [ProviderFactory](src/Migrator/ProviderFactory.cs), [MigrationLoader](src/Migrator/MigrationLoader.cs) and [history implementation](src/Migrator/Providers/TransformationProvider.cs).

## Fluent API and deployment tooling

Run the [compiled fluent example](examples/FluentQuickStart/Program.cs):

```sh
dotnet run --project examples/FluentQuickStart
```

The example creates a complete table definition, previews it without changing history, runs a whole-session migration, then verifies automatic reversal. The [runner guide](docs/runner-guide.md) covers CLI commands, tags/profiles, maintenance, transactions, optional DI/logging, locks and preview limitations. Build the source packages locally to try the new tooling; no NuGet publication accompanies these PRs.

## Schema and data operations

Inside a migration, `Database` implements [`ITransformationProvider`](src/Migrator/Framework/ITransformationProvider.cs). It includes:

| Area               | Examples                                                                              |
| ------------------ | ------------------------------------------------------------------------------------- |
| Tables and columns | `AddTable`, `RemoveTable`, `RenameTable`, `AddColumn`, `ChangeColumn`, `RemoveColumn` |
| Keys and indexes   | `AddPrimaryKey`, `AddForeignKey`, `AddIndex` and corresponding removal operations     |
| Schema inspection  | `TableExists`, `ColumnExists`, `GetTables`, `GetColumns`                              |
| Data and SQL       | `Insert`, `Update`, `Delete`, `ExecuteNonQuery`, `ExecuteQuery`, `ExecuteScalar`      |

For example, a new migration can add a column:

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

Provider implementations determine which operations are available and how they map to SQL. Use `Database.ExecuteNonQuery(...)` for custom SQL and keep dialect-specific statements explicit. The source also includes a [schema builder API](src/Migrator/Framework/SchemaBuilder/SchemaBuilder.cs).

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
| Sybase       | `Sybase`                     |

This is an inventory of dialects present in source, **not a guarantee that every server version, driver or operation is supported**. Some entries are legacy variants. Verify the combination you deploy against the [provider implementations](src/Migrator/Providers/Impl) and [provider tests](src/Migrator.Tests/Providers).

## Comparison with other .NET frameworks

Reviewed **22 September 2026**. Migrator's column describes this repository; the alternatives summarize their official documentation. These are workflow differences, not performance benchmarks or a ranking.

| Capability                   | Migrator.NET (this fork)          | FluentMigrator                               | EF Core                              | DbUp                       | Evolve                            |
| ---------------------------- | --------------------------------- | -------------------------------------------- | ------------------------------------ | -------------------------- | --------------------------------- |
| Authoring                    | Handwritten C# transformation API | Handwritten C# fluent DSL                    | C# scaffolded from model differences | SQL or C# scripts          | Versioned SQL files               |
| ORM-independent workflow     | Yes                               | Yes                                          | Uses EF model / DbContext            | Yes                        | Yes                               |
| Model-difference scaffolding | No built-in generator             | Hand-authored                                | Yes, with model snapshots            | Hand-authored              | Hand-authored                     |
| Downgrade applied migrations | Authored `Down()`                 | `Down()`; supported auto-reverse expressions | Generated/editable `Down()`          | Custom undo or forward fix | Forward fix; no Down command      |
| Separate histories           | Scope + selected assembly/types   | Custom version table + filtering             | Contexts + custom history table      | Journals + script filters  | Metadata table/schema + locations |
| Execution                    | Library / custom host             | Library + CLI                                | CLI, scripts, bundles, runtime       | Library / custom host      | Library, .NET tool, CLI           |
| Recurring work               | Custom code                       | Maintenance migrations / profiles            | Seeding APIs (EF 9+)                 | `RunAlways` scripts        | Checksum-based repeatable SQL     |

All five can execute raw SQL. Transaction support depends on database capabilities: Migrator starts one per migration; DbUp makes transactions opt-in; the others have configurable transaction behavior. Reversing a completed migration is different from rolling back a failed transaction. Evolve's checksum-based repeatables also differ from always-run scripts or lifecycle hooks.

- Choose **Migrator** for direct C# schema operations, scoped history and integration with your own host.
- Consider **FluentMigrator** for its fluent authoring API, packaged runners, tags and profiles.
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

The homepage in [`docs/`](docs/README.md) includes installation, a runnable quick start, provider information and a sourced feature comparison. It uses plain HTML, CSS and JavaScript with no build dependencies.

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

### Version 13 source changes

The unreleased v13 stack separates columns from named table constraints and removes the old column flags and duplicate fluent builder. See the [12.1-to-13 migration guide](docs/migration-guide-12.1-to-13.md) before recompiling migrations. These source features are not claims about the published 12.1 NuGet package.


### SQL expressions and collations in v13

```csharp
new Column("Id", DbType.String, 27) { DefaultValue = RawSql.Insert("ksuid_new()") };
builder.Create.Table("Events").WithColumn("Id").AsString(27)
    .WithDefaultValue(RawSql.Insert("ksuid_new()"));

new Column("Name", DbType.String, 100) { Collation = Collation.CaseInsensitive };
builder.Create.Table("Names").WithColumn("Name").AsString(100)
    .WithCollation(Collation.CaseInsensitive);
```

SQL expressions are trusted migration code and must exist on the target database.
Ordinary string defaults remain quoted literals. Semantic collations have explicit
provider limits; SQLite's `AsciiIgnoreCase` never substitutes for Unicode folding.
Use `Collation.Named("provider_name")` for a specific language or installed collation.
See the [mapping and migration guide](docs/migration-guide-12.1-to-13.md#explicit-sql-defaults-and-semantic-collations).

Additional engines are admitted only with passing real-database CI.
[SAP HANA qualification and deferred engine requirements](docs/additional-database-qualification.md)
cover the current FluentMigrator gaps without claiming untested support.
