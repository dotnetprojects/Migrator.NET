"""Regression checks for coverage comparison edge cases."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
import sys

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("coverage_summary", Path(__file__).with_name("summarize-code-coverage.py"))
coverage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(coverage)

normalizer_spec = importlib.util.spec_from_file_location("normalize_coverage", Path(__file__).with_name("normalize-code-coverage.py"))
normalizer = importlib.util.module_from_spec(normalizer_spec)
normalizer_spec.loader.exec_module(normalizer)


class CoverageSummaryTests(unittest.TestCase):
    def test_vstest_attachment_copies_are_deduplicated(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            data = b'<coverage lines-valid="10" lines-covered="5"/>'
            for relative in ("guid/coverage.cobertura.xml", "runner/In/host/coverage.cobertura.xml"):
                path = root / relative
                path.parent.mkdir(parents=True)
                path.write_bytes(data)
            normalizer.normalize(root)
            self.assertEqual((root / "coverage/coverage.cobertura.xml").read_bytes(), data)
            normalizer.normalize(root)  # Idempotent; the canonical copy is ignored.

    def test_missing_empty_and_conflicting_reports_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError):
                normalizer.normalize(root)
            first = root / "one/coverage.cobertura.xml"
            first.parent.mkdir()
            first.write_text('<coverage lines-valid="0"/>')
            with self.assertRaises(ValueError):
                normalizer.normalize(root)
            first.write_text('<coverage lines-valid="10"/>')
            second = root / "two/coverage.cobertura.xml"
            second.parent.mkdir()
            second.write_text('<coverage lines-valid="20"/>')
            with self.assertRaises(ValueError):
                normalizer.normalize(root)

    def test_delta_uses_percentage_points_and_different_denominators(self):
        current = {"lines": (90, 120), "branches": (1, 4)}
        base = {"lines": (80, 100), "branches": (1, 8)}
        report = coverage.summary(current, base)
        self.assertIn("75.00% (90/120) | 80.00% (80/100) | -5.00", report)
        self.assertIn("25.00% (1/4) | 12.50% (1/8) | +12.50", report)

    def test_missing_baseline_and_zero_branches(self):
        report = coverage.summary({"lines": (1, 2), "branches": (0, 0)})
        self.assertIn("50.00% (1/2) | Unavailable | N/A", report)
        self.assertIn("Branches | N/A | Unavailable | N/A", report)
        self.assertIn("No baseline available", report)

    def test_cobertura_counts_and_empty_report_rejection(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "coverage.xml"
            path.write_text('<coverage lines-covered="3" lines-valid="4" branches-covered="0" branches-valid="0"/>')
            self.assertEqual(coverage.read_coverage(path)["lines"], (3, 4))
            path.write_text('<coverage lines-covered="0" lines-valid="0" branches-covered="0" branches-valid="0"/>')
            with self.assertRaises(ValueError):
                coverage.read_coverage(path)


if __name__ == "__main__":
    unittest.main()
