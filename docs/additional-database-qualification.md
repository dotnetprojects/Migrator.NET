# Additional database qualification

Reviewed 22 September 2026 against FluentMigrator's current
[runner projects](https://github.com/fluentmigrator/fluentmigrator/tree/main/src)
and [provider configuration](https://fluentmigrator.github.io/intro/configuration.html).
This is an implementation gate, not a claim of released provider support.

| Additional engine | Real-engine GitHub Actions route | Current disposition |
| --- | --- | --- |
| SAP HANA | Official HANA Express Linux container and SAP's .NET driver, disposable schema | Implemented in the v13 source stack with a mandatory actual-engine matrix job; require green PR checks before merge |
| Amazon Redshift | AWS test warehouse/serverless endpoint with CI credentials, network access and resource cleanup | No configured test infrastructure; defer provider |
| Snowflake | Snowflake test account, warehouse, credentials and disposable database/schema | No configured test infrastructure; defer provider |
| Db2 for IBM i | IBM i endpoint on Power infrastructure and compatible .NET/ODBC driver | No configured test infrastructure; defer provider |

The existing Db2 job runs Db2 LUW, not Db2 for IBM i. PostgreSQL compatibility does
not prove Redshift behavior. Snowpark's local test framework does not test Snowflake
DDL and catalog behavior through the production .NET driver.

Older provider lists also mention SQL Server Compact and SAP SQL Anywhere.
FluentMigrator's current FAQ marks these as dropped; they are not current additions
to pursue. Different SQL Server/PostgreSQL dialect versions and Oracle drivers are
not additional database engines.

## HANA admission criteria

The HANA matrix startup pins HANA Express 2.00.088.00.20251110.1 and
Sap.Data.Hana.Net.v8.0 2.30.27. It uses a disposable public CI credential, bounded
startup, native .NET connection, identity/constraint DDL, persisted data, rollback
and catalog checks. Any startup or behavioral failure fails the job. Logs and
runner resource evidence are retained; cleanup runs independently of test success.
The prerequisite probe passed in [run 35766200488](https://github.com/dotnetprojects/Migrator.NET/actions/runs/35766200488). The standalone workflow is replaced by the mandatory Hana job in the complete database matrix; the probe project remains reproducible evidence.

The provider matrix at source `eabec55` is recorded in [run 35770116342](https://github.com/dotnetprojects/Migrator.NET/actions/runs/35770116342).
Provider admission requires this matrix to pass, covering
imperative/fluent schema creation, constraint metadata, data, migration history,
restart/rollback, preview parity and explicit unsupported operations. Do not mark
a provider supported on the basis of SQL string tests or a skipped secret-gated job.

## Primary sources

- [SAP's official HANA Express image and installation requirements](https://hub.docker.com/r/saplabs/hanaexpress)
- [SAP Docker installation guide](https://developers.sap.com/tutorials/hxe-ua-install-using-docker)
- [Redshift Serverless setup](https://docs.aws.amazon.com/redshift/latest/gsg/)
- [Snowflake local testing framework scope](https://docs.snowflake.com/en/developer-guide/snowpark/python/testing-locally)
- [Db2 for IBM i platform](https://www.ibm.com/support/pages/db2-ibm-i)
- [FluentMigrator's current provider FAQ](https://fluentmigrator.github.io/intro/faq.html)

## HANA provider scope

The source provider is selected by `ProviderTypes.Hana` and accepts a caller-owned
`HanaConnection` or the SAP factory. The core remains free of SAP driver references.
The tests use Sap.Data.Hana.Net.v8.0 2.30.27 and HANA Express 2.00.088.00.20251110.1.

Supported operations include row/column table creation, explicit keys/checks/FKs,
column add/change/remove/rename, table rename/remove, ordinary and unique indexes,
parameterized data operations, schema/constraint/index metadata, versioned history,
and structured create-table/add-column preview. Names are case-preserving and accept
unquoted table or schema.table input; embedded identifier dots require explicit SQL.

Whole-session transactions and native locking are not advertised. The provider
retains HANA's default DDL autocommit behavior; data rollback does not prove DDL rollback.
Use explicit SQL for tenant administration, computed columns, specialized indexes,
collation configuration, and provider-specific data types without a mapped CLR type.
The provider rejects unsupported included/filtered/clustered indexes and semantic
collation requests instead of ignoring them. The packaged CLI does not bundle the SAP driver;
use the library runner in a host that references the SAP package. Raw defaults retain HANA's
engine restrictions: CURRENT_TIMESTAMP is valid, whereas arbitrary LOWER(...) defaults are not.

[HANA constraints](https://help.sap.com/docs/SAP_HANA_PLATFORM/4fe29514fd584807ac9f2a04f6754767/209f7cf5751910149d9ce6b033d8ddce.html),
[referential constraints](https://help.sap.com/docs/SAP_HANA_PLATFORM/4fe29514fd584807ac9f2a04f6754767/20ccc0a175191014901b88e6bc175c44.html),
and [DDL autocommit](https://help.sap.com/docs/SAP_HANA_PLATFORM/4fe29514fd584807ac9f2a04f6754767/d538d11053bd4f3f847ec5ce817a3d4c.html)
are documented by SAP.
