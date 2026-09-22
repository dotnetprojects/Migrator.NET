# Migrator.NET homepage

Static GitHub Pages site, with no build tools, external fonts or client-side dependencies. `index.html` contains the homepage, quick start and sourced framework comparison. Styling and progressive enhancements live in `assets/`.

## Preview

From the repository root:

```sh
python -m http.server 8766 --directory docs --bind 127.0.0.1
```

Open http://localhost:8766. Content and navigation work without JavaScript. Copy buttons require a secure context (HTTPS or localhost).

## Publish on GitHub Pages

1. In the repository's **Settings → Pages → Build and deployment**, set **Source** to **GitHub Actions**.
2. Merge the site and `.github/workflows/pages.yml` into `master`.
3. The workflow publishes only `docs/`. After enabling Pages, you can also run **Deploy homepage to GitHub Pages** manually on `master`.

Expected project URL: https://dotnetprojects.github.io/Migrator.NET/

All site assets use relative URLs, so the repository subpath works without a custom domain. No deployment or repository setting changes are performed by a local preview. GitHub may require an environment approval if the repository has deployment protection rules.

## Keep the comparison accurate

The comparison distinguishes source capabilities from guarantees about released packages or database compatibility. Update the review date and source links together when reviewing it. Avoid equating transaction rollback with reversing completed migrations, treating a provider enum as a support guarantee, or assuming scopes isolate physical tables. The v13 runner filters explicitly scoped migrations and lets unscoped migrations inherit its effective scope.

The quick start targets the unreleased v13 source's .NET 9 API and references the source project. Check the selected NuGet release's target frameworks. The SQLite driver version matches the repository test dependency. Validate authoring and runner snippets together when changing them.

## Status badges and test counts

The homepage links to the master CI badge, stable NuGet version and MPL-1.1 license.
Pages also redeploys when master CI completes. During deployment it selects the latest
completed master push run (including failures), downloads its TRX artifacts, and renders
executed/passed/failed/skipped/other counts with the run URL, commit and timestamp.
Counts are a deployment snapshot, not code coverage or a guarantee about every provider.
Missing suites produce an incomplete-results message, never a partial success total.
The committed/local page shows a fallback link until deployment supplies results.
The renderer only changes the uploaded Pages artifact; it does not commit generated counts.

Keep the homepage, README summary and detailed comparison aligned. The fluent homepage
example replaces the imperative version-1 class and uses the same quick-start runner.
