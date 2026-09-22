# Code cleanup review â€” 2026-09-22

Reviewed from freshly fetched `origin/master` (`ed162ba`). This is a focused source and build review, not certification of every database provider.

## Changes in this cleanup

- **Factory resolution:** stop swallowing exceptions thrown by registered factory delegates. Missing registrations still fall through to the system registry and reflection; actual initialization failures retain their original exception. Use a concurrent registry for registration, lookup and provider-name enumeration.
- **Migration names:** preserve the first character of lowercase names and handle empty or numeric-only names without substring exceptions. These names are used in loader tracing and migration logging.
- **SQL type names:** compare SQL names independently of the current culture, avoid repeated enumeration, and expand length/precision/scale placeholders when falling back to the default type mapping.
- **Duplication:** share loader trace output, delegate the `DbType` registration overloads to the `MigratorDbType` implementation, and use a hash set for duplicate migration-version detection.
- **Unnecessary reflection:** instantiate known dialect classes directly. The existing null result for unknown provider values remains unchanged.
- **Dead code:** remove three files containing only commented-out test code: the old MySQL provider fixture, SQLite PRAGMA fixture and schema-dumper fixture. None contributed executable tests.
- **Small hygiene fixes:** remove a duplicate MySQL import, dispose the generic test provider and directly index the built fluent-operation list.

## Follow-up changes

- **Provider default parsing:** moved SQL Server, Oracle and PostgreSQL default interpretation into separate internal parsers. Shared text classification, numeric conversions and hexadecimal decoding remove duplicated conversion logic while retaining dialect-specific date, interval, cast and GUID handling. Removed unreachable string branches and PostgreSQL's empty primary-key branch.
- **Parser corrections:** numeric SQL `NULL` defaults remain null; malformed odd-length binary literals now fail instead of silently losing their last digit. PostgreSQL numeric literals no longer depend on whether their text contains the table name; SQL expressions still pass through as `RawSql`.
- **Fixture inheritance:** removed an empty SQLite fixture with a hidden setup method; renamed the table-plus-primary-key helper so it no longer hides the base helper; removed SQL Server's identical copy of the inherited compound-primary-key test.
- **Duplicate scenarios:** retained one identity/primary-key metadata test and removed two identical copies in the generic fixture. This removes two repeated cases from each of four derived provider suites. No distinct assertions were removed.
- **CLI structure:** extracted typed options and command handlers from the entry point. Driver names and factories now use one mapping. Options are validated before assembly loading or opening a database, including options passed to offline/list commands. Exit codes and credential-safe errors remain covered.
- **Warning noise:** deliberate calls to three obsolete APIs now go through the test-only `LegacyMetadata` helper, with suppression scoped to those calls. Production obsolete-API warnings and complexity diagnostics remain enabled.

## Validation and remaining limits

- Solution rebuild passes. The final build retains production complexity and compatibility warnings; the CLI complexity and hidden-test-member warnings are gone.
- Unit suite: 175 passed, zero skipped, including 53 catalog parser cases and CLI argument/filter/offline-output coverage.
- SQLite suite: 203 passed, zero skipped. The previous 205 included two duplicate generic cases.
- Parser cases check CLR types as well as values under a non-English culture, SQL expressions, nulls, SQL Server datetime conversion, Oracle GUID byte order, PostgreSQL casts/timezones/intervals, and invalid binary input.
- SQL Server, PostgreSQL and Oracle catalog queries still need their live CI suites after this refactor. Docker is unavailable locally, so no server-backed matrix result is claimed. Test discovery/count reporting is dynamic and does not require hardcoded count updates.
- The provider catalog/type-mapping methods remain substantial; this change separates and tests default parsing without rewriting their database queries. Other existing complexity warnings in the migration runner and SQLite reconstruction/parser code remain future cleanup work.
