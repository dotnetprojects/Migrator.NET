# Additional database qualification

Reviewed 22 September 2026 against FluentMigrator's current
[runner projects](https://github.com/fluentmigrator/fluentmigrator/tree/main/src)
and [provider configuration](https://fluentmigrator.github.io/intro/configuration.html).
This is an implementation gate, not a claim of released provider support.

| Additional engine | Real-engine GitHub Actions route | Current disposition |
| --- | --- | --- |
| SAP HANA | Official HANA Express Linux container and SAP's .NET driver, disposable schema | Qualification workflow added; provider admission requires a successful actual-engine run, followed by provider behavioral tests |
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

The separate qualification workflow pins HANA Express 2.00.088.00.20251110.1 and
Sap.Data.Hana.Net.v8.0 2.30.27. It uses a disposable public CI credential, bounded
startup, native .NET connection, identity/constraint DDL, persisted data, rollback
and catalog checks. Any startup or behavioral failure fails the job. Logs and
runner resource evidence are retained; cleanup runs independently of test success.
This is a prerequisite probe, not provider integration coverage.

If qualification passes, add the provider and mandatory matrix coverage for
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
