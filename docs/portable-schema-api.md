# Portable schema expressions and triggers

Import `DotNetProjects.Migrator.Framework` and use `SqlExpression` to compose
server expressions. Identifiers use the provider's quoting rules; explicitly
delimit mixed-case PostgreSQL names. `Raw` is a trusted, provider-specific SQL
escape hatch, never an input sanitization API. Literals are rendered for DDL,
where bind parameters are not permitted.

```csharp
using static DotNetProjects.Migrator.Framework.SqlExpression;

var finalized = Exists("Parent", "p",
    Column("Id", "p").EqualTo(Old("ParentId"))
        .And(Column("Finalized", "p").EqualTo(Value(1))));
Database.CreateGuardTrigger(new GuardTrigger("Guard_Child_Update", "Child",
    TriggerEvent.Update, "Finalized parent", finalized, KeyColumns: ["Id"]));
Database.DropGuardTrigger("Guard_Child_Update", "Child");
```

The fluent equivalents are `builder.Create.GuardTrigger(definition)` and
`builder.Delete.Trigger(name, table)`. They support SQL preview and explicit
reverse operations. Create fails if the name already exists; drop fails if it
does not exist. Each definition has exactly one Insert, Update or Delete event.
Omitting Condition rejects all affected rows. Old is unavailable on INSERT;
New is unavailable on DELETE. Combine OLD and NEW parent predicates to protect
both ends of reparenting. `ChangedFrom` compares SQL NULL safely.

SQLite (including MonoSQLite), PostgreSQL (including the legacy dialect),
SQL Server 2012+ and MySQL/MariaDB have guard implementations. SQLite uses
RAISE(ABORT), PostgreSQL raises an exception, SQL Server uses THROW and MySQL
uses SIGNAL. An invalid row rejects the statement, including its trigger side
effects. SQL Server errors can invalidate the containing transaction; roll it
back before continuing. MySQL requires transactional tables (InnoDB).

SQL Server UPDATE triggers require KeyColumns naming a non-null unique key.
Assignments to these columns are rejected, even if the value is unchanged,
because joining inserted/deleted otherwise cannot reliably pair old and new
rows. Multi-row statements are evaluated with set-based EXISTS, never scalar
variables. UpdateColumns means column assignment (including an unchanged
value), and is supported on SQLite, PostgreSQL and SQL Server. MySQL rejects
this option; use an OLD/NEW change predicate there.

Oracle, DB2, Firebird, HANA, Informix and Sybase also have guard implementations,
with live fixtures for multi-row rejection, valid writes, reparenting and named
drop/recreate. Oracle uses raise_application_error, DB2 SIGNAL, HANA SIGNAL
SQL_ERROR_CODE, Informix an SPL error procedure, Firebird a named exception,
and Sybase ROLLBACK TRIGGER WITH RAISERROR. Firebird/Informix helper objects use
the trigger name plus `_error`/`_fn` and are removed by DropGuardTrigger. Firebird
helper DDL is transactional. Oracle and HANA retain their native DDL commit
behavior. Oracle predicates may query related tables but must respect Oracle's
mutating-table restrictions. Sybase uses set-based OLD/NEW pairing with the same
immutable key requirement as SQL Server. Firebird/HANA cannot detect assignments
to unchanged columns and reject UpdateColumns explicitly.

`GuardTriggerProviderTests` runs the same five behavioral cases against all
eleven implementations through both direct SQL and the database-independent
Insert/Update/Delete/Select APIs: write rejection, statement atomicity for bulk
updates/deletes, null-safe changes and reparenting. Projected INSERT tests also
cover multi-row insert atomicity once that API is available. "Portable" refers to the shared C# definition;
the library renders native trigger SQL for each database. It is not an arbitrary
trigger-body translator. HANA guard creation reads native column types from its
catalog for OLD/NEW scalar bindings; offline preview of predicates with these
bindings is currently unavailable and throws `NotSupportedException`.

Domain messages must be representable by the database and connection character
sets. The Informix and ASE CI images use legacy single-byte locales, so their
shared guard cases use ASCII messages; the other nine fixtures also assert a
Japanese message round trip. Apostrophes and backslashes are tested on all eleven.

Ingres supports rules and error-raising procedures but is not implemented by
this API yet; it throws NotSupportedException. This is a library implementation
gap, not a claim that Ingres lacks database-enforced write guards.

MySQL servers with binary logging require sufficient trigger-creation privileges
or an administrator-configured log_bin_trust_function_creators setting. The CI
container enables that setting only on its disposable test server.

Guard predicates do not serialize concurrent transactions. Cross-row uniqueness
invariants also require a database UNIQUE constraint or suitable isolation and
locking. A predicate tested against a serial fixture is not a concurrency lock.
PostgreSQL trigger functions use the trigger's name plus `_fn` in the table's
schema; reserve that name and drop through this API to remove both objects.

