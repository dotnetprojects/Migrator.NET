# Migrating from 12.1 to 13

Version 13 is a breaking release. This guide is maintained alongside the implementation; items explicitly marked planned are not available yet. Do not run a changed migration history against production without validating the upgrade on a restored database.

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

Keep SQL generation independent of a live connection. A dialect defines identifier quoting, type/literal rendering and SQL capabilities. Metadata readers inspect existing schema; execution manages commands, transactions and history. Neither preview nor a SQL generator may query or mutate the database.

The current provider surface mixes these concerns. The v13 implementation is staged to preserve testable provider behavior while replacing authoring APIs. Unsupported combinations must fail explicitly before DDL, not disappear from generated SQL.

## Design references

Reviewed 2026-09-22:

- [EF Core CreateTableOperation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.operations.createtableoperation?view=efcore-10.0) separates columns, primary key, unique constraints, checks and foreign keys.
- [FluentMigrator ColumnDefinition](https://github.com/fluentmigrator/fluentmigrator/blob/main/src/FluentMigrator.Abstractions/Model/ColumnDefinition.cs) still carries constraint flags; its expression/generator separation is useful, but its column model is not the target here.
- [Alembic operations](https://alembic.sqlalchemy.org/en/latest/ops.html) distinguish named table constraints from column alteration and use explicit batch reconstruction for SQLite.

## Additional v13 candidates

Evaluate typed schema-qualified identifiers, explicit literal versus SQL-expression defaults, ordered constraint metadata, deterministic constraint naming, SQLite constraint parsing without regular-expression guesses, typed provider capabilities, and removal of obsolete duplicate authoring APIs. These are candidates, not claims of implemented functionality.
