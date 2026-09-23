# Migrator.NET website and migration manual

The static GitHub Pages site contains the homepage, sourced framework comparison and 38 documentation chapters. Every example has Classic/Fluent tabs; shared host and shell commands appear in both tabs. The choice persists across pages when local storage is available. Without JavaScript both examples remain visible and chapter navigation still works. No external fonts or client-side libraries are required.

## Edit and build

The generator uses only Python's standard library. Edit `_src/content.py` for chapters and paired code examples, `_src/home.html` for the homepage, and `_src/comparison.html` for its comparison table. Styling and progressive enhancements live in `assets/site.css` and `assets/site.js`. Commit generated `guide/*.html`, `index.html` and `assets/search-index.json` with their sources.

From the repository root:

```sh
python -B .github/scripts/build-docs.py
python -B .github/scripts/build-docs.py --check
python -B .github/scripts/verify-docs.py --compile
python -B .github/scripts/test-homepage-tests.py
```

`--compile` requires the .NET 9 SDK/runtime and restores sample dependencies. It compiles every C# example against the source projects and executes SQLite schema-parity/reversal checks for creation pairs. The verifier also checks local links, fragments, source references, unique IDs, control references and paired examples. Temporary projects are removed after validation.

Use the `page`, `section` and `pair` helpers for content. Complete migration-class samples include imports. Body fragments belong inside `Up()` or `BuildUp(MigrationBuilder migration)`; host fragments use the context described in their chapter. Shell and shared host samples are rendered in both tabs.

## Preview

From the repository root:

```sh
python -m http.server 8766 --directory docs --bind 127.0.0.1
```

Open [localhost:8766](http://localhost:8766). Check desktop and narrow screens, keyboard tab selection (Left/Right, Home/End), preference persistence, copying, search, mobile chapter navigation and horizontal table scrolling. Copy buttons need HTTPS or localhost; search needs HTTP serving. Content and navigation work without JavaScript.

## Publish on GitHub Pages

1. In the repository's **Settings → Pages → Build and deployment**, set **Source** to **GitHub Actions**.
2. Merge the site and `.github/workflows/pages.yml` into `master`.
3. The workflow validates generated pages and links, then publishes `docs/`. After enabling Pages, you can also run **Deploy homepage to GitHub Pages** manually on `master`. The [documentation workflow](../.github/workflows/docs.yml) checks generation, links, C# samples and the CI-count renderer on pull requests.

Expected project URL: https://dotnetprojects.github.io/Migrator.NET/

All site assets and search-result links use relative URLs, so the repository subpath works without a custom domain. Asset fingerprints refresh cached CSS and JavaScript after changes. No deployment or repository setting changes are performed by a local preview.

## Keep the comparison accurate

The comparison distinguishes source capabilities from guarantees about released packages or database compatibility. Update the review date and source links together when reviewing it. Avoid equating transaction rollback with reversing completed migrations, treating a provider enum as a support guarantee, or assuming scopes isolate physical tables. The runner filters explicitly scoped migrations and lets unscoped migrations inherit its effective scope.

The quick start targets .NET 9 and installs the core library and SQLite driver through NuGet. Sample validation uses local project references to catch API drift. The SQLite driver version matches the repository test dependency.

## Status badges, test counts and code coverage

The homepage links to master CI results and NuGet, and identifies the MPL-1.1 license.
Pages also redeploys when master CI completes. During deployment it selects the latest
completed master push run (including failures), downloads its TRX artifacts, and renders
executed/passed/failed/skipped/other counts with the run URL, commit and timestamp.
The same run supplies the merged `code-coverage` artifact. Pages displays line and branch
coverage percentages and covered/total counts for production assemblies, with a link to
the downloadable HTML report (open `index.html`). Shared code is counted once in the
merged report; suite percentages are never added or averaged. A missing or expired
coverage artifact is shown as unavailable, never as 0% or replaced by another run.
Counts and coverage are a deployment snapshot, not a guarantee about every provider.
Missing suites produce an incomplete-results message, never a partial success total.
The committed/local page shows a fallback link until deployment supplies results.
The renderer only changes the uploaded Pages artifact; it does not commit generated counts. Preserve `TEST_RESULTS_START` and `TEST_RESULTS_END` in `_src/home.html`.

Keep the homepage, README summary and detailed comparison aligned. The fluent homepage
example replaces the Classic version-1 class and uses the same quick-start runner.

Present capabilities as ordinary features, without version-specific preview banners. Keep version numbers in the upgrade guide where they explain compatibility changes. Highlight automatic live-schema SQLite reconstruction in the homepage and README, distinguishing it from EF Core's model-based rebuilds and SQL runners' author-written scripts. Retain preservation limits and links to the operation matrix.

## Design references

The learning path follows [FluentMigrator's documentation](https://fluentmigrator.github.io/intro/quick-start.html), adapted to this API. Visual references are [Resend](https://resend.com/) for typography and code tabs and [Gel](https://www.geldata.com/) for code walkthroughs. The paper/rust palette, migration-history motif, layout and copy are specific to this project.