Syntax references: [SQLite triggers](https://www.sqlite.org/lang_createtrigger.html),
[PostgreSQL triggers](https://www.postgresql.org/docs/current/sql-createtrigger.html),
[SQL Server multi-row triggers](https://learn.microsoft.com/en-us/sql/relational-databases/triggers/create-dml-triggers-to-handle-multiple-rows-of-data),
[MySQL SIGNAL](https://dev.mysql.com/doc/refman/8.0/en/signal.html).

## Audit triggers

`CreateAuditTrigger(AuditTrigger)` and `builder.Create.AuditTrigger(definition)`
write a row to an existing audit table after each affected source row. Supply
an EntityKey expression (OLD on delete, NEW on insert, either on update) and
BeforeFields with stable JSON keys. INSERT stores SQL NULL as BeforeJson;
UPDATE and DELETE use the explicitly supplied previous-row expressions.
`DropAuditTrigger(name, table)` removes the rule and its PostgreSQL function.

The default target columns are OccurredAt, EntityId, Operation, BeforeJson;
all can be renamed. JsonObject preserves SQL NULL as JSON null and uses native
database value types. SQLite stores booleans as integers, so its JSON contains
numbers for such fields. BLOBs must be omitted or explicitly converted using
Raw; there is no implicit portable binary JSON encoding. SQL Server JSON PATH
interprets dots in field keys as paths; use simple keys on that provider.

Condition supports change-only auditing (`Old("Label").ChangedFrom(New("Label"))`)
and related-row predicates. UpdateColumns instead selects assignments, including
same-value assignments, with the same provider restrictions as guards. The
trigger runs in the writer's transaction; rollback removes source changes and
audit events. Multi-row writes produce one snapshot per matching source row.
SQL Server uses set-based inserted/deleted pairing and the same immutable key
requirement as guards. Audit JSON requires SQL Server 2016+ and PostgreSQL 9.4+.

RecordedAt defaults to database UTC: SQLite text with three fractional digits,
PostgreSQL timestamp without time zone with microsecond precision, SQL Server
datetime2 with seven fractional digits, MySQL/MariaDB UTC_TIMESTAMP(6) with six.
Use a target column that preserves this precision. Alternatively supply a
RecordedAt expression for a source timestamp; its value is copied unchanged.
These are wall-clock timestamps, not a unique event sequence. No application
clock is consulted. Unsupported providers fail before execution.

On SQLite, a suppressed audit INSERT (for example, an outer INSERT OR IGNORE
meeting an audit-table unique constraint) aborts the source statement. Audit
tables must permit a separate row for every event. Avoid REPLACE conflict modes
that can replace earlier audit rows when their keys conflict.

## Filtered views

`SelectQuery` declares a source table/alias, an ordered list of `SelectItem`
expressions with explicit output aliases, optional inner/left `QueryJoin`s,
and a predicate. The expression model supports correlated EXISTS/NOT EXISTS,
column comparisons and grouped Boolean conditions. SQL NULL and native
database comparison semantics apply. Input arrays are copied at construction.

Use `CreateView(name, query)`, `ReplaceView(name, query)` and
`DropView(name, ifExists: true)`. Create fails if the view exists; ordinary
drop fails if absent. Conditional drop uses a catalog lookup and never catches
a deliberate missing-object error. There is a race if another session changes
the same view concurrently; serialize schema migrations.

The fluent API is `builder.Create.View(name, query, replace: false)` and
`builder.Delete.View(name, ifExists: false)`. Standard SELECT, alias, inner/left
join and predicate rendering works for every provider. Native replace is used
on PostgreSQL, Oracle, MySQL/MariaDB, DB2, Sybase ASE 16+ and HANA; SQL Server uses ALTER
for existing views; Firebird uses CREATE OR ALTER. SQLite uses a savepoint and
validates the replacement by preparing a read, preserving the old definition
on failure. Ingres/Informix use transactional drop/create: if already inside a
migration transaction the caller must roll back that transaction on failure.
DDL-committing engines retain their native transaction limitations.

Object paths and identifier atoms remain distinct: delimit table/view paths
explicitly where case matters, and supply output aliases as literal names.
Output aliases are always quoted. Native replacement may restrict changes to
the public column signature; use explicit drop/create when changing it.

## Portable CHECK expressions

`CheckConstraint.FromExpression(name, expression)` works in CREATE TABLE. The
existing string constructor and fluent string overloads remain unambiguous for
`null` arguments; portable fluent expressions use inferred generic overloads. Use
`provider.AddCheckConstraint(name, table, expression)` to add to an existing
table (SQLite performs its existing transactional table rebuild). Fluent table
`WithCheckConstraint` and `Create.CheckConstraint(...).OnTable(...).WithExpression`
also accept expressions. Grouped AND/OR, null checks and column comparisons use
the same expression model as guards/views.

`Column("Payload").IsNotNull().And(Column("Payload").BinaryLength().GreaterThan(Value(0)))`
rejects SQL NULL and zero-length binary, while accepting a zero byte. BinaryLength
supports SQLite, PostgreSQL, SQL Server, MySQL/MariaDB, DB2, Firebird and HANA.
Oracle LOBs, Informix BYTE, ASE IMAGE and Ingres have no guaranteed CHECK-safe
implementation here and throw before DDL rather than assuming character length.
ASE retains `IMAGE` for binary byte preservation: its `VARBINARY` storage strips
trailing zero bytes and cannot faithfully represent empty binary values. ASE
forbids `IMAGE` references in CHECK constraints, so binary-length CHECK rendering
fails explicitly rather than changing storage to a lossy type.

`Column("Payload").IsJson(JsonShape.Array)` rejects malformed JSON, nonarrays,
JSON null and SQL NULL. `IsJson(allowNull: true)` accepts SQL NULL and every
valid JSON value including scalars. JsonShape.Object restricts to an object.
SQLite JSON1 and MySQL/MariaDB use a CASE around shape parsing so malformed
input cannot slip through UNKNOWN. PostgreSQL uses its JSON parser (9.3+);
malformed input raises a parser error and rejects the write. Native json/jsonb
columns still get shape checks. SQL Server array/object rules work on 2016+;
Any JSON value (including scalars) requires ISJSON VALUE from SQL Server 2022+.
Oracle requires 21c+ with compatible >=20 for scalar JSON; shape uses the JSON
type() item method. DB2, Firebird, Informix, Ingres, HANA and Sybase JSON checks
are explicitly unsupported. Server versions are requirements, not silently
detected fallbacks. Use transactional tables and enforced CHECK support
(MySQL 8.0.16+). Oracle treats empty text as SQL NULL.

CHECK accepts UNKNOWN on most engines. Combine IsNotNull explicitly with
comparisons when NULL must be rejected. Trim/TextLength support nonblank reason
rules; TextLength follows database character and trailing-space semantics and
must not be used for binary length.

References: [SQLite JSON](https://www.sqlite.org/json1.html),
[PostgreSQL JSON](https://www.postgresql.org/docs/13/functions-json.html),
[SQL Server ISJSON](https://learn.microsoft.com/en-us/sql/t-sql/functions/isjson-transact-sql).
Oracle 18c+ and DB2 11.5+ also render per-row audit triggers with native
JSON_OBJECT and explicit NULL ON NULL. Oracle uses SYS_EXTRACT_UTC(SYSTIMESTAMP),
DB2 uses CURRENT TIMESTAMP minus CURRENT TIMEZONE (microsecond precision).
Use native timestamp target columns to retain clock precision. Firebird, HANA,
Informix, Sybase and Ingres explicitly reject this audit convenience API until
their transactional JSON serialization is implemented; guard support is separate.

## Projected data migrations

`InsertFromQuery(target, targetColumns, query)` executes one INSERT ... SELECT
in the current migration transaction, with an explicit ordered target mapping.
The fluent queue is `builder.InsertFromQuery(target, targetColumns, query)`.
Projection count and duplicate target columns are validated before execution.
Joins, predicates, CASE, explicit NULL/constants and binary columns remain on
the server. `SqlExpression.Parameter(value)` binds values through the existing
provider parameter conversion and timeout handling; repeated occurrences bind
in SQL order, including positional drivers. Parameters are not available in
view or trigger DDL. Value emits a DDL literal; Raw remains trusted native SQL.

`SelectQuery.Ordered(table, alias, columns, orderBy, limit: ...)` supports ordered
seed selection with correlated NOT EXISTS. The original constructor retains its
optional predicate and joins, including an explicit null fourth argument.
Firebird string parameters use UTF-8 VARCHAR up to 8191 characters and text BLOB
casts above that bound, preserving long CASE values without a narrowing fallback.
Limit uses native TOP/FIRST/LIMIT/FETCH FIRST syntax. Constant-only queries use
the provider's single-row source. Coalesce/Max support aggregate seed inserts,
and Scalar embeds a scalar query. RowNumber supports explicit partition keys
and ordering; include a unique tie-breaker such as CreatedAt plus Id. Legacy
SQLite rowid ordering can be requested explicitly through Raw. Sybase ASE and
Ingres window numbering fail explicitly; use native SQL or materialized values.
Modern window-capable engine versions are required (SQLite 3.25+, MySQL 8+,
Firebird 3+, PostgreSQL 8.4+). Ordered results do not promise physical insert
order. This API inserts projections; general query-based UPDATE is not included.
