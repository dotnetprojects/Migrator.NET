"""Framework-level capabilities, independent of individual database coverage."""
from html import escape

FRAMEWORKS = ["Migrator.NET", "FluentMigrator", "EF Core", "DbUp", "Evolve", "OrmLite", "grate"]

# A dedicated API on applicable providers earns a check. Raw SQL and custom
# extensions do not count as that feature; SQL execution has its own row.
FRAMEWORK_ROWS = [
    ("Create / drop tables", [True, True, True, False, False, True, False], "Structured table definitions and lifecycle operations."),
    ("Rename tables", [True, True, True, False, False, False, False], "Native DDL or provider emulation; OrmLite exposes column rename but no dedicated table-rename API."),
    ("Rename columns", [True, True, True, False, False, True, False], "Native DDL or provider emulation; engine restrictions apply to every framework."),
    ("Change column type, default or nullability", [True, True, True, False, False, True, False], "Conversion and preservation rules depend on the provider."),
    ("Primary / foreign keys", [True, True, True, False, False, True, False], "Dedicated key definitions on applicable providers."),
    ("Composite primary keys", [True, True, True, False, False, False, False], "Multi-column primary keys; OrmLite models require a single primary key."),
    ("Composite foreign keys", [True, True, True, False, False, False, False], "Multi-column references; OrmLite's reviewed foreign-key API maps one field at a time."),
    ("Composite unique constraints / indexes", [True, True, True, False, False, True, False], "Enforce uniqueness across a group of columns."),
    ("Filtered indexes / conditional uniqueness", [True, True, True, False, False, True, False], "Provider-specific filters and index options are not universal engine capabilities."),
    ("Conditional guard triggers", [True, False, False, False, False, False, False], "A reusable definition rejects writes when its predicate matches."),
    ("Related-row EXISTS guard rules", [True, False, False, False, False, False, False], "Correlate affected rows with related tables without writing a trigger body."),
    ("Null-safe OLD / NEW change predicates", [True, False, False, False, False, False, False], "Compose change rules, including null transitions and negation."),
    ("Atomic multirow guard enforcement", [True, False, False, False, False, False, False], "Reject the whole statement if any affected row violates the guard."),
    ("Transactional JSON audit triggers", [True, False, False, False, False, False, False], "Record keys, operation, UTC time and previous values through a dedicated API."),
    ("Conditional auditing / selected JSON fields", [True, False, False, False, False, False, False], "Filter audit events and declare the fields in each snapshot."),
    ("General declarative trigger actions", [False, False, False, False, False, False, False], "Arbitrary actions such as dependent-row updates require SQL or custom code; dedicated guard and audit APIs are compared above."),
    ("Structured stored-procedure / function definitions", [False, False, False, False, False, False, False], "A dedicated definition API, rather than executing a hand-written SQL body."),
    ("Filtered view query + lifecycle API", [True, False, False, False, False, False, False], "Structured view creation, replacement and removal."),
    ("View queries with joins / correlated predicates", [True, False, False, False, False, False, False], "Inner/left joins, aliases, grouped predicates and correlated EXISTS."),
    ("Portable binary byte-length CHECK rules", [True, False, False, False, False, False, False], "A typed expression translates byte-length semantics for the selected provider."),
    ("Portable JSON validity / shape CHECK rules", [True, False, False, False, False, False, False], "Typed JSON validation and array/object checks on applicable providers."),
    ("Projected INSERT … SELECT", [True, False, False, False, False, True, False], "Explicit target mapping and server-side data movement through a query API."),
    ("CASE / COALESCE in data projections", [True, False, False, False, False, True, False], "Compose defaults and conditional projected values without a SQL string."),
    ("Partitioned ROW_NUMBER projections", [True, False, False, False, False, True, False], "Window numbering inside the migration query API; deterministic ordering is required."),
    ("Ordered / limited / anti-join data selection", [True, False, False, False, False, True, False], "Build ordered seeds and NOT EXISTS selection on the server."),
    ("SQLite rebuild preservation of views / triggers", [True, False, False, False, False, False, False], "Live-schema preservation, dependency validation and savepoint rollback, including external and TEMP objects."),
]


def render():
    rows = [f'<tr class="feature-group"><th colspan="{len(FRAMEWORKS) + 1}" id="schema-operation-comparison">Schema, triggers and data operations</th></tr>']
    for title, supported, note in FRAMEWORK_ROWS:
        if len(supported) != len(FRAMEWORKS):
            raise ValueError(f"Framework column mismatch: {title}")
        values = []
        for index, available in enumerate(supported):
            state, symbol, label = ("yes", "✓", "Dedicated framework API") if available else ("no", "✗", "No dedicated API; raw SQL or custom code may be used")
            css = ' class="ours"' if index == 0 else ''
            values.append(f'<td{css}><span class="feature-status feature-{state}" aria-label="{label}">{symbol}</span></td>')
        rows.append(f'<tr class="advanced-feature"><th scope="row">{escape(title)}<span class="feature-detail">{escape(note)}</span></th>{"".join(values)}</tr>')
    return '\n'.join(rows)


