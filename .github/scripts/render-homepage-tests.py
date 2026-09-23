"""Render a dated CI snapshot from downloaded TRX artifacts into the Pages artifact."""
import html
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

EXPECTED = {"Unit", "SQLite", "SQLServer", "PostgreSQL", "Oracle", "MySQL",
            "MariaDB", "Firebird", "Db2", "Informix", "Sybase", "Hana"}


def render_coverage(report, run):
    if report is None or not pathlib.Path(report).is_file():
        return '<p>Code coverage unavailable for this run: no merged coverage artifact is available.</p>'
    root = ET.parse(report).getroot()
    if root.tag != 'coverage':
        raise ValueError('Expected a Cobertura coverage report')
    items = []
    for metric, label in (("lines", "line coverage"), ("branches", "branch coverage")):
        covered = int(root.attrib[f'{metric}-covered'])
        total = int(root.attrib[f'{metric}-valid'])
        if not 0 <= covered <= total or (metric == 'lines' and total == 0):
            raise ValueError(f'Invalid {metric} coverage')
        rate = f'{100 * covered / total:.2f}%' if total else 'N/A'
        items.append(f'<li><strong>{rate}</strong>{label}<small>{covered:,} / {total:,} covered</small></li>')
    url = html.escape(run.get('coverage_url', run['html_url']), quote=True)
    return ('<h3>Code coverage</h3><ul class="test-counts coverage-counts">' + ''.join(items)
            + '</ul><p>Production assemblies, merged across unit and database suites; '
            'shared code is counted once. '
            f'<a href="{url}">Download the full HTML coverage report ↗</a> '
            '(code-coverage artifact; open index.html).</p>')


def render(results, run, coverage=None):
    suites = {}
    for path in pathlib.Path(results).rglob("*.trx"):
        name = path.stem
        if name not in EXPECTED or name in suites:
            raise ValueError(f"Unexpected or duplicate suite: {name}")
        root = ET.parse(path).getroot()
        counter = root.find(".//{*}ResultSummary/{*}Counters")
        if counter is None:
            raise ValueError(f"Missing counters: {name}")
        values = {k: int(counter.attrib[k]) for k in ("total", "executed", "passed", "failed")}
        if not 0 <= values["passed"] + values["failed"] <= values["executed"] <= values["total"]:
            raise ValueError(f"Invalid counters: {name}")
        suites[name] = values
    url = html.escape(run["html_url"], quote=True)
    sha = html.escape(run["head_sha"][:7])
    date = html.escape(run["updated_at"])
    conclusion = html.escape(run["conclusion"] or "unknown")
    source = f'<p><a href="{url}">CI run · {sha}</a> · {date} · workflow: {conclusion}. Latest completed master run at deployment time.</p>'
    coverage_html = render_coverage(coverage, run)
    if set(suites) != EXPECTED:
        return source + f'<p>Incomplete test results: {len(suites)} of {len(EXPECTED)} suites available. Full totals unavailable; inspect the run for failures or missing artifacts.</p>' + coverage_html
    totals = {key: sum(s[key] for s in suites.values()) for key in ("total", "executed", "passed", "failed")}
    totals["skipped"] = totals["total"] - totals["executed"]
    totals["other"] = totals["executed"] - totals["passed"] - totals["failed"]
    counts = '<ul class="test-counts">' + ''.join(f'<li><strong>{totals[key]:,}</strong>{label}</li>' for key, label in (("executed", "executed"), ("passed", "passed"), ("failed", "failed"), ("skipped", "skipped / not executed"), ("other", "other outcomes"))) + '</ul>'
    return counts + coverage_html + source


if __name__ == "__main__":
    results, metadata, page = sys.argv[1:4]
    coverage = sys.argv[4] if len(sys.argv) > 4 else None
    target = pathlib.Path(page)
    content = target.read_text(encoding="utf-8")
    start = "<!-- TEST_RESULTS_START -->"
    end = "<!-- TEST_RESULTS_END -->"
    before, remainder = content.split(start, 1)
    _, after = remainder.split(end, 1)
    target.write_text(before + start + "\n" + render(results, json.loads(pathlib.Path(metadata).read_text(encoding="utf-8")), coverage) + "\n" + end + after, encoding="utf-8")
