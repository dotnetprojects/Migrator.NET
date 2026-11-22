"""Homepage operation matrices. Checkmarks describe APIs, not universal engine guarantees."""
from html import escape

PROVIDERS = ["SQLite", "PostgreSQL", "SQL Server", "MySQL", "MariaDB", "Oracle", "Db2", "Firebird", "Informix", "Sybase ASE", "SAP HANA", "Ingres"]
LABELS = {
    "yes": ("✓", "Built-in API"),
    "limited": ("△", "Provider or version limits"),
    "sql": ("SQL", "Author native SQL; no portable API"),
    "no": ("—", "Not implemented by this API"),
}

def cells(default="yes", **overrides):
    return [overrides.get(p, default) for p in PROVIDERS]

DATABASE_ROWS = [
    ("Create / drop tables", cells(Ingres="limited"), "Named fields, keys and explicit engine limits."),
    ("Rename tables", cells(Firebird="no", Ingres="limited"), "Firebird 5 table rename is unsupported; this is not a schema-move API."),
    ("Add / drop / rename columns", cells("limited"), "Native DDL or reconstruction; dependent objects and data types impose limits."),
    ("Change type, default or nullability", cells("limited"), "Database conversion rules apply. SQLite reconstructs supported definitions."),
    ("Primary, foreign and unique keys", cells(Ingres="limited"), "Composite keys supported; action/match options follow provider capabilities."),
    ("Indexes and conditional uniqueness", cells("limited"), "Native filtered indexes or supported emulation; not every filter is portable."),
    ("Conditional guard triggers", cells(Ingres="no"), "INSERT/UPDATE/DELETE guards; SQL Server/ASE UPDATE needs immutable pairing keys."),
    ("Audit triggers with JSON snapshots", cells("sql", **{p: "yes" for p in ["SQLite", "PostgreSQL", "SQL Server", "MySQL", "MariaDB", "Oracle", "Db2"]}), "One event per matching row; native JSON/UTC and minimum-version requirements."),
    ("General triggers / procedures / functions", cells("sql"), "Use explicit native SQL for arbitrary trigger bodies and stored programs."),
    ("Filtered, aliased views: create / replace / drop", cells(Ingres="limited"), "Inner/left joins, grouped predicates and correlated EXISTS; lifecycle limits apply."),
    ("Binary byte-length CHECK expressions", cells(Oracle="no", Informix="no", Ingres="no", **{"Sybase ASE": "no"}), "Length counts bytes, including zero bytes; ASE IMAGE and other LOB CHECK restrictions apply."),
    ("JSON validity / array / object CHECK expressions", cells("no", **{p: "limited" for p in ["SQLite", "PostgreSQL", "SQL Server", "MySQL", "MariaDB", "Oracle"]}), "Strict JSON and shape checks; SQL Server scalar JSON needs 2022+."),
    ("Projected INSERT … SELECT", cells(Ingres="limited"), "Ordered target mapping, parameters, expressions and binary values stay on the server."),
    ("Partitioned ROW_NUMBER projections", cells("limited", **{"Sybase ASE": "no", "Ingres": "no"}), "Modern engine versions required; supply deterministic ordering and a unique tie-breaker."),
    ("SQLite rebuild with views / external triggers", ["yes"] + ["no"] * 11, "SQLite-specific reconstruction; savepoints and connection settings are restored."),
]

FRAMEWORK_ROWS = [
    ("Create / drop tables", ["✓ API", "✓ Fluent API", "✓ Operations", "SQL", "SQL"]),
    ("Rename tables / columns", ["△ Provider limits", "✓ Fluent API", "✓ Operations", "SQL", "SQL"]),
    ("Reusable conditional guard definitions", ["✓ Provider adapters", "SQL / custom", "SQL / custom", "SQL", "SQL"]),
    ("Transactional JSON audit convenience API", ["△ Seven engines", "SQL / custom", "SQL / custom", "SQL", "SQL"]),
    ("Filtered view query + lifecycle API", ["✓ SelectQuery", "SQL / custom", "SQL / custom", "SQL", "SQL"]),
    ("Portable byte-length / JSON CHECK expressions", ["△ Provider adapters", "SQL / custom", "SQL predicate", "SQL", "SQL"]),
    ("Projected INSERT … SELECT", ["✓ Query expressions", "SQL / custom", "SQL / custom", "SQL", "SQL"]),
    ("SQLite table reconstruction", ["✓ Live schema", "△ Manual for general alteration", "✓ Model metadata", "SQL", "SQL"]),
    ("ORM model-based migration scaffolding", ["—", "—", "✓ Built in", "—", "—"]),
]