def render_notes():
    return '''<div class="feature-qualifications" id="feature-qualifications">
<p><strong>One rule for every framework:</strong> ✓ means a dedicated framework API implements the feature on applicable providers. A missing database capability does not remove the framework's checkmark. ✗ means there is no dedicated API in the reviewed core/provider packages; authoring raw SQL or custom code does not earn a check for that feature. SQL execution is compared in its own row.</p>
<p><strong>Classic + Fluent:</strong> Migrator.NET exposes the schema, guard, audit, CHECK, view and projected-data operations above through both provider calls and the fluent migration builder. Provider support, engine versions, data types and DDL transaction rules still apply. See the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/portable-schema-api.md">portable API and provider qualifications</a> and <a href="guide/providers.html">provider guide</a>.</p>
<p class="feature-note">Schema feature sources reviewed 22 November 2026: <a href="https://github.com/dotnetprojects/Migrator.NET/tree/master/src/Migrator/Framework">Migrator APIs</a>; FluentMigrator <a href="https://github.com/fluentmigrator/fluentmigrator/blob/main/src/FluentMigrator.Abstractions/Builders/Create/ICreateExpressionRoot.cs">schema builders</a>, <a href="https://fluentmigrator.github.io/providers/sql-server.html#filtered-indexes">filtered indexes</a> and <a href="https://fluentmigrator.github.io/operations/execute-sql.html">SQL execution</a>; EF Core <a href="https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.migrationbuilder">migration operations</a>, <a href="https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#arbitrary-changes-via-raw-sql">views and triggers through SQL</a> and <a href="https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations#migrations-limitations">SQLite preservation limits</a>; <a href="https://dbup.readthedocs.io/en/latest/usage/">DbUp</a>; <a href="https://evolve-db.netlify.app/concepts/">Evolve</a>. OrmLite <a href="https://docs.servicestack.net/ormlite/db-migrations">migrations</a>, <a href="https://docs.servicestack.net/ormlite/apis/schema">schema APIs</a>, <a href="https://docs.servicestack.net/ormlite/ddl-attributes">indexes and constraints</a>, <a href="https://docs.servicestack.net/ormlite/limitations">key limitations</a>, <a href="https://docs.servicestack.net/ormlite/schema-diff">schema diff and rebuilds</a>, <a href="https://docs.servicestack.net/ormlite/window-functions">window functions</a> and <a href="https://github.com/ServiceStack/ServiceStack/blob/main/ServiceStack.OrmLite/src/ServiceStack.OrmLite/OrmLiteWriteApi.cs">INSERT … SELECT API</a>; grate <a href="https://grate-devs.github.io/grate/">SQL runner</a> and <a href="https://grate-devs.github.io/grate/configuration-options/">configuration</a>. Third-party extensions are outside this comparison.</p>
<p class="feature-note"><strong>Scope:</strong> OrmLite migrations directly expose the OrmLite connection and its schema/query APIs; those built-in APIs count here. EF Core is compared through MigrationBuilder, not an additional DbContext supplied by application code. OrmLite's generic CHECK SQL and custom view/trigger hooks do not count as typed portable definitions. Its SQLite rebuild preserves the rebuilt table's triggers and indexes, but the final row also requires external/TEMP dependency handling and validation. grate's trigger/view/procedure folders organize SQL scripts, rather than provide definition APIs.</p>
</div>
<details class="feature-ideas"><summary>Possible next additions — ideas, not commitments</summary>
<p>Guard and JSON audit triggers, structured views, typed CHECK rules and projected INSERT queries are already implemented above. The remaining ideas would extend those APIs:</p>
<ul>
<li>☐ General declarative trigger actions, such as updating dependent rows, beyond the existing guard and audit definitions.</li>
<li>☐ Expression-based UPDATE projections with joins and explicit row-pairing guarantees, beyond the existing column-copy UpdateTable/Match API.</li>
<li>☐ Structured stored-procedure and function definitions with provider translations, beyond executing their SQL directly.</li>
<li>☐ Migration-content checksum validation and checksum-driven repeatables, beyond version history and maintenance hooks.</li>
<li>☐ Schema comparison that generates migration operations, beyond explicit migrations and the current version plan.</li>
</ul><p>These are unimplemented proposals, not a release roadmap or supported feature checkmarks.</p></details>'''
