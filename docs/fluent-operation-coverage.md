# Imperative/fluent capability inventory

The machine-readable [inventory](fluent-operation-coverage.json) maps every named method family on `ITransformationProvider` to fluent authoring, schema inspection or an explicit context API. `FluentCoverageTests` checks this inventory against reflection so new normal-API methods require an entry. Overload convenience is consolidated into typed definitions; this does not promise FluentMigrator source compatibility.

| Capability | Authoring / inspection | Validation evidence |
| --- | --- | --- |
| Complete tables, columns, types, defaults, identity, nullability and precision | `Create.Table`, `Create.Column`, `Alter.Column` | Complete table snapshot, SQLite schema/data, provider precision tests |
| Keys, foreign-key actions, unique/check constraints | `Create.PrimaryKey`, `ForeignKey`, `Unique`, `Check` | Provider constraint/action suites; unsupported providers throw |
| Index keys, include/filter/cluster options | `Create.Index` with the existing `Index` model | Provider index suites; preview rejects unsupported options |
| Views and joins | `Create.View` with either normal definition model | Definitions snapshot caller input; provider implementations retain their own limitations |
| Drop/rename operations | `Delete` / `Rename` | SQLite schema tests, reversal tests; destructive changes need explicit reverse definitions |
| Inserts, conditional insert, update, delete | `Insert`, `Update`, `Delete.FromTable` | FluentDataChangesAndSchemaReadsPersistExpectedRows |
| Truncate, data copy, update from another table | `Execute.Truncate/CopyData/UpdateFrom` | Normal provider tests; mutable pair snapshot regression |
| SQL, files, resources | `Execute.Sql/Script/EmbeddedScript` | SQL execution/preview tests; provider-specific batch splitting is separate work |
| Scalars/readers/existence/metadata | `Schema`, `Schema.Table`, `Select`, `SelectScalar` | Reader disposal and persisted-data assertions; nullable helpers are additive |
| Database administration | `Administration` | Typed operations flag transaction incompatibility; backend capabilities still apply |
| Provider conditions, commands/connections | `IfDatabase`, `Execute.WithCommand/WithConnection/WithProvider` | Inactive reversal, explicit preview rejection; callbacks execute trusted code |
| History and transaction administration | Explicit `Context` | Runner transaction/scope/history regressions; not schema expressions |

Execution coverage is broader than SQL-generation and automatic-reversal coverage. Unsupported preview/reversal operations fail explicitly. The inventory is an API coverage check, not a claim that every overload has an independent live test on every engine. Continue adding behavioral provider tests when changing an operation's semantics.
