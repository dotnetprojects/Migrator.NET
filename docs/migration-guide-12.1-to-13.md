# Migrating from 12.1 to 13

Version 13 is a breaking release. This guide covers compatibility changes when updating existing migrations. Validate the upgrade on a restored database before running a changed migration history against production. For current API usage, see the [migration manual](https://dotnetprojects.github.io/Migrator.NET/guide/).

## Schema model

Columns describe data type, length, precision/scale, nullability, identity generation and defaults. Primary keys, unique constraints, foreign keys and checks belong to the table. Indexes are separate schema objects: a unique index is not automatically a unique constraint.

The v13 API accepts complete named constraint definitions when creating a table, and returns the same kinds of definitions from metadata inspection. Key column order is significant. A composite UNIQUE constraint must never mark each member column as individually unique. Altering a column must not infer that its table constraints should be removed.

The old column flags and duplicate fluent builder are removed. These are source-breaking changes: update historical migration source before recompiling for v13; the runner preserves the existing history table format.

## Implemented breaking changes

### Column flags, constructors and inspection

`ColumnProperty`, its extensions, `Column.ColumnProperty`,
`IColumn.ColumnProperty`, `IsPrimaryKey` and `IsPrimaryKeyNonClustered` are
removed. Constructors and `AddColumn` overloads taking flags are removed.

| 12.1 | 13 |
| --- | --- |
| `ColumnProperty.Null` / `None` | `IsNullable = true` (the default) |
| `ColumnProperty.NotNull` | `IsNullable = false` |
| `ColumnProperty.Identity` | `IsIdentity = true` |
| `ColumnProperty.Unsigned` | `IsUnsigned = true` |
| `ColumnProperty.PrimaryKey` | `new PrimaryKeyConstraint(name, columns)` in the table definition |
| `PrimaryKeyWithIdentity` | Identity on the column plus a separate primary key |
| `PrimaryKeyNonClustered` | `new PrimaryKeyConstraint(name, columns) { NonClustered = true }` |
| `ColumnProperty.Unique` | `new UniqueConstraint(name, columns)` |
| `ColumnProperty.Indexed` | An explicit `Index` definition |
| `ColumnProperty.CaseSensitive` | `Collation = "provider_collation_name"` |

For example, replace a flagged `AddColumn` call with:

```csharp
Database.AddColumn("Users", new Column("Email", DbType.String, 200)
{
    IsNullable = false,
    DefaultValue = "unknown"
});
Database.AddUniqueConstraint("UQ_Users_Email", "Users", "Email");
```

Use `DefaultValue = 10` for numeric defaults: a positional integer after the
type is the column **size**, not its default. Unsupported unsigned/collation
combinations produce diagnostics. Collation names are explicit; the SQL Server
provider no longer queries or guesses a case-sensitive database collation.
SQLite's former `CaseSensitive` flag emitted `NOCASE`; choose `BINARY` or
`NOCASE` explicitly for the desired behavior.

Read primary/unique membership through `GetTableConstraints(table)`, retaining
the constraint's member order. `GetColumns` returns column attributes only.
SQLite column metadata now follows physical column order, not primary-key order.
A plain SQLite INTEGER primary key remains a rowid alias; `IsIdentity` indicates
explicit `AUTOINCREMENT`, which is preserved on rebuild.

### One fluent authoring API

The obsolete `Framework.SchemaBuilder` namespace and `ExecuteSchemaBuilder`
method are removed. Use `Framework.Fluent.MigrationBuilder` and
`builder.Apply(provider)`, or derive from `FluentMigration`.
Replace `AddTable/AddColumn` chains with `Create.Table(...).WithColumn(...)`.
Replace `WithProperty`, unnamed `PrimaryKey()` and `Unique()` with
`NotNullable()`, `Identity()`, `Unsigned()`, `WithCollation(name)`,
`WithPrimaryKey(name, columns)` and `WithUniqueConstraint(name, columns)`.
Use `Create.ForeignKey` with separate delete/update actions.

### Column changes do not own constraints

`ChangeColumn` changes attributes without inferring creation or removal of
unique constraints. SQL Server's `AdoptColumnUniqueConstraint` and the implicit
ownership marker mechanism are removed. Use explicit `AddUniqueConstraint` and
`RemoveConstraint` calls. Existing extended-property markers are harmless;
v13 does not use them to delete constraints.

Oracle changes columns in place so native constraints remain attached; a type
conversion that Oracle cannot perform must be expressed as an explicit data
migration. Identity validation runs before table creation, and identity no longer
requires primary-key membership.

SQLite `RemoveAllIndexes` now preserves table UNIQUE constraints. To remove
constraints too, call `RemoveAllConstraints` explicitly. SQLite
`PrimaryKeyExists(table, name)` checks the actual name (use null for an unnamed
legacy key), rather than returning true for any primary key.

### `Unique` is renamed to `UniqueConstraint`

Replace `new Unique { Name = "UQ_Users_Email", KeyColumns = ["Email"] }` with `new UniqueConstraint("UQ_Users_Email", "Email")`. When importing both `System.Data` and `DotNetProjects.Migrator.Framework`, use an alias for the latter's `UniqueConstraint` (ADO.NET also defines that name).

### Explicit table keys and complete constraint definitions

```csharp
Database.AddTable("Users",
    new Column("TenantId", DbType.Int32),
    new Column("Id", DbType.Int32),
    new Column("Email", DbType.String, 200),
    new PrimaryKeyConstraint("PK_Users", "TenantId", "Id"),
    new UniqueConstraint("UQ_Users_Email", "TenantId", "Email"),
    new CheckConstraint("CK_Users_Id", "Id > 0"));
```

The supplied key order is preserved. Explicit primary-key definitions make their columns non-nullable without mutating the caller's column objects. This also rejects NULL in a composite SQLite primary key; old flag-based composite SQLite keys allowed NULL. SQLite identity requires a single INTEGER primary key and rejects incompatible combinations instead of silently removing identity.

Fluent equivalent: append `.WithPrimaryKey("PK_Users", "TenantId", "Id")`, `.WithUniqueConstraint("UQ_Users_Email", "TenantId", "Email")`, or `.WithCheckConstraint("CK_Users_Id", "Id > 0")` to the table builder. Each is part of the complete table definition.

### Structured constraint inspection

Use `Database.GetTableConstraints("Users")`, or `Schema.Table("Users").ConstraintDefinitions()`, then select `PrimaryKeyConstraint`, `UniqueConstraint`, `ForeignKeyConstraint` or `CheckConstraint`. Key column order belongs to the constraint. A unique index stays in index metadata. SQLite returns `Name == null` for unnamed legacy constraints; a backing autoindex name is not an invented constraint name.

Structured readers cover SQLite, SQL Server, PostgreSQL, Oracle, MySQL, MariaDB, Db2, Firebird, Informix and Sybase. Each reader is exercised in its live CI job; unsupported engines throw `NotSupportedException`. MySQL identifies the primary key as `PRIMARY` regardless of a supplied symbolic name. Quoted qualified Oracle/MySQL lookups are currently rejected explicitly. These limitations must not be interpreted as empty metadata.

### Custom provider and dialect implementations

`ITransformationProvider` now requires `GetTableConstraints(string)`. Return accurate typed definitions, including ordered key members, or throw `NotSupportedException`; do not return an empty array for an unsupported reader. `IDialect` replaces `RegisterProperty/SqlForProperty` with `RegisterColumnAttribute/SqlForColumnAttribute`, using the non-flag `ColumnAttribute` enum (Null, NotNull, Identity, Unsigned). Custom column mappers use explicit column attributes; removed helpers include `IndexSql`, `PropertySelected`, `AddPrimaryKey`, `AddUnique`, and `AddForeignKey`. `GetPrimaryKeys(IEnumerable<Column>)` and column/index joining helpers are removed: inspect table constraints and create indexes explicitly. `GetCollationSql` generates a supported collation clause or throws before DDL. `IDialect` also adds `QuoteIdentifier(string)` for one identifier atom and `GetTableConstraintSql(TableConstraint)` for pure SQL rendering. Implementations derived from `Dialect` inherit defaults. Constraint names containing quote delimiters are escaped; a dot within a constraint name is not a schema separator.

### SQLite alterations preserve named primary keys

Rebuilding a table now retains an explicitly named primary key and its declared
column order. Changing a column definition does not implicitly remove that key.
To drop a column belonging to a named primary key, first call
`RemovePrimaryKey(table)`, then remove the column, and explicitly create any
replacement key. A failed attempt leaves the original table intact.

Rebuilds also retain physical column order for tables with named primary keys,
including when a column's type or size changes. Identity rebuilds retain the
constraint name and the sequence high-water mark.

## Provider authors and dialects (design)

Keep SQL rendering independent of a live connection. A dialect defines identifier quoting, type/literal rendering and SQL capabilities. Metadata readers inspect existing schema; execution manages commands, transactions and history. Connected preview may read history and schema before rendering, but it must not mutate the database. Offline rendering uses an explicitly supplied schema context.

The provider surface combines these concerns through execution and metadata contracts. SQL rendering uses a separate context. Unsupported combinations must fail explicitly before DDL, not disappear from generated SQL.

## Design references

Reviewed 2026-09-22:

- [EF Core CreateTableOperation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.operations.createtableoperation?view=efcore-10.0) separates columns, primary key, unique constraints, checks and foreign keys.
- [FluentMigrator ColumnDefinition](https://github.com/fluentmigrator/fluentmigrator/blob/main/src/FluentMigrator.Abstractions/Model/ColumnDefinition.cs) still carries constraint flags; its expression/generator separation is useful, but its column model is not the target here.
- [Alembic operations](https://alembic.sqlalchemy.org/en/latest/ops.html) distinguish named table constraints from column alteration and use explicit batch reconstruction for SQLite.

## Schema API changes

Typed constraints with ordered metadata, the SQLite constraint tokenizer, explicit SQL defaults, semantic collations and the consolidated authoring API work together. Use explicit object names and check provider-specific operation behavior when upgrading a custom dialect.

## Explicit SQL defaults and semantic collations

`RawSql.Insert("ksuid_new()")` marks trusted SQL as an expression in either API:

```csharp
new Column("Id", DbType.String, 27) { DefaultValue = RawSql.Insert("ksuid_new()") };
// Fluent:
builder.Create.Table("Events").WithColumn("Id").AsString(27)
    .WithDefaultValue(RawSql.Insert("ksuid_new()"));
```

The database must provide that function. Strings remain quoted values, so
`WithDefaultValue("ksuid_new()")` stores that text instead of calling a function.
SQLite wraps expressions in parentheses as required for expression defaults.
Metadata exposes unparsed SQL defaults as `RawSql`, replacing the previous private
expression object; inspect `RawSql.Sql` instead of assuming every default is a string.
Expression text is trusted migration code, not a parameter or a cross-database function abstraction.

`Column.Collation` is now a typed `Collation` value. String assignment still selects
a provider name through an implicit conversion; `Collation.Named("name")` is explicit.
Fluent `.WithCollation(...)` takes the same type.

```csharp
new Column("Name", DbType.String, 100) { Collation = Collation.CaseInsensitive };
builder.Create.Table("Names").WithColumn("Name").AsString(100)
    .WithCollation(Collation.CaseInsensitive);
```

| Preset | SQL Server | MySQL 8 | MariaDB 10.10+ | PostgreSQL | SQLite |
| --- | --- | --- | --- | --- | --- |
| `CaseInsensitive` (accent-sensitive) | Latin1 General 100 CI AS SC | utf8mb4 0900 as ci | utf8mb4 UCA1400 nopad as ci | Explicit installed name required | Unsupported |
| `CaseSensitive` (accent-sensitive) | Latin1 General 100 CS AS SC | utf8mb4 0900 as cs | utf8mb4 UCA1400 nopad as cs | Explicit installed name required | Explicit installed name required |
| `Binary` | Latin1 General 100 BIN2 | utf8mb4 0900 bin | utf8mb4 nopad bin | C | BINARY |
| `AsciiIgnoreCase` | Unsupported | Unsupported | Unsupported | Unsupported | NOCASE |

These presets describe comparison intent, not identical sorting, normalization,
language tailoring, or trailing-space behavior across engines. Use a named collation
for a specific language or exact provider semantics. MySQL/MariaDB presets require
utf8mb4-compatible text columns and the listed engine versions. Other dialects reject
unmapped presets; custom dialects can override `ResolveCollation(CollationKind)`.
Unsupported requests fail during SQL generation, before executing the table operation.
SQLite never downgrades Unicode case-insensitivity to its ASCII-only NOCASE behavior.
SQLite rebuilds preserve declared column collations, including named custom
collations registered on the connection. `GetColumns` reports these names.
Changing a collation explicitly rebuilds the table; a resulting uniqueness
violation rolls back the change and preserves the original data. Index-level
`COLLATE` clauses remain unsupported for rebuilds and fail before replacing the table.

## Consolidated migration history

A consolidated baseline can use `Database.MigrationApplied(version, scope)` to
record versions whose schema it already includes. The runner rechecks the active
scope's history before each planned migration and skips versions now applied,
including their `AfterUp` callbacks. The same rule applies to downgrades when an
earlier `Down` removes another version from history. Recording the baseline's own
version does not insert it twice. History for another scope does not skip a step
in the current scope. Transaction rollback still applies to baseline schema and
history changes according to the selected transaction mode.

## SQLite defaults and identity

### SQLite GUID defaults

SQLite now renders a CLR `Guid` default as a blob using `Guid.ToByteArray()`,
matching GUID parameters inserted by the provider. Previously a GUID default
was text, so a defaulted foreign-key value did not match an explicitly inserted
parent GUID even when both represented the same identifier.

This fixes new table/column definitions, including backfilling a new column.
Existing text GUID defaults and data are preserved during unrelated rebuilds;
column inspection retains their SQL as `RawSql` so storage classes are not
silently converted. Databases already containing mixed text/blob GUIDs require
an explicit data migration that converts related keys consistently. A string
default remains text; use a CLR `Guid` when authoring a GUID default.

### SQLite identity columns

SQLite requires the identity column and its single-column primary key in the
same table definition. Separate `AddColumn` and `AddPrimaryKey` calls create an
invalid intermediate definition. Use the SQLite provider's atomic rebuild API:

```csharp
var sqlite = (SQLiteTransformationProvider)Database;
var definition = sqlite.GetSQLiteTableInfo("Settings");
definition.Columns.Add(new Column("Id", DbType.Int32) { IsIdentity = true });
definition.ColumnMappings.Add(new MappingInfo { OldName = null, NewName = "Id" });
definition.PrimaryKey = new PrimaryKeyConstraint("PK_Settings", "Id");
sqlite.RecreateTable(definition);
```

`SQLiteTransformationProvider` is in `DotNetProjects.Migrator.Providers.Impl.SQLite`;
`MappingInfo` is in its `Models` namespace. Existing rows receive generated IDs.
This example assumes the table has no existing primary key or dependent foreign
keys requiring a separate migration plan.

## Identifier quoting and renamed tables

Use `QuoteColumnNameIfRequired` for columns in authored SQL and
`QuoteTableNameIfRequired` for tables. A table name may acquire a schema prefix;
using that API for a column can produce an invalid reference such as `dbo.Color`.

Renaming a table does not rename its explicitly named constraints or backing
indexes. On SQL Server and PostgreSQL, recreating the old table with the old
primary-key name can therefore collide with the renamed table's key. Give the
replacement table a distinct key name (for example `PK_Client_New`), or explicitly
rename the retained key using provider-specific SQL before reusing its name.

For PostgreSQL, create an ICU nondeterministic collation explicitly (for example
`CREATE COLLATION app_ci (provider=icu, locale='und-u-ks-level2', deterministic=false)`)
and use `Collation.Named("app_ci")`. The framework does not silently create shared
database objects while rendering a column or preview.

MySQL/MariaDB expose unique indexes as unique constraints in their catalogs, so
metadata cannot recover whether the original author used CREATE UNIQUE INDEX or
a UNIQUE table clause. No ownership decision may be inferred from that syntax.

## Oracle index options and constraint metadata

Oracle now rejects nonempty `Index.IncludeColumns` and `Index.Clustered = true` before
DDL. Version 12.1 silently ignored them. Remove these options for an ordinary Oracle
index or author an explicit Oracle-specific design; a SQL Server clustered-index
request is not translated to an Oracle index-organized table.

Structured metadata preserves SQL Server nonclustered primary keys and Oracle
ordered foreign-key pairs/delete actions. Foreign-key constructor arrays are copied,
matching primary/unique definitions, so later caller-array edits cannot change the key.

## Build and package identity

The core assembly and file versions are 13.0.0.0; generated assembly metadata preserves the existing title and description. Recompile consumers of the breaking API and update assembly/version binding assumptions. Keep the core, optional DI integration and CLI on compatible package versions.