def render():
    header = "".join(f'<th scope="col">{escape(p)}</th>' for p in PROVIDERS)
    rows = []
    for title, states, note in DATABASE_ROWS:
        values = "".join(f'<td><span class="feature-status feature-{state}" aria-label="{LABELS[state][1]}">{LABELS[state][0]}</span></td>' for state in states)
        rows.append(f'<tr><th scope="row">{escape(title)}</th>{values}<td class="feature-note">{escape(note)}</td></tr>')
    framework_rows = "".join('<tr><th scope="row">' + escape(title) + '</th>' + ''.join('<td>' + escape(value) + '</td>' for value in values) + '</tr>' for title, values in FRAMEWORK_ROWS)
    return '''<section class="container section feature-section" id="database-features" aria-labelledby="features-title">
<div class="section-heading"><div><p class="eyebrow">OPERATIONS, DATABASE BY DATABASE</p><h2 id="features-title">Check the features<br><em>your migration needs.</em></h2></div><p>Source capability review: 22 November 2026.<br>Released packages can differ from repository source.</p></div>
<p>Choose an operation first, then check its database and version requirements. A checkmark means an implemented API with provider rendering. It does not promise that every option, data type or DDL transaction is supported on every version.</p>
<p class="feature-legend"><strong>✓</strong> Built-in API &nbsp; <strong>△</strong> Provider/version limits &nbsp; <strong>SQL</strong> Author native SQL &nbsp; <strong>—</strong> No implementation / not applicable</p>
<div class="table-scroll" role="region" aria-label="Database operation support checklist" tabindex="0"><table class="database-feature-table"><caption>DotNetProjects.Migrator source operations. Scroll horizontally to compare all twelve databases.</caption><thead><tr><th scope="col">Operation</th>''' + header + '<th scope="col">Qualifications</th></tr></thead><tbody>' + ''.join(rows) + '''</tbody></table></div>
<p class="feature-note">SQLite aliases use the same adapter; PostgreSQL and SQL Server legacy selectors still require an engine version that supports the requested feature. Ingres has owner/catalog contracts but no default live CI job. Oracle, MySQL and HANA retain native DDL commit behavior. General stored-program support through SQL is separate from the portable guard/audit APIs.</p>
<p>See the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/portable-schema-api.md">portable schema API and exact version limits</a>, <a href="guide/providers.html">provider guide</a>, <a href="guide/sqlite.html">SQLite preservation limits</a> and <a href="#test-results">live CI evidence</a>.</p>
<h3 id="schema-operation-comparison">The same operations in other frameworks</h3>
<p>“SQL / custom” means the operation can be implemented using native SQL or an extension; it does not mean that the framework cannot execute it. This compares the core APIs in the linked sources, excluding third-party extensions. Database/provider limitations apply to every column.</p>
<div class="table-scroll" role="region" aria-label="Schema operation comparison by framework" tabindex="0"><table><caption>Built-in schema abstractions versus native SQL workflows; reviewed 22 November 2026.</caption><thead><tr><th scope="col">Operation</th><th scope="col">Migrator.NET</th><th scope="col">FluentMigrator</th><th scope="col">EF Core</th><th scope="col">DbUp</th><th scope="col">Evolve</th></tr></thead><tbody>''' + framework_rows + '''</tbody></table></div>
<p class="feature-note">Sources: <a href="https://github.com/dotnetprojects/Migrator.NET/tree/master/src/Migrator/Framework">Migrator APIs</a>; FluentMigrator <a href="https://github.com/fluentmigrator/fluentmigrator/blob/main/src/FluentMigrator.Abstractions/Builders/Create/ICreateExpressionRoot.cs">create</a>, <a href="https://github.com/fluentmigrator/fluentmigrator/blob/main/src/FluentMigrator.Abstractions/Builders/Rename/IRenameExpressionRoot.cs">rename</a> and <a href="https://fluentmigrator.github.io/operations/execute-sql.html">SQL</a>; EF Core <a href="https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.migrationbuilder">operations</a> and <a href="https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#arbitrary-changes-via-raw-sql">triggers/views through SQL</a>; <a href="https://dbup.readthedocs.io/en/latest/usage/">DbUp execution</a>; <a href="https://evolve-db.netlify.app/concepts/">Evolve SQL migrations</a>. The <a href="#compare">broader workflow comparison</a> is below.</p>
<details class="feature-ideas"><summary>Possible next additions — not implemented promises</summary><ul><li>☐ General declarative trigger actions and dependent-row updates.</li><li>☐ Projected UPDATE queries with explicit row-pairing guarantees.</li><li>☐ Ingres guard rules and broader live provider qualification.</li><li>☐ Portable stored-procedure/function definitions.</li><li>☐ Migration-content checksums and richer schema-diff tooling.</li></ul><p>These are candidate improvements, not support checkmarks. Native SQL remains available for engine-specific behavior.</p></details>
</section>'''
