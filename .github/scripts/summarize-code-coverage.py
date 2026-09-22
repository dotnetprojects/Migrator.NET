"""Compare merged Cobertura reports; missing baselines must never imply 0%."""

import os
from pathlib import Path
import xml.etree.ElementTree as ET


def read_coverage(path):
    root = ET.parse(path).getroot()
    counts = {}
    for metric in ("lines", "branches"):
        covered = int(root.attrib[f"{metric}-covered"])
        total = int(root.attrib[f"{metric}-valid"])
        if not 0 <= covered <= total:
            raise ValueError(f"Invalid {metric} coverage in {path}")
        counts[metric] = (covered, total)
    if counts["lines"][1] == 0:
        raise ValueError(f"No instrumented source lines in {path}")
    return counts


def percentage(count):
    covered, total = count
    return 100 * covered / total if total else None


def display(count):
    rate = percentage(count)
    return f"{rate:.2f}% ({count[0]}/{count[1]})" if rate is not None else "N/A"


def summary(current, baseline=None):
    rows = [
        "## Code coverage (Coverlet)",
        "",
        "Combined Unit and all database suites; production assemblies only.",
        "",
        "| Metric | Current | PR base | Change (percentage points) |",
        "| --- | ---: | ---: | ---: |",
    ]
    for metric in ("lines", "branches"):
        previous = display(baseline[metric]) if baseline else "Unavailable"
        before = percentage(baseline[metric]) if baseline else None
        after = percentage(current[metric])
        delta = f"{after - before:+.2f}" if before is not None and after is not None else "N/A"
        rows.append(f"| {metric.title()} | {display(current[metric])} | {previous} | {delta} |")
    if baseline is None:
        rows.extend(["", "No baseline available. A successful push or manual run on the exact PR target commit must first publish a code-coverage artifact (retained for 90 days). No change is inferred."])
    rows.extend(["", "Download the **code-coverage** artifact and open **index.html** for coverage by assembly, class and source line. Coverage changes are informational; no minimum threshold is enforced."])
    return "\n".join(rows) + "\n"


def main():
    current = read_coverage(Path("artifacts/coverage/Cobertura.xml"))
    baseline_path = Path("artifacts/baseline/Cobertura.xml")
    baseline = read_coverage(baseline_path) if baseline_path.exists() else None
    report = summary(current, baseline)
    for label, variable in (("Measured commit (PR merge result)", "HEAD_SHA"), ("PR target commit", "BASE_SHA")):
        value = os.environ.get(variable)
        if value:
            report += f"\n{label}: `{value}`\n"
    Path("artifacts/coverage/summary.md").write_text(report, encoding="utf-8")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
            output.write(report)
    print(report)


if __name__ == "__main__":
    main()
