# Migrating from 12.1 to 13

Version 13 is a breaking release. This guide is maintained alongside the implementation; items explicitly marked planned are not available yet. Do not run a changed migration history against production without validating the upgrade on a restored database.

## Schema model (implementation in progress)

Columns describe data type, length, precision/scale, nullability, identity generation and defaults. Primary keys, unique constraints, foreign keys and checks belong to the table. Indexes are separate schema objects: a unique index is not automatically a unique constraint.

The v13 target API accepts complete named constraint definitions when creating a table, and returns the same kinds of definitions from metadata inspection. Key column order is significant. A composite UNIQUE constraint must never mark each member column as individually unique. Altering a column must not infer that its table constraints should be removed.

Planned removal: ColumnProperty.PrimaryKey, PrimaryKeyNonClustered, PrimaryKeyWithIdentity, Unique and Indexed; unnamed fluent PrimaryKey()/Unique() shortcuts; name-based ownership inference. The replacement examples and exact supported-provider behavior are added with the corresponding implementation commits below.

## Implemented breaking changes

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

The supplied key order is preserved. Explicit primary-key definitions make their columns non-nullable without mutating the caller's column objects. This also rejects NULL in a composite SQLite primary key; old flag-based composite SQLite keys allowed NULL. Do not combine a constraint object with legacy primary-key flags. SQLite identity requires a single INTEGER primary key and rejects incompatible combinations instead of silently removing identity.

Fluent equivalent: append `.WithPrimaryKey("PK_Users", "TenantId", "Id")`, `.WithUniqueConstraint("UQ_Users_Email", "TenantId", "Email")`, or `.WithCheckConstraint("CK_Users_Id", "Id > 0")` to the table builder. Each is part of the complete table definition.

### Structured constraint inspection

Use `Database.GetTableConstraints("Users")`, or `Schema.Table("Users").ConstraintDefinitions()`, then select `PrimaryKeyConstraint`, `UniqueConstraint`, `ForeignKeyConstraint` or `CheckConstraint`. Key column order belongs to the constraint. A unique index stays in index metadata. SQLite returns `Name == null` for unnamed legacy constraints; a backing autoindex name is not an invented constraint name.

Initial structured readers cover SQLite, SQL Server, PostgreSQL, Oracle, MySQL and MariaDB. Other readers explicitly throw `NotSupportedException` until implemented. MySQL identifies the primary key as `PRIMARY` regardless of a supplied symbolic name. Quoted qualified Oracle/MySQL lookups are currently rejected explicitly. These limitations must not be interpreted as empty metadata.

### Custom provider and dialect implementations

`ITransformationProvider` now requires `GetTableConstraints(string)`. Return accurate typed definitions, including ordered key members, or throw `NotSupportedException`; do not return an empty array for an unsupported reader. `IDialect` adds `QuoteIdentifier(string)` for one identifier atom and `GetTableConstraintSql(TableConstraint)` for pure SQL rendering. Implementations derived from `Dialect` inherit defaults. Constraint names containing quote delimiters are escaped; a dot within a constraint name is not a schema separator.

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
