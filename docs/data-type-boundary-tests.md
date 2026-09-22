# Data-type and boundary tests

`DataBoundaryTests` runs the same parameterized scenarios in the SQLite, SQL Server, PostgreSQL, Oracle, MySQL, MariaDB, Firebird, Db2, Informix, Sybase and HANA CI jobs. Each case uses the existing isolated database/schema or transaction setup. Server failures are failures, not skips or automatic evidence that a feature is unsupported.

## What the suite verifies

| Area | Assertions against stored data / the database |
| --- | --- |
| Every `MigratorDbType` enum value | Explicit supported/unsupported contract; supported types create a column, expose metadata and accept NULL (ASE BIT instead tests a non-null false value); unsupported types fail before creating a table |
| Signed integers and Byte | Minimum, maximum, negative values where applicable, zero, insert and update |
| SByte and unsigned integers | Supported ranges, including values above signed 16-/32-/64-bit maxima; explicit rejection for unsupported mappings; SQLite rejects UInt64 above Int64.MaxValue |
| Decimal and Currency | Positive/negative fractions down to 0.0001 and exact readback; decimal(12,4) limits, catalog precision/scale and overflow |
| Single and Double | Positive/negative fractional values and zero, with a specified tolerance |
| Variable strings | Requested capacities 1, 32, 255, 256, 2000 and 4000 for String; 1, 255, 256 and 2000 for AnsiString; exact content, NULL and update to a shorter value |
| Fixed strings | Both StringFixedLength and AnsiStringFixedLength at 1, 32 and 255 characters, exact full-length content and catalog length |
| Large text | `int.MaxValue` selects large-object storage; insert and read back more than 70,000 characters, including a distinctive suffix |
| String edge cases | Exact limit and limit+1, NULL, empty string, whitespace, trailing spaces, quotes, SQL punctuation, backslash/newline and accented Latin-1 text |
| Binary | Default, bounded, 8001 and maximum size mappings; zeros, high bytes, insert/update, NULL and a 70,000-byte payload |
| Boolean | False, true and update back to false |
| Dates and timestamps | Leap day and year boundary; timestamps include 23:59:59 |
| Time | Midnight, ordinary time and 23:59:59 |
| Schema changes | Widen a populated string, retain NULL and existing content, write at the new limit, rename and verify content again |
| Defaults and nullability | Omitted value vs explicit NULL vs zero, change a default without rewriting existing data, reject NULL in a required column |

The separate `DataTypeBoundaryTests` and `DialectCapacityRegressionTests` run without servers. They cover every enum value for all eleven dialects, decimal rendering, large-text fallback and storage transitions such as SQL Server binary 8000/8001, HANA text 5000/5001, Db2 32672/32673 and Informix 32739/32740. These rendering checks complement live tests; they do not prove that a server accepted the SQL.

## Explicit type support

The test contract is maintained independently of the production mapping in `DataTypeContract`. A new enum value fails until the contract is updated deliberately.

| Types | Supported mappings in this matrix |
| --- | --- |
| AnsiString, Binary, Byte, Boolean, Currency, Date, DateTime, Decimal, Double, Int16, Int32, Int64, Single, String, Time, AnsiStringFixedLength, StringFixedLength | All eleven |
| Guid, DateTimeOffset | All except HANA |
| DateTime2 | All except Firebird |
| SByte | SQLite |
| UInt16, UInt32, UInt64 | SQLite, SQL Server, PostgreSQL, Oracle, MySQL, MariaDB |
| VarNumeric | SQLite, SQL Server, Db2 |
| Interval | SQLite, SQL Server, PostgreSQL, Oracle, MySQL, MariaDB |
| Object, Xml, Json | Rejected by the eleven selected dialects; historical SqlServer2005 XML support is outside this matrix |

This table describes schema mapping support, not identical native storage or full value fidelity. In particular, the new per-type schema test does not by itself establish Guid, DateTimeOffset, Interval or VarNumeric value round trips. Existing interval tests remain in place. Exhaustive offset/precision, GUID-format and variable-numeric boundaries need additional provider-specific qualification.

## Engine semantics and remaining limits

