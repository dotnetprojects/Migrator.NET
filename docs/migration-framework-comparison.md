# .NET database migration frameworks: detailed feature comparison

**Reviewed: 22 September 2026.** This is a capability comparison, not a benchmark or an overall ranking.

The main matrices cover **DotNetProjects.Migrator, FluentMigrator, EF Core migrations, DbUp and Evolve**—all five frameworks on the homepage. Additional sections cover **EF6, grate and RoundhousE**, with a short boundary comparison for **Flyway and Liquibase**. This is a defined shortlist, not a claim to catalogue every migration package ever published.

Migrator findings are pinned to repository commit [`ab3aa9f`][m-revision], before the parallel refactoring. FluentMigrator's SQLite implementation is pinned to [`2e0acdb`][f-sqlite-generator]. Other findings describe the linked official documentation as reviewed, not guaranteed behavior of every historical release. EF Core features introduced in version 9 are labeled. Check provider and release compatibility separately.

[Homepage](https://dotnetprojects.github.io/Migrator.NET/) · [Project README](../README.md) · [SQLite emulation comparison](#sqlite-emulation-comparison) · [Source index](#source-index)

## Contents

- [How to read the matrices](#how-to-read-the-matrices)
- [Authoring and application integration](#authoring-and-application-integration)
- [Schema and data operations](#schema-and-data-operations)
- [History, ordering and repeatability](#history-ordering-and-repeatability)
- [Transactions, rollback and coordination](#transactions-rollback-and-coordination)
- [Deployment, inspection and configuration](#deployment-inspection-and-configuration)
- [Database coverage and portability](#database-coverage-and-portability)
- [SQLite emulation comparison](#sqlite-emulation-comparison)
- [EF6, grate and RoundhousE](#ef6-grate-and-roundhouse)
- [Flyway and Liquibase in a .NET deployment](#flyway-and-liquibase-in-a-net-deployment)
- [Choosing a framework and identifying Migrator gaps](#choosing-a-framework-and-identifying-migrator-gaps)
- [Validation and maintenance](#validation-and-maintenance)
- [Source index](#source-index)

## How to read the matrices

| Term                 | Meaning                                                                                                                                                |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Built-in / named API | The reviewed tool provides this operation or workflow. Database restrictions still apply.                                                              |
| Configure            | Available through documented runner settings, composition or extension points.                                                                         |
| Custom               | You supply application code, SQL or deployment orchestration. Not automatic framework behavior.                                                        |
| No built-in          | No implementation in the inspected Migrator source, or no equivalent in the reviewed documented workflow. It does not rule out third-party extensions. |
| Provider-dependent   | Availability or semantics depend on the database integration and release.                                                                              |
| Not verified         | Evidence is insufficient for a positive or negative compatibility claim.                                                                               |

A SQL runner can execute a hand-authored table rebuild; that does **not** mean it automatically emulates `AlterColumn`. Likewise, recording applied migrations is not schema-drift detection, a transaction is not a deployment mutex, and a version downgrade is not a data restore.

## Authoring and application integration

Evidence: [Migrator runner][m-runner], [loader][m-loader], [migration contract][m-migration]; [FluentMigrator quick start][f-start] and [SQL execution][f-sql]; [EF Core overview][ef-overview] and [managing migrations][ef-managing]; [DbUp usage][d-usage] and [script providers][d-providers]; [Evolve concepts][e-concepts] and [configuration][e-options].

| Capability                               | Migrator                                                         | FluentMigrator                             | EF Core                                               | DbUp                                  | Evolve                                     |
| ---------------------------------------- | ---------------------------------------------------------------- | ------------------------------------------ | ----------------------------------------------------- | ------------------------------------- | ------------------------------------------ |
| Primary authoring artifact               | Public C# migration class                                        | C# migration class with fluent expressions | Generated, editable C# migration + model snapshot     | SQL file or C# `IScript`              | Versioned SQL file                         |
| Requires an ORM model                    | No                                                               | No                                         | Yes, for normal scaffolding                           | No                                    | No                                         |
| Generates changes from model differences | No built-in                                                      | No built-in model differ in core workflow  | Yes                                                   | No; author scripts                    | No; author scripts                         |
| Migration without a model change         | Yes                                                              | Yes                                        | Empty migration, then custom operations               | Yes                                   | Yes                                        |
| Schema DSL / transformation API          | `Database` operations; optional `SchemaBuilder`                  | Fluent create/alter/delete expressions     | `MigrationBuilder` operations                         | No schema DSL; SQL / commands         | No schema DSL; SQL                         |
| Custom C# logic                          | `Up` / `Down`; open provider                                     | Migration code / connection operations     | SQL/custom operations for database work               | `IScript` and command factory         | Surrounding host logic; migrations are SQL |
| Raw SQL                                  | Command, query and scalar APIs                                   | Inline, file and embedded SQL              | `migrationBuilder.Sql`                                | Primary workflow                      | Primary workflow                           |
| Migration discovery                      | Assembly scan or explicit `Type[]`                               | Assembly scanning / filters                | Context's migration assembly                          | Configurable script providers         | Locations or embedded resources            |
| Constructor dependency injection         | Default loader uses `Activator.CreateInstance`; customize loader | Runner/DI integration                      | Context services; migration customization is separate | Custom script provider/host if needed | No C# migration constructors               |
| Embedded execution                       | Yes                                                              | Yes                                        | Yes                                                   | Yes                                   | Library mode                               |
| Dedicated execution host                 | Write your own                                                   | Library or packaged runner                 | Tooling, bundles or custom host                       | Write your own                        | CLI, .NET tool or library                  |

EF Core's model snapshot comparison is not a live-database schema comparison. DbUp's C# support is more than static SQL file loading, but it does not supply a cross-database schema-operation layer.

## Schema and data operations

This table separates having an authoring API from that API working identically on every engine. SQLite is broken out below. Evidence: [Migrator interface][m-api] and [provider factory][m-factory]; [FluentMigrator operations][f-start]; [EF Core migration operations][ef-managing]; [DbUp script execution][d-usage]; [Evolve SQL model][e-concepts].

| Operation family                        | Migrator                                 | FluentMigrator                      | EF Core                                    | DbUp                          | Evolve                                |
| --------------------------------------- | ---------------------------------------- | ----------------------------------- | ------------------------------------------ | ----------------------------- | ------------------------------------- |
| Create / drop table                     | Schema API                               | Fluent API                          | Migration operations                       | Author SQL                    | Author SQL                            |
| Rename table                            | Schema API                               | Fluent API                          | Migration operation                        | Author SQL                    | Author SQL                            |
| Add / drop / rename column              | Schema API                               | Fluent API                          | Migration operations                       | Author SQL                    | Author SQL                            |
| Change type / nullability / default     | `ChangeColumn` and default API           | Alter expressions                   | `AlterColumn`                              | Author SQL                    | Author SQL                            |
| Primary / composite keys                | API; provider-dependent                  | Fluent expressions                  | Migration operations                       | Author SQL                    | Author SQL                            |
| Foreign keys / delete behavior          | API; mapped constraint types             | Fluent expressions                  | Migration operations                       | Author SQL                    | Author SQL                            |
| Unique constraints                      | API                                      | Fluent expressions                  | Migration operations                       | Author SQL                    | Author SQL                            |
| Check constraints                       | API using SQL predicate                  | Provider/custom SQL as applicable   | Migration operations                       | Author SQL                    | Author SQL                            |
| Indexes                                 | API and index model                      | Fluent expressions                  | Operations / provider annotations          | Author SQL                    | Author SQL                            |
| Filtered / included / clustered indexes | Provider-specific subsets                | Provider-specific options           | Provider-specific support                  | Engine-specific SQL           | Engine-specific SQL                   |
| Views                                   | `AddView` and SQL                        | Usually SQL                         | Usually SQL migrations                     | Author SQL                    | SQL; repeatables useful               |
| Stored procedures / triggers            | Raw SQL                                  | SQL / connection operations         | SQL / custom operations                    | Author SQL                    | Author SQL                            |
| Fixed-data insert / update / delete     | Data API                                 | Fluent data expressions             | `InsertData` / `UpdateData` / `DeleteData` | SQL or C#                     | SQL                                   |
| Transform existing data                 | SQL, provider reads/writes, copy helpers | SQL / connection operations         | SQL / custom operations                    | SQL or C#                     | SQL                                   |
| Live table / column existence           | Existence and metadata APIs              | Schema query API                    | SQL/custom code                            | SQL or C#                     | SQL                                   |
| Full schema-drift report                | No built-in                              | Not established by version tracking | Snapshot comparison alone is insufficient  | Journal alone is insufficient | Checksums concern scripts, not schema |

## History, ordering and repeatability

Evidence: [Migrator loader][m-loader], [execution][m-execution] and [history storage][m-provider]; [FluentMigrator configuration][f-config], [maintenance][f-maintenance] and [profiles][f-profiles]; [EF Core overview][ef-overview], [history][ef-history] and [seeding][ef-seeding]; [DbUp journaling][d-journal], [script types][d-types] and [usage][d-usage]; [Evolve concepts][e-concepts] and [options][e-options].

| Capability                 | Migrator                                                | FluentMigrator                         | EF Core                                        | DbUp                                             | Evolve                          |
| -------------------------- | ------------------------------------------------------- | -------------------------------------- | ---------------------------------------------- | ------------------------------------------------ | ------------------------------- |
| Applied-change identity    | Numeric version within scope                            | Migration version                      | Migration ID                                   | Script name                                      | Script metadata                 |
| Default history            | `SchemaInfo`                                            | Version table                          | `__EFMigrationsHistory`                        | E.g. `SchemaVersions`                            | `changelog`                     |
| History customization      | Table name; scope column                                | Version-table metadata                 | Table/schema; custom services                  | Custom journal / table                           | Metadata table/schema           |
| Independent modules        | Scope + selected migrations                             | Separate history + filters             | Contexts/assemblies + separate history         | Filters + separate journals                      | Locations + separate metadata   |
| Environment selection      | Host selection; ignore attribute for assembly discovery | Tags / profiles / configuration        | Context/deployment configuration               | Filters / host                                   | Locations / placeholders / host |
| Skip applied work          | Version history                                         | Version history                        | Migration history                              | Journal                                          | Metadata                        |
| Applied-source checksum    | No built-in                                             | Not a core version-table guarantee     | No script checksum journal                     | Standard journal tracks names; custom validation | Script checksums                |
| Late lower-numbered change | Revisits missing versions up to target                  | Check runner policy                    | Do not assume IDs make diverging branches safe | Unrecorded scripts eligible; ordering matters    | `OutOfOrder`                    |
| Repeat on content change   | Custom                                                  | Not equivalent to maintenance/profiles | Not equivalent to seeding                      | Custom checksum-aware runner                     | Repeatable SQL                  |
| Always-run work            | Host code; hooks are per executed migration             | Maintenance / selected profiles        | Seeding APIs, EF 9+                            | `RunAlways` / `NullJournal`                      | Not identical to RunAlways      |
| Existing-schema baseline   | Custom verified history initialization                  | Custom baseline/runner strategy        | Existing-schema workflow                       | `MarkAsExecuted`                                 | `StartVersion` / skip options   |
| Repair checksums           | Not applicable                                          | Not established by version history     | Not applicable                                 | Custom journal concern                           | `repair`                        |

**Migrator scope detail:** `MigrationAttribute.Scope` changes where a history record is written; it does not filter assembly discovery. A runner reads its provider scope and checks duplicate versions across its entire loaded set. Use separate assemblies or explicit types, normally leaving the attribute scope unset. History isolation is not table isolation. [Loader][m-loader], [execution][m-execution], [provider][m-provider].

## Transactions, rollback and coordination

Evidence: [Migrator execution][m-execution] and [runner][m-runner]; [FluentMigrator configuration][f-config] and [auto-reverse][f-reverse]; [EF Core management][ef-managing], [deployment][ef-applying] and [SQLite limitations][ef-sqlite]; [DbUp transactions][d-transactions] and [philosophy][d-philosophy]; [Evolve concepts][e-concepts] and [options][e-options].

| Capability                     | Migrator                         | FluentMigrator                                  | EF Core                                                               | DbUp                                         | Evolve                               |
| ------------------------------ | -------------------------------- | ----------------------------------------------- | --------------------------------------------------------------------- | -------------------------------------------- | ------------------------------------ |
| Default transaction unit       | Per migration                    | Per migration; configurable                     | Version-sensitive: EF 9 grouped pending migrations, reverted in EF 10 | None                                         | Per migration                        |
| Whole-run transaction          | Not a runner option              | Configure/orchestrate; check runner             | Depends on version/operations                                         | `WithTransaction()`                          | `CommitAll`                          |
| Per-change transaction opt-out | No migration attribute           | Transaction behavior                            | Raw SQL suppression                                                   | Choose strategy / separate runs              | Script opt-out                       |
| Failed DDL rollback            | Engine-dependent                 | Engine-dependent                                | Engine-dependent                                                      | When enabled and supported                   | Engine-dependent                     |
| Reverse committed migration    | Authored `Down()`                | `Down()`                                        | Generated/editable `Down()`                                           | Custom undo / forward fix                    | Forward fix; no Down command         |
| Generate reverse operations    | No                               | Supported auto-reverse expressions              | Scaffolding; review output                                            | No schema reverse generator                  | No                                   |
| Target earlier version         | `MigrateTo`                      | Down/rollback APIs                              | Earlier target / reverse script                                       | Custom                                       | Target limits forward work, not undo |
| Restore deleted data           | Backup / reconstruction          | Same                                            | Same                                                                  | Same                                         | Same                                 |
| Cross-process coordination     | No built-in migration lock found | Serialize deployment / application-lock pattern | Migration locking, EF 9+; execution-path dependent                    | Host/provider concern; journal is not a lock | Cluster setting; provider-dependent  |
| Post-commit hooks              | `AfterUp` / `AfterDown`          | Maintenance stages                              | Host/seeding lifecycle; not direct equivalent                         | Host / ordered scripts                       | Host / ordered scripts               |

A scope, checksum, history primary key or ordinary database write lock does not prove that two deployments can safely run the entire sequence concurrently. Evolve's cluster setting must be checked for the selected provider; it is not a blanket SQLite session-lock guarantee.

## Deployment, inspection and configuration

Evidence: [Migrator runner][m-runner] and [execution][m-execution]; [FluentMigrator runners][f-start] and [configuration][f-config]; [EF Core deployment][ef-applying]; [DbUp usage][d-usage], [variables][d-variables] and [logging][d-logging]; [Evolve execution][e-start] and [options][e-options].

| Capability                           | Migrator                                          | FluentMigrator                             | EF Core                           | DbUp                             | Evolve                                           |
| ------------------------------------ | ------------------------------------------------- | ------------------------------------------ | --------------------------------- | -------------------------------- | ------------------------------------------------ |
| Packaged CLI                         | No                                                | Yes                                        | `dotnet ef`                       | Core library; custom host        | Yes                                              |
| Dedicated migration bundle generator | No; publish host                                  | Package runner/migrations                  | Yes                               | Publish host                     | CLI distribution, not EF-style bundle generation |
| Review SQL without applying          | No equivalent runner SQL generator                | Preview/output                             | Scripts                           | Authored SQL / pending scripts   | Authored SQL                                     |
| Dry-run qualification                | Skips bodies; still touches provider/transactions | Processor preview; user code needs care    | Not a full side-effect simulation | Pending list / custom simulation | `RollbackAll` actually executes                  |
| Idempotent deployment SQL            | Custom                                            | Preview is not idempotent history guarding | Provider-dependent; not SQLite    | Author SQL / use journal         | Author SQL / use metadata                        |
| Status                               | Versions / loaded types                           | Runner/tool info                           | CLI / history APIs                | Pending/executed APIs            | `info`                                           |
| Command timeout                      | Provider setting                                  | Processor setting                          | Database/provider setting         | Runner/provider setting          | `CommandTimeout`                                 |
| Logging                              | `ILogger` / writers                               | Logging integration                        | EF logging                        | `IUpgradeLog` / integrations     | Host/CLI                                         |
| SQL substitution                     | Custom                                            | Script tokens                              | Custom logic                      | `$variable$`                     | `${placeholder}`                                 |
| Deployment identity                  | Host connection                                   | Runner connection                          | Migration connection              | Host connection                  | Tool connection                                  |

**Migrator dry run is not an offline SQL preview.** Execution starts provider work while `Up()`/`Down()` are skipped. It cannot show SQL from those skipped bodies and should not be described as side-effect-free database validation. [Execution source][m-execution].

## Database coverage and portability

| Framework      | How support is supplied                                                                                                                                                                                      | What it does not guarantee                                         |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------ |
| Migrator       | Source dialects + separate ADO.NET drivers. Live CI covers SQLite, SQL Server, PostgreSQL, Oracle, MySQL, MariaDB, Firebird, Db2, Informix and Sybase; Ingres is another source dialect. [CI guide][m-live]. | Every server/driver release, operation or arbitrary SQL construct. |
| FluentMigrator | Provider generators/processors. [Configuration][f-config].                                                                                                                                                   | The same expression working on every engine.                       |
| EF Core        | Relational provider packages. [Multiple providers][ef-providers].                                                                                                                                            | One provider's generated migrations working unchanged elsewhere.   |
| DbUp           | Database integrations. [Provider list][d-databases].                                                                                                                                                         | SQL dialect translation.                                           |
| Evolve         | Database integrations. [Requirements][e-requirements].                                                                                                                                                       | SQL translation or identical transactions.                         |

A migration can compile yet require a table copy, lose an unsupported schema detail or fail on existing data. Compare the exact operation and data shape, not just database names.

## SQLite emulation comparison

### What emulation means

SQLite has native table rename, column rename, add-column and (on sufficiently recent engines, subject to restrictions) drop-column operations. SQLite 3.53.0 added native `ALTER COLUMN … SET/DROP NOT NULL`; it still does not provide general type/default alteration or `ALTER TABLE ADD/DROP CONSTRAINT`. More complex changes require a replacement table, copying rows and rebuilding dependent objects. Native capabilities evolve independently of the .NET driver package. [SQLite ALTER TABLE reference][sqlite-alter].

Migrator reads the **live schema** into `SQLiteTableInfo`, modifies that representation and calls `RecreateTable`. It creates `<table>Temp`, copies mapped columns with `INSERT … SELECT`, drops the original, renames the replacement and recreates represented indexes. This works without an ORM model, but depends on what its schema reader can represent. [Implementation][m-sqlite], [schema model][m-sqlite-model].

### Automatic operation matrix

**R** = built-in rebuild; **N** = native SQL path, subject to engine restrictions; **U** = unique-index substitution; **Manual** = author the change/rebuild yourself; **Manual** also covers a generated statement that the engine does not support. Rows describe **changes to an existing table**, not constraints declared when creating it.

The combined SQL-runner column applies **individually to DbUp and Evolve**: both execute supplied SQL rather than diffing/rebuilding the schema. grate and RoundhousE follow the same distinction. Manual does not mean the engine cannot perform the operation.

Evidence: [EF Core SQLite operation table][ef-sqlite], [FluentMigrator SQLite generator][f-sqlite-generator], [inherited SQL templates][f-generic-generator] and [processor][f-sqlite-processor], [DbUp scripts][d-usage], [Evolve concepts][e-concepts]. Migrator cells are supported by the source/test inventory below.

| Existing-table operation     | Migrator                          | FluentMigrator                  | EF Core     | DbUp / Evolve                                  |
| ---------------------------- | --------------------------------- | ------------------------------- | ----------- | ---------------------------------------------- |
| Add ordinary column          | R                                 | N                               | N           | Manual SQL                                     |
| Remove column                | R                                 | N; engine restrictions          | R           | Manual SQL/rebuild                             |
| Rename column                | R                                 | N; engine restrictions          | N           | Manual SQL/rebuild                             |
| Change declared type         | R                                 | Manual                          | R           | Manual rebuild                                 |
| Change nullability           | R                                 | Manual                          | R           | Manual SQL on 3.53+ / rebuild on older engines |
| Change default               | R via full `Column`               | Manual                          | R via alter | Manual rebuild                                 |
| Remove default               | R via dedicated API; caveat below | Manual                          | R via alter | Manual rebuild                                 |
| Add primary key              | R                                 | Manual                          | R           | Manual rebuild                                 |
| Remove primary key           | R                                 | Manual                          | R           | Manual rebuild                                 |
| Add foreign key              | R                                 | Manual                          | R           | Manual rebuild                                 |
| Remove foreign key           | R                                 | Manual                          | R           | Manual rebuild                                 |
| Add unique constraint        | R                                 | U                               | R           | Manual rebuild/index                           |
| Remove unique constraint     | R                                 | U for tool-created unique index | R           | Manual rebuild/index                           |
| Add check constraint         | R                                 | Manual                          | R           | Manual rebuild                                 |
| Remove check constraint      | R                                 | Manual                          | R           | Manual rebuild                                 |
| Create / drop ordinary index | N                                 | N                               | N           | Manual SQL                                     |
| Rename table                 | N                                 | N                               | N           | Manual SQL                                     |

The table describes framework paths, not everything the newest SQLite engine can do. Migrator still rebuilds for nullability changes; FluentMigrator still rejects its general alter-column expression even when a newer engine can execute a hand-authored NOT NULL alteration.

EF Core rebuilds rely on model-represented artifacts; the docs identify failures for artifacts outside that model. EF 9+ uses a SQLite lock table with abandoned-lock recovery considerations. These are separate from rebuild support. [SQLite limitations][ef-sqlite].

FluentMigrator supports inline FKs during table creation. Its reviewed generator directs callers to manual reconstruction for later FK changes; `LOOSE` mode skips unsupported expressions rather than emulating them. Unique-index substitution does not imply that an existing table-level UNIQUE constraint can be dropped as an index. [Generator][f-sqlite-generator].

### Migrator's emulated operations, precisely

Methods refer to the pinned [SQLite provider][m-sqlite]. Tests illustrate evidence, not exhaustive coverage of every data/schema combination.

| API / operation                      | Implementation behavior                                                                          | Qualification / evidence                                                                                                                                                              |
| ------------------------------------ | ------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AddColumn`                          | Adds a column and mapping without an old source column; rebuilds.                                | Existing rows receive SQLite default/NULL behavior; incompatible NOT NULL requirements can fail. [Tests][t-add-column].                                                               |
| `ChangeColumn`                       | Replaces the entire matching `Column` definition; rebuilds.                                      | Specify properties to retain. Type affinity during copying is not arbitrary data conversion. [Tests][t-change-column].                                                                |
| `RemoveColumnDefaultValue`           | Clears parsed default; rebuilds.                                                                 | Dedicated API is exercised, but generic `ChangeColumn_RemoveDefaultValue_Success` is skipped under issue #139. Not every default-removal path is verified. [Tests][t-sqlite-general]. |
| `RemoveColumn`                       | Removes column/mapping and matching single-column indexes/uniques/FKs; rebuilds affected tables. | Rejects detected CHECK references and composite dependencies until adjusted. Can remove inbound single-column FKs from other tables. [Tests][t-remove-column].                        |
| `RenameColumn`                       | Changes copy mapping, column and represented key/index references; adjusts referencing tables.   | Requires FK enforcement off; not an arbitrary SQL-expression rewriter. [Tests][t-rename-column].                                                                                      |
| `AddPrimaryKey`                      | Sets membership, orders selected columns, rebuilds.                                              | Composite keys supported; `PrimaryKeyExists` checks for any PK rather than matching its name. [Tests][t-pk].                                                                          |
| `RemovePrimaryKey`                   | Clears PK/PK-identity flags; rebuilds.                                                           | Changes identity-related semantics; review referencing tables. [Source][m-sqlite].                                                                                                    |
| `AddForeignKey` / `RemoveForeignKey` | Adds/removes represented FK; rebuilds child table.                                               | Validate existing rows and enforcement. [FK tests][t-fk], [integrity tests][t-integrity].                                                                                             |
| `AddUniqueConstraint`                | Adds named unique definition; rebuilds.                                                          | Duplicate data can reject the copy. [Metadata tests][t-uniques].                                                                                                                      |
| `AddCheckConstraint`                 | Adds named CHECK SQL; rebuilds.                                                                  | Predicate must accept existing rows and be understood by the reader. [Tests][t-check].                                                                                                |
| `RemoveConstraint`                   | Removes matching unique and check definitions; rebuilds.                                         | Does not remove FKs/PKs; use dedicated APIs. [Source][m-sqlite].                                                                                                                      |
| `RemoveAllConstraints`               | Removes PK/unique definitions via rebuilds.                                                      | Retains FKs and leaves CHECK handling incomplete; not an all-constraint eraser. [Tests][t-remove-constraints], [source][m-sqlite].                                                    |
| `RemoveAllIndexes`                   | Clears indexes **and unique constraints**; rebuilds.                                             | Broader than dropping non-unique indexes. [Source][m-sqlite].                                                                                                                         |
| `RecreateTable`                      | Public low-level schema/mapping reconstruction.                                                  | Requires a consistent supported representation. [Composite-key round-trip test][t-recreate].                                                                                          |
| `TruncateTable`                      | Emits `DELETE FROM`.                                                                             | Not native TRUNCATE and not an identity-sequence reset. [Source][m-sqlite].                                                                                                           |

### What survives reconstruction—and what is not guaranteed

| Schema/data detail                             | Migrator at the pinned revision                                           | Implication                                                                                 |
| ---------------------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Mapped rows                                    | Named-column `INSERT … SELECT`.                                           | New constraints/types must accept the data.                                                 |
| Names, parsed types, nullability, defaults     | Included in column model.                                                 | Not a lossless representation of arbitrary CREATE SQL.                                      |
| Composite PKs                                  | Represented; dedicated rebuild test.                                      | Check membership/order when replacing definitions.                                          |
| FKs and delete actions                         | Read from schema/PRAGMA; emitted into replacement DDL.                    | Not a promise about every clause, e.g. arbitrary deferrability.                             |
| Unique / CHECK definitions                     | Included in `SQLiteTableInfo`.                                            | Reader restrictions apply; rename does not rewrite arbitrary CHECK expressions.             |
| Indexes / represented filters                  | Recreated after replacement.                                              | Complex predicates, expressions, collations and sort details require separate verification. |
| Triggers                                       | No trigger collection/replay in schema model or rebuild.                  | Do not assume preservation; a table drop removes its triggers. Recreate as needed.          |
| Views / dependent SQL                          | No general dependency-SQL rewrite.                                        | Validate/recreate dependencies after renames/drops.                                         |
| `WITHOUT ROWID`, `STRICT`, generated columns   | Not modeled as a complete round-trip contract.                            | No blanket preservation claim for external schemas.                                         |
| Hidden `rowid` / AUTOINCREMENT high-water mark | Only mapped columns copied; no explicit sequence-state restoration.       | Historical rowid/sequence metadata may change.                                              |
| Type / length enforcement                      | Changes declarations, not SQLite typing rules.                            | Declared size is not SQL Server-like length enforcement.                                    |
| FK enforcement state                           | Runner disables before migration and restores after successful execution. | Direct provider calls differ; exception restoration is not proven by success-path tests.    |
| Whole-database FK validation                   | Integrity helper exists; runner does not automatically invoke it.         | Enabling enforcement alone does not validate existing rows.                                 |

Evidence: [SQLite provider][m-sqlite], [schema model][m-sqlite-model], [execution][m-execution], [SQLite reconstruction procedure][sqlite-alter]. Re-evaluate these limitations after the parallel refactoring.

### How the other frameworks compare on preservation

| Framework          | Replacement schema source                                       | Responsibility for unsupported dependencies                                                                                 |
| ------------------ | --------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Migrator           | Live reader + `SQLiteTableInfo`.                                | Author handles objects outside the representation.                                                                          |
| EF Core            | Model/migration metadata.                                       | Author handles artifacts outside automatic model-based rebuilding. [Docs][ef-sqlite].                                       |
| FluentMigrator     | No general rebuild engine found in inspected SQLite components. | Author writes reconstruction for unsupported alterations. [Generator][f-sqlite-generator], [processor][f-sqlite-processor]. |
| DbUp               | Project SQL / C#.                                               | Script author. [Usage][d-usage].                                                                                            |
| Evolve             | Project SQL.                                                    | Script author. [Concepts][e-concepts].                                                                                      |
| grate / RoundhousE | Project SQL.                                                    | Script author. Database integration is not emulation. [grate][g-home], [RoundhousE][r-home].                                |
| EF6                | Selected provider's migration generator.                        | Provider-specific; EF Core rebuild support must not be attributed to EF6. No specific EF6 SQLite emulation verified here.   |

**Practical conclusion:** Migrator's differentiator is live-schema-based SQLite reconstruction without an ORM model. It is not unique in automatic SQLite rebuilding—EF Core also does this—and is not a lossless rewriter of every SQLite schema feature.

## EF6, grate and RoundhousE

Evidence: [EF6 migrations][ef6-main], [automatic migrations][ef6-auto], [history][ef6-history], [CLI][ef6-cli]; [grate home][g-home], [configuration][g-config], [script types][g-types], [anytime][g-anytime], [everytime][g-everytime], [one-time][g-onetime]; [RoundhousE][r-home] and [grate migration guide][g-migrate].

| Capability                                  | EF6 Code First                                | grate                             | RoundhousE                                      |
| ------------------------------------------- | --------------------------------------------- | --------------------------------- | ----------------------------------------------- |
| Authoring                                   | C# from EF6 model                             | Lifecycle SQL folders             | Lifecycle SQL folders                           |
| ORM dependency                              | EF6 model/context                             | None                              | None                                            |
| Model-difference generation                 | Yes                                           | No                                | No                                              |
| Automatic migrations without explicit files | Optional EF6 feature                          | No                                | No                                              |
| Reverse version                             | `Down`, target migration                      | Forward/custom recovery           | Forward/custom recovery                         |
| Change once                                 | Versioned migration                           | One-time scripts                  | One-time scripts                                |
| Run after content change                    | Not a SQL repeatable mechanism                | Anytime scripts                   | Anytime workflow                                |
| Every deployment                            | Seed/custom lifecycle                         | Everytime scripts                 | Everytime workflow                              |
| Detect script edits                         | Not a script checksum journal                 | One-time hash checking            | Changed-script policies                         |
| Existing-schema baseline                    | Existing-schema workflow                      | `--baseline`                      | Verify release's workflow                       |
| Transactions                                | EF/provider execution                         | Opt-in `--transaction`            | Transaction flags / outside-transaction scripts |
| Environment filtering                       | Host/configuration                            | Filename conventions              | Environment scripts                             |
| SQL token replacement                       | Custom                                        | User tokens                       | Tokens                                          |
| History separation                          | Context history / customization               | Migration schema/configuration    | Repository/schema conventions                   |
| Preview / inspection                        | Script generation                             | `--dryrun`, logs                  | Check release's dry-run/log tooling             |
| Execution                                   | PMC/runtime; `ef6.exe` replaces `migrate.exe` | CLI; self-contained distributions | CLI / .NET tooling                              |
| Automatic SQLite emulation                  | Provider-specific; not verified               | None in documented workflow       | None in documented workflow                     |

RoundhousE maintainers point to grate as a successor. The migration guide documents differences; do not assume parity for every flag, history configuration or folder. This is a compatibility consideration, not a claim of identical release/support status.

## Flyway and Liquibase in a .NET deployment

These can migrate databases used by .NET applications, but do not replace Migrator's in-process C# transformation API directly. This narrower comparison avoids folding edition-dependent features into the main matrices.

| Concern                   | Flyway                                                            | Liquibase                                           |
| ------------------------- | ----------------------------------------------------------------- | --------------------------------------------------- |
| Artifacts                 | Versioned / repeatable migrations                                 | Changelog changesets, including formatted SQL       |
| Recovery                  | Explicit undo migrations where the selected edition supports Undo | Change-type-dependent / authored rollback           |
| Selection and assumptions | Tool configuration; check command/edition                         | Contexts/preconditions; format/version restrictions |
| Automatic SQLite rebuild  | Not established here; supplied SQL is not emulation               | Not established here; verify change type/extension  |
| .NET integration          | Separate deployment tool                                          | Separate deployment tool                            |

Sources: [Flyway Undo][flyway-undo], [baseline migrations][flyway-baseline], [Liquibase rollback][liquibase-rollback], [preconditions][liquibase-preconditions]. This document does not claim that every command is available in a free edition.

## Choosing a framework and identifying Migrator gaps

These interpretations are grounded in the preceding evidence, rather than universal recommendations.

| Requirement                                    | Candidate / tradeoff                                                    |
| ---------------------------------------------- | ----------------------------------------------------------------------- |
| No ORM model, frequent SQLite alterations      | Evaluate Migrator's live-schema reconstruction and preservation limits. |
| EF model defines schema                        | EF Core supplies scaffolding, rebuilds and deployment artifacts.        |
| Handwritten C# / packaged runners / fluent DSL | FluentMigrator; manual work for unsupported SQLite alterations.         |
| SQL-first runner composed in .NET              | DbUp's script providers, journal and transaction strategies.            |
| SQL checksums / change-triggered repeatables   | Evolve's built-in conventions.                                          |
| Existing RoundhousE folders                    | Evaluate grate's migration guide and history compatibility.             |
| Existing EF6 application                       | Assess EF6/provider behavior separately from EF Core.                   |
| Multi-language database-owned deployment       | Evaluate Flyway/Liquibase and required editions.                        |

Potential Migrator improvements, **not implemented-feature claims**:

1. A packaged CLI and dedicated SQL-preview/export workflow.
2. Validation of edits to already applied migration content.
3. Cross-process migration locking and explicit failure recovery.
4. Repeatable migrations distinct from execution hooks.
5. Stronger SQLite preservation of triggers, generated columns, table options and complex indexes.
6. Clearer bulk-removal semantics and FK-state restoration after exceptions.
7. Continued operation-level provider documentation and live test coverage.

## Validation and maintenance

Reviewed in a separate Git worktree based on `ab3aa9f`. No migration implementation files or parallel-refactoring checkout were changed.

The existing SQLite category was executed on Windows:

```sh
dotnet test src/Migrator.Tests/Migrator.Tests.csproj --configuration Release --filter "TestCategory=SQLite"
```

**139 passed, 1 skipped, 0 failed.** The skipped test is `ChangeColumn_RemoveDefaultValue_Success`, documented by [issue #139](https://github.com/dotnetprojects/Migrator.NET/issues/139). Existing compiler warnings were present. This validates existing scenarios, not the complete preservation matrix. Competitors were reviewed through documentation/source, **not executed in a comparative test harness**.

When updating:

- Pin the new source revision and recheck SQLite rebuilds after refactoring.
- Verify competitor provider versions before promoting “Check” to a compatibility promise.
- Keep native SQL, automatic emulation and author-written workarounds distinct.
- Review ignored tests, schema round trips and real data, not just generated SQL.
- Update the date, sources and homepage summary together.

## Source index

- **Migrator:** [revision][m-revision], [runner][m-runner], [loader][m-loader], [execution][m-execution], [lifecycle][m-migration], [API][m-api], [history][m-provider], [factory][m-factory], [live tests][m-live], [SQLite implementation][m-sqlite], [SQLite model][m-sqlite-model].
- **FluentMigrator:** [quick start][f-start], [configuration][f-config], [SQL][f-sql], [auto-reverse][f-reverse], [maintenance][f-maintenance], [profiles][f-profiles], pinned [SQLite generator][f-sqlite-generator] and [processor][f-sqlite-processor].
- **EF Core:** [overview][ef-overview], [management][ef-managing], [deployment][ef-applying], [history][ef-history], [providers][ef-providers], [seeding][ef-seeding], [SQLite][ef-sqlite].
- **DbUp:** [usage][d-usage], [providers][d-providers], [journal][d-journal], [script types][d-types], [transactions][d-transactions], [variables][d-variables], [logging][d-logging], [databases][d-databases], [philosophy][d-philosophy].
- **Evolve:** [concepts][e-concepts], [options][e-options], [execution][e-start], [requirements][e-requirements].
- **EF6:** [migrations][ef6-main], [automatic][ef6-auto], [history][ef6-history], [CLI][ef6-cli].
- **grate / RoundhousE:** [grate][g-home], [options][g-config], [script types][g-types], [migration guide][g-migrate], [RoundhousE][r-home].
- **SQLite engine:** [ALTER TABLE and reconstruction procedure][sqlite-alter].

[m-runner]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/Migrator.cs
[m-loader]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/MigrationLoader.cs
[m-execution]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/MigrateAnywhere.cs
[m-migration]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/Framework/Migration.cs
[m-api]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/Framework/ITransformationProvider.cs
[m-provider]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/Providers/TransformationProvider.cs
[m-factory]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/ProviderFactory.cs
[m-live]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/docs/live-database-tests.md
[m-sqlite]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/Providers/Impl/SQLite/SQLiteTransformationProvider.cs
[m-sqlite-model]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator/Providers/Impl/SQLite/Models/SQLiteTableInfo.cs
[t-add-column]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_AddColumnTests.cs
[t-change-column]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_ChangeColumnTests.cs
[t-remove-column]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_RemoveColumnTests.cs
[t-rename-column]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_RenameColumnTests.cs
[t-pk]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_AddPrimaryKeyTests.cs
[t-fk]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_AddForeignKeyTests.cs
[t-integrity]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_CheckForeignKeyIntegrityTests.cs
[t-uniques]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_GetUniques.cs
[t-check]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_GetCheckConstraintsTests.cs
[t-remove-constraints]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_RemoveAllConstraintsTests.cs
[t-recreate]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProvider_RecreateTable.cs
[t-sqlite-general]: https://github.com/dotnetprojects/Migrator.NET/blob/ab3aa9f488196139334ae4b2ea335e803a280533/src/Migrator.Tests/Providers/SQLite/SQLiteTransformationProviderTests.cs
[m-revision]: https://github.com/dotnetprojects/Migrator.NET/tree/ab3aa9f488196139334ae4b2ea335e803a280533/
[f-start]: https://fluentmigrator.github.io/intro/quick-start.html
[f-config]: https://fluentmigrator.github.io/intro/configuration.html
[f-sql]: https://fluentmigrator.github.io/operations/execute-sql.html
[f-reverse]: https://fluentmigrator.github.io/migration-types/auto-reversing.html
[f-maintenance]: https://fluentmigrator.github.io/migration-types/maintenance.html
[f-profiles]: https://fluentmigrator.github.io/migration-types/profiles.html
[f-sqlite-generator]: https://github.com/fluentmigrator/fluentmigrator/blob/2e0acdb7c375b03e50e65f34ddf50e44ee45df30/src/FluentMigrator.Runner.SQLite/Generators/SQLite/SQLiteGenerator.cs
[f-sqlite-processor]: https://github.com/fluentmigrator/fluentmigrator/blob/2e0acdb7c375b03e50e65f34ddf50e44ee45df30/src/FluentMigrator.Runner.SQLite/Processors/SQLite/SQLiteProcessor.cs
[ef-overview]: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
[ef-managing]: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing
[ef-applying]: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
[ef-history]: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table
[ef-providers]: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers
[ef-seeding]: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
[ef-sqlite]: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
[d-usage]: https://dbup.readthedocs.io/en/latest/usage/
[d-providers]: https://dbup.readthedocs.io/en/latest/more-info/script-providers/
[d-journal]: https://dbup.readthedocs.io/en/latest/more-info/journaling/
[d-types]: https://dbup.readthedocs.io/en/latest/more-info/script-types/
[d-transactions]: https://dbup.readthedocs.io/en/latest/more-info/transactions/
[d-variables]: https://dbup.readthedocs.io/en/latest/more-info/variable-substitution/
[d-logging]: https://dbup.readthedocs.io/en/latest/more-info/logging/
[d-databases]: https://dbup.readthedocs.io/en/latest/supported-databases/
[d-philosophy]: https://dbup.readthedocs.io/en/latest/philosophy-behind-dbup/
[e-concepts]: https://evolve-db.netlify.app/concepts/
[e-options]: https://evolve-db.netlify.app/configuration/options/
[e-start]: https://evolve-db.netlify.app/getting-started/
[e-requirements]: https://evolve-db.netlify.app/requirements/
[ef6-main]: https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/migrations/
[ef6-auto]: https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/migrations/automatic
[ef6-history]: https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/migrations/history-customization
[ef6-cli]: https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/migrations/ef6-exe
[g-home]: https://grate-devs.github.io/grate/
[g-config]: https://grate-devs.github.io/grate/configuration-options/
[g-types]: https://grate-devs.github.io/grate/script-types/
[g-anytime]: https://grate-devs.github.io/grate/script-types/anytime/
[g-everytime]: https://grate-devs.github.io/grate/script-types/everytime/
[g-onetime]: https://grate-devs.github.io/grate/script-types/one-time/
[g-migrate]: https://grate-devs.github.io/grate/migrating-from-roundhouse/
[r-home]: https://github.com/chucknorris/roundhouse
[sqlite-alter]: https://www.sqlite.org/lang_altertable.html
[flyway-undo]: https://documentation.red-gate.com/flyway/reference/commands/undo
[flyway-baseline]: https://www.red-gate.com/hub/product-learning/flyway/flyways-baseline-migrations-explained-simply/
[liquibase-rollback]: https://support.liquibase.com/hc/en-us/articles/29383086010523-How-to-Define-Rollbacks
[liquibase-preconditions]: https://docs.liquibase.com/community/user-guide-5-0-4/what-are-preconditions
[f-generic-generator]: https://github.com/fluentmigrator/fluentmigrator/blob/2e0acdb7c375b03e50e65f34ddf50e44ee45df30/src/FluentMigrator.Runner.Core/Generators/Generic/GenericGenerator.cs
