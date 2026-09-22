"""Select one validated report, ignoring byte-identical VSTest attachment copies."""
import hashlib
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def normalize(directory):
    directory = Path(directory)
    destination = directory / "coverage" / "coverage.cobertura.xml"
    reports = {}
    for path in directory.rglob("coverage.cobertura.xml"):
        if path == destination:
            continue
        data = path.read_bytes()
        reports[hashlib.sha256(data).hexdigest()] = data
    if len(reports) != 1:
        raise ValueError(f"Expected one distinct Coverlet report, found {len(reports)}")
    data = next(iter(reports.values()))
    root = ET.fromstring(data)
    if root.tag != "coverage" or int(root.get("lines-valid", "0")) <= 0:
        raise ValueError("Coverlet report contains no instrumented lines")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(data)
    print(f"Validated Coverlet report: {destination}")


if __name__ == "__main__":
    normalize(sys.argv[1])
