# Imperative/fluent capability inventory

The machine-readable [inventory](fluent-operation-coverage.json) maps every named method family on `ITransformationProvider` to fluent authoring, schema inspection or an explicit context API. `FluentCoverageTests` checks this inventory against reflection so new normal-API methods require an entry. Overload convenience is consolidated into typed definitions; this does not promise FluentMigrator source compatibility.

| Capability | Authoring / inspection | Validation evidence |
| --- | --- | --- |
| Complete tables, columns, types, defaults, identity, nullability and precision | `Create.Table(...).WithColumn(...)`, `Create.Column(...).OnTable(...)`, `Alter.Column(...).OnTable(...)` | Shared column options, independent column handles and snapshots, SQLite schema/data |
| Keys, foreign-key actions, unique/check constraints | `Create.PrimaryKey/UniqueConstraint/CheckConstraint(...).OnTable(...)`; `Create.ForeignKey(...).FromTable(...).WithColumns(...).ToTable(...).WithColumns(...).OnDelete(...).OnUpdate(...)` | Composite key order, independent actions and provider dispatch; unsupported providers throw |
| Index keys, include/filter/cluster options | `Create.Index(name).OnTable(table).WithColumns(...)` or `Create.Index(definition).OnTable(table)` | Input/build snapshots, provider index suites; preview rejects unsupported options |
| Views and joins | `Create.View(name).FromTable(table).WithFields(...)` or `.WithElements(...)` | Definitions snapshot caller input; provider implementations retain their own limitations |
| Drop/rename operations | `Delete` / `Rename` | SQLite schema tests, reversal tests; destructive changes need explicit reverse definitions |
| Inserts, conditional insert, update, delete | `Insert.IntoTable(...).Row(...).IfNotExists(...)`, `Update.Table(...).Set(...).Where(...)`, `Delete.FromTable(...).Where(...)`; explicit `AllRows()` for unfiltered update/delete | Persisted rows, invalid/incomplete expressions rejected before execution |
| Truncate, data copy, update from another table | `Execute.Truncate(table)`, `Execute.CopyDataFromTable(source).ToTable(target).WithColumns(...)`, `Execute.UpdateTable(target).FromTable(source).Set(...).Match(...)` | SQLite copy, normal provider tests, mutable pair snapshots |
| SQL, files, resources | `Execute.Sql/Script/EmbeddedScript` | SQL execution/preview tests; SQL Server GO batch splitting through the script APIs, with explicit rejection of unsupported client directives |
| Scalars/readers/existence/metadata | `Schema`, `Schema.Table(table).Select(...) / SelectScalar(...)` | Reader disposal and persisted-data assertions; nullable helpers are additive |
| Database administration | `Administration` | Typed operations flag transaction incompatibility; backend capabilities still apply |
| Provider conditions, commands/connections | `IfProvider`, `Execute.WithCommand/WithConnection/WithProvider` | Inactive reversal, explicit preview rejection; callbacks execute trusted code |
| History and transaction administration | Explicit `Context` | Runner transaction/scope/history regressions; not schema expressions |

Execution coverage is broader than SQL-generation and automatic-reversal coverage. Unsupported preview/reversal operations fail explicitly. The inventory is an API coverage check, not a claim that every overload has an independent live test on every engine. Continue adding behavioral provider tests when changing an operation's semantics.
