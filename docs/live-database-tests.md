# Live database tests

The pull-request workflow runs independent jobs on GitHub-hosted Ubuntu 22.04 with .NET 9. No external database, private download, repository secret, or paid runner is required. Database credentials in the scripts are disposable CI credentials.

## Tested versions

| Job | Pinned image / embedded driver | Client |
| --- | --- | --- |
| SQLite | embedded | Microsoft.Data.Sqlite 9.0.7; System.Data.SQLite.Core 1.0.119 |
| SQLServer | `mcr.microsoft.com/mssql/server:2019-CU32-ubuntu-20.04` | Microsoft.Data.SqlClient 6.1.0 |
| PostgreSQL | `postgres:13.23` | Npgsql 9.0.3 |
| Oracle | `gvenzl/oracle-free:23.9-slim-faststart` | Oracle.ManagedDataAccess.Core 23.9.1 |
| MySQL | `mysql:8.0.44` | MySql.Data 9.4.0 |
| MariaDB | `mariadb:11.4.10` | MySql.Data 9.4.0 |
| Firebird | `firebirdsql/firebird:5.0.3` | FirebirdSql.Data.FirebirdClient 10.3.4 |
| Db2 | `icr.io/db2_community/db2:11.5.9.0` | Net.IBM.Data.Db2-lnx 9.0.0.400 |
| Informix | `icr.io/informix/informix-developer-database:15.0.1.0.3` | Informix.Net.Core-lnx 4.1501.2.2026 |
| Sybase | `datagrip/sybase:16.0` (ASE developer image) | AdoNetCore.AseClient 0.19.2 |

The IBM Linux packages and ASE client are conditional test-project dependencies selected by `-p:LiveDatabase=Db2`, `Informix`, or `Sybase`. They do not become library dependencies. The library's provider identifiers and public API remain unchanged. Db2 and Informix containers need privileged mode. Image tags are fixed versions; container inspection artifacts record the actual downloaded image IDs.

## Coverage and isolation

`LiveDatabaseTests` adds ten scenarios each for MySQL, MariaDB, Firebird, Db2, Informix and Sybase: database/view catalogs; table/column metadata; persisted CRUD and defaults; column add/rename/type/nullability/default changes and removal; identity generation; primary-key enforcement/removal; foreign-key enforcement/removal; unique/check enforcement/removal; ordered composite index metadata/removal; and two complete migration up/down cycles with persisted version tracking.

Every test creates a uniquely named database (a schema for Db2, an independent server-side file for Firebird). Connections disable pooling. Teardown disposes the provider and drops that database/schema; Db2 removes tables in dependency order first. Migration cycles reuse the same isolated store to exercise repeatability even on engines whose DDL commits automatically. ASE test databases enable full logging for ALTER TABLE and allow DDL in transactions and allocate 32 MB of data plus a separate 16 MB log allocation to accommodate the image's model database.

Existing SQL Server, PostgreSQL, Oracle and SQLite suites continue to run in full. The Unit job uses the complement of all database categories. An audit compares NUnit's discovery count against the union of all job results and rejects missing or duplicate test assignments. Each job rejects zero executed tests; new suites also reject skips. Existing ignored tests retain their documented reasons: generic default removal (issue #139) and a SQL Server column-change regression (issue #132). TRX and NUnit XML expose each reason for review.

Readiness and startup are bounded; database jobs time out after 35 minutes. Startup logs, container logs/inspection, TRX and NUnit XML are uploaded on success or failure. Registry downloads may retry; test failures never do. A new commit cancels an obsolete run.

## Local reproduction

Run from the repository root. For Unit and SQLite, only the .NET SDK and PowerShell are needed:

```powershell
dotnet build Migrator.slnx
./.github/scripts/test.ps1 -Database Unit
./.github/scripts/test.ps1 -Database SQLite
```

For a server-backed suite, use Linux with Docker, .NET 9 and PowerShell (`pwsh`). Start one engine at a time because the script uses the name `migrator-db` and fixed host ports:

```bash
database=MySQL # or a server job name from the table
bash .github/scripts/start-database.sh "$database"
dotnet build Migrator.slnx -p:LiveDatabase="$database"
pwsh -File .github/scripts/test.ps1 -Database "$database"
docker logs migrator-db
docker rm -fv migrator-db
```

For Db2/Informix, install the native prerequisites and export driver paths before testing (the workflow contains the same setup):

```bash
sudo apt-get update
sudo apt-get install -y libaio1 libxml2 unixodbc libncurses5
output="$PWD/src/Migrator.Tests/bin/Debug/net9.0"
# Db2:
export DB2_CLI_DRIVER_INSTALL_PATH="$output/clidriver"
export LD_LIBRARY_PATH="$output/clidriver/lib"
# Informix, instead:
export DELIMIDENT=y
export INFORMIXDIR="$output/native"
export LD_LIBRARY_PATH="$output/native/lib:$output/native/lib/cli:$output/native/lib/esql"
```

New suites accept `MIGRATOR_MYSQL`, `MIGRATOR_MARIADB`, `MIGRATOR_FIREBIRD`, `MIGRATOR_DB2`, `MIGRATOR_INFORMIX`, or `MIGRATOR_SYBASE` connection-string overrides. Use disposable servers with administrative database/schema creation permissions. Defaults match the startup script. Existing suites read `appsettings.json` through ConfigurationReader, with a `MIGRATOR_` plus uppercased configuration-key override.

To reproduce the assignment audit, download all `test-results-*` artifacts from a single completed workflow into `TestResults`, preserving their per-database directories, then run:

```bash
python3 .github/scripts/verify-test-coverage.py TestResults
```

## Engine and provider limits

- MySQL/MariaDB DDL may commit automatically; the suite uses independent databases instead of relying on rollback. Modern pinned versions enforce CHECK constraints.
- Firebird identity columns require Firebird 3 or newer; this suite tests version 5. SQL cannot enumerate all server database files, so `GetDatabases` returns the attached database. Firebird has no general table rename operation.
- Db2 primary-key and unique-constraint columns must be NOT NULL. Column changes can require REORG, which the provider performs. Foreign-key updates are restrictive; supported delete actions are translated separately. `GetDatabases` returns the current server database, not a client catalog.
- Informix uses SERIAL/BIGSERIAL identity types and positional parameters. Declarative referential actions support restrictive behavior and cascading deletes; unsupported actions throw explicitly.
- ASE declarative foreign keys support restrictive behavior, without cascading actions. Default removal uses ASE's `REPLACE ... DEFAULT NULL` syntax.
- The new Firebird/Db2/Informix index implementations cover ordinary and unique indexes. Unsupported INCLUDE, filtered or clustered options throw rather than silently changing semantics; this is a provider limitation, not a claim that each engine lacks every such feature.

## Excluded candidate: Ingres

As checked on 2026-09-22, the official [Actian Ingres image](https://hub.docker.com/r/actian/ingres) (`ii12.1.0_p31093`) is distributed for commercial subscription use and its standalone startup requires a valid `license.xml` mounted at `/Secrets/license`. Accepting the license environment variable does not supply that key. Consequently we cannot validate a reproducible public server/driver pair under the no-new-secrets/no-private-downloads requirement. No Ingres tests are added or blanket-skipped; the existing provider is retained. Reconsider when a publicly runnable distribution and compatible driver are available. This exclusion is an installation/licensing blocker, not a provider implementation estimate.

Db2, Informix and Sybase are included rather than excluded for incomplete provider implementations: their public images and drivers are exercised by the workflow, and the tested catalog/DDL operations are implemented in this change.
