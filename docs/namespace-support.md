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
| Ingres | Owner-qualified DDL and native table/view/column/constraint/index-existence catalogs; the legacy provider still lacks structured index/FK metadata and live qualification infrastructure |

Renaming keeps the source namespace when the new name is unqualified. It is not
a portable API for moving tables between namespaces. SQLite foreign keys cannot
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
- Ingres has no engine/driver job in this repository. Its command contracts do
  not certify full live provider support. Firebird 6 schema support likewise
  requires separate version qualification.

Related issue: [#48](https://github.com/dotnetprojects/Migrator.NET/issues/48).
This matrix describes the tests added here, not a claim that every possible
identifier, database version or schema-changing operation is covered.
