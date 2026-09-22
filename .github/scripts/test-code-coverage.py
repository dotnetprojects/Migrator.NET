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


class CoverageSummaryTests(unittest.TestCase):
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
