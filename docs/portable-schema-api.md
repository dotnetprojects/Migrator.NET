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