- SQLite ignores string length and decimal precision constraints. The tests assert preservation of over-length values instead of inventing server enforcement. Its INTEGER storage is signed 64-bit; binding an out-of-range UInt64 now throws rather than allowing driver conversion to corrupt the value.
- Oracle treats empty character strings as NULL. ASE represents an empty varchar as a single space and disallows nullable BIT columns. The tests assert those explicit behaviors.
- Informix reserves the lowest signed integer value for NULL, truncates over-length VARCHAR assignments, and trims trailing spaces on readback. ASE also trims trailing spaces. Tests verify these native contracts explicitly.
- Firebird decimal precision describes a minimum capacity: DECIMAL(12,4) uses a scaled 64-bit integer. The suite checks its actual storage boundary rather than expecting overflow at twelve digits.
- MySQL/MariaDB tests set `STRICT_ALL_TABLES`; ASE tests enable `STRING_RTRUNCATION` and raise `TEXTSIZE` for large-object readback. Length enforcement depends on these session settings.
- Large-column declarations are tested with bounded allocations, not multi-gigabyte payloads. Engine row-size limits and every native length transition are not exhaustively live-tested.
- Accented Latin-1 text is covered across the legacy CI encodings. Supplementary Unicode characters, combining-sequence normalization, collations, embedded NUL in text, DST/timezone offsets, NaN/infinity and concurrent transactions remain separate qualification work.
- Ingres and historical provider aliases are outside the eleven-engine CI matrix; see [live database tests](live-database-tests.md).

## Reproduction and validation

After building, the existing commands include these tests automatically:

```powershell
./.github/scripts/test.ps1 -Database Unit -Coverage
./.github/scripts/test.ps1 -Database SQLite -Coverage
```

For one new live suite on a configured server:

```powershell
dotnet test src/Migrator.Tests/Migrator.Tests.csproj --no-build --filter "FullyQualifiedName~DataBoundaryTests&TestCategory=MySQL"
```

The implementation was exercised locally against SQLite and the unit suite. The ten server engines require the GitHub Actions run; the presence of a test here is not a claim that those runs have already passed. Coverage comparisons must use the same selected suites: a Unit+SQLite number is not whole-matrix coverage.

Local validation on 2026-09-23: 823 Unit+SQLite cases passed together, followed by the additional PostgreSQL driver-binding regression (824 passing cases in total). The new live fixture contributes 75 cases per engine; the separate dialect/driver suite contributes 371 cases. Comparing Unit+SQLite with the earlier local Coverlet report, line coverage rose from 4,356/10,049 (43.35%) to 4,529/10,078 (44.94%); branch coverage rose from 1,864/4,934 (37.78%) to 1,946/4,958 (39.25%). The latter coverage run precedes the additional driver-binding test. The denominator changes include the provider fixes.

## Corrections exposed by these tests

- MySQL/MariaDB: ANSI length 256 is retained; maximum ANSI text maps to LONGTEXT; Currency uses DECIMAL(19,4); explicit decimal precision and scale are honored. [MySQL numeric types](https://dev.mysql.com/doc/refman/8.0/en/precision-math-numbers.html).
- Oracle: new Currency columns use NUMBER(19,4) instead of rounding to one fractional digit.
- SQL Server: binary lengths above 8000 use VARBINARY(max).
- Firebird: bounded binary columns use VARCHAR(n) CHARACTER SET OCTETS; metadata recognizes OCTETS as binary. ANSI variable strings remain variable-length, maximum ANSI text uses a text BLOB, and fixed Unicode strings retain the requested length. [Firebird character and binary types](https://firebirdsql.org/file/documentation/chunk/en/refdocs/fblangref50/fblangref50-datatypes-chartypes.html).
- Shared parameter binding accepts Single and SByte. PostgreSQL binds UInt64 as Decimal, matching its NUMERIC(20,0) mapping; SQLite checks its signed integer limit.

Mapping changes affect newly generated DDL; they do not alter existing tables automatically. Applications relying on previous implicit truncation or rounding should review their migrations.

## CI regression fixes

The first full matrix exposed additional regressions: PostgreSQL fixed-character metadata and non-UTC timestamp binding, Oracle character metadata and Single storage, SQL Server numeric/fixed-character metadata, Db2 Byte binding, Informix large-text scalar readback, and untyped NULL parameters for binary columns. The fixes include NULL updates through both update overloads and native Oracle BINARY_FLOAT storage. Local Unit+SQLite validation after these changes passes 855 tests.

VSTest can publish byte-identical coverage attachments at multiple paths. CI now validates and normalizes these into one report per job, while still rejecting missing, empty, or conflicting reports. Five Python regression tests cover report handling and coverage comparison.
