# Namespace qualification

A table argument can be unqualified (Orders) or qualified (sales.Orders).
ProviderFactory.Create with defaultSchema applies the namespace to unqualified
names only. Explicit qualification wins. Quote each identifier component
separately when it contains dots, spaces or reserved words.

GetTables() enumerates the configured namespace, or the connection's current
namespace when none is configured. GetTables(schema) explicitly selects one
namespace. GetColumns(schema, table) uses the same native catalog path as
GetColumns(qualifiedTable), rather than assuming an ADO.NET driver's GetSchema
restriction layout. Table enumeration returns local names.

| Provider | Namespace model |
| --- | --- |
| SQL Server / SQL Server 2005 | Database schema; metadata follows qualified object identity |
| PostgreSQL / PostgreSQL 8.2 selector | Database schema; unqualified lookup follows the search path |
| Oracle / MsOracle | Schema owner; unquoted names fold to uppercase |
| Db2 | Schema; unquoted names fold to uppercase |
| Informix | Object owner; unquoted names fold to lowercase |
| Sybase ASE | Object owner; enable QUOTED_IDENTIFIER for quoted SQL identifiers |
| HANA | Database schema; preserves the provider's case-sensitive quoted-name convention |
| MySQL / MariaDB | Database (SCHEMA is a synonym for DATABASE) |
| SQLite / MonoSQLite | main, temp, or a connection-local ATTACH alias |
| Firebird | The provider targets Firebird 5: namespaces are explicitly rejected, including configured defaults |
| Ingres | Owner-qualified DDL and native table/view/column/constraint/index catalogs, including composite foreign keys and cross-owner references |

Renaming keeps the source namespace when the new name is unqualified. It is not
a portable API for moving tables between namespaces. ASE index removal and sp_rename require
a connection in the object's owner namespace; an explicitly different owner is
rejected before renaming. SQLite foreign keys cannot
reference another attached database; that request fails before table creation.
SQLite reconstruction keeps its temporary table, indexes, triggers and
AUTOINCREMENT sequence in the original database and checks foreign keys across
attached databases.

## Tests and evidence

- NamespaceProviderContractTests exercises every public provider selector,
  including aliases and Ingres, with unqualified, qualified and default namespace
  DDL, table/view lookup and enumeration. These are command contracts, not proof
  of execution on an engine.
- NamespaceLifecycleTests runs in all eleven existing database CI jobs. Each
  engine gets unqualified, qualified, quoted-qualified and default-namespace
  cases covering table creation/removal, enumeration, columns/defaults, inserts,
  updates, views, column changes/renames/removal, PK/FK/unique/index metadata and
  removal, table rename and data preservation. Firebird tests explicit rejection
  for the three namespace modes and table rename (unsupported by Firebird 5).
- NamespaceIsolationTests creates two namespaces with identical table, key
  and index names on SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, Db2 and HANA.
  The provider default points at the second while changes target the first.
- SQLiteNamespaceTests exercises both selectors with dotted ATTACH names,
  reconstruction, triggers, index isolation, sequence high-water preservation,
  qualified renames and rejection of cross-database foreign keys.
- IngresMetadataTests executes the actual catalog SELECTs against SQLite tables
  shaped like the documented Ingres catalogs. It tests owner isolation, composite
  key order, constraint-backed versus ordinary unique indexes, included columns,
  split CHECK/FK definition text, referential actions and quoted cross-owner names.
  DDL contracts cover index creation/removal and restrictive column/constraint
  drops. This is executable catalog coverage, not an Ingres engine simulation.
- The Ingres lifecycle fixture can be enabled for a licensed instance as described
  below. No live Ingres run has been performed. Firebird 6 schema support likewise
  requires separate version qualification.

## Running the Ingres lifecycle suite

The [official Ingres image](https://hub.docker.com/r/actian/ingres) requires a
commercial license and license key. It is not added to the public CI matrix.
Use a licensed test installation with an empty, disposable owner namespace and
a current Actian .NET driver compatible with .NET 9. Set MIGRATOR_INGRES_DRIVER
to the driver's assembly path and MIGRATOR_INGRES to its connection string, then:

```powershell
dotnet test src/Migrator.Tests/Migrator.Tests.csproj -p:LiveDatabase=Ingres --filter 'TestCategory=Ingres'
```

This enables the four existing lifecycle cases (unqualified, qualified,
quoted-qualified and default namespace). Missing configuration fails explicitly;
the suite does not turn missing connectivity into passing or skipped tests.
Setup refuses a nonempty owner namespace and each case rolls back its DDL.
The optional fixture is not compiled into normal CI discovery.

The implementation uses Actian's documented [standard catalogs](https://docs.actian.com/ingres/11.0/DatabaseAdmin/Standard_Catalogs_for_All_Databases.htm).
Ingres secondary indexes support key and non-key columns; clustered and filtered
index requests are rejected explicitly. DROP COLUMN and DROP CONSTRAINT use
RESTRICT, so dependent objects must be removed by the caller first. Renaming is
also subject to the engine's dependency restrictions.

Related issue: [#48](https://github.com/dotnetprojects/Migrator.NET/issues/48).
This matrix describes the tests added here, not a claim that every possible
identifier, database version or schema-changing operation is covered.
