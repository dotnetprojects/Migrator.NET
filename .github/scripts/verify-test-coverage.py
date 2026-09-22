"""Ensure database filters form a complete, disjoint partition of NUnit discovery."""
import pathlib
import sys
import xml.etree.ElementTree as ET

expected = {"Unit", "SQLite", "SQLServer", "PostgreSQL", "Oracle", "MySQL",
            "MariaDB", "Firebird", "Db2", "Informix", "Sybase", "Hana"}
seen = {}
executed = 0
counts = set()
found = set()
for path in pathlib.Path(sys.argv[1]).rglob("Migrator.Tests.xml"):
    database = path.parent.name
    if database not in expected:
        raise SystemExit(f"Unexpected result file: {path}")
    if database in found:
        raise SystemExit(f"Duplicate suite: {database}")
    found.add(database)
    root = ET.parse(path).getroot()
    counts.add(int(root.attrib["testcasecount"]))
    tests = root.findall(".//test-case")
    if not tests or not any(t.get("result") == "Passed" for t in tests):
        raise SystemExit(f"No passing tests: {database}")
    executed += len(tests)
    for test in tests:
        name = test.attrib["fullname"]
        # NUnit can discover inherited or repeated cases with identical full names.
        # They are valid within one job, but must never appear in another job.
        if name in seen and seen[name] != database:
            raise SystemExit(f"Test assigned to both {seen[name]} and {database}: {name}")
        seen[name] = database
        if test.get("result") not in {"Passed", "Skipped"}:
            raise SystemExit(f"Test did not pass: {name}")
    print(f"{database}: {len(tests)} tests")

if found != expected:
    raise SystemExit(f"Missing suites: {sorted(expected - found)}")
if len(counts) != 1 or executed != next(iter(counts)):
    raise SystemExit(f"Discovery reports {counts} tests but jobs covered {executed} test cases")
print(f"All {executed} discovered tests assigned exactly once.")
