"""Run with: python .github/scripts/test-homepage-tests.py"""
import importlib.util
import pathlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("renderer", pathlib.Path(__file__).with_name("render-homepage-tests.py"))
renderer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(renderer)


class HomepageCountsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name)
        self.run = dict(html_url="https://github.com/dotnetprojects/Migrator.NET/actions/runs/1",
                        head_sha="abc123456", updated_at="2026-09-22T12:00:00Z", conclusion="failure")

    def populate(self, passed=3, failed=1):
        for suite in renderer.EXPECTED:
            (self.root / f"{suite}.trx").write_text(
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                f'<ResultSummary><Counters total="5" executed="4" passed="{passed}" failed="{failed}"/>'
                '</ResultSummary></TestRun>')

    def test_counts_and_failure_provenance(self):
        self.populate()
        result = renderer.render(self.root, self.run)
        for text in ('48</strong>executed', '36</strong>passed', '12</strong>failed',
                     '12</strong>skipped', '0</strong>other', 'abc1234', 'workflow: failure'):
            self.assertIn(text, result)

    def test_success(self):
        self.populate(passed=4, failed=0)
        self.run['conclusion'] = 'success'
        self.assertIn('48</strong>passed', renderer.render(self.root, self.run))

    def test_missing_suite_does_not_show_partial_totals(self):
        self.populate()
        (self.root / 'Unit.trx').unlink()
        result = renderer.render(self.root, self.run)
        self.assertIn('Incomplete test results: 11 of 12', result)
        self.assertNotIn('test-counts', result)

    def test_duplicate_suite_rejected(self):
        self.populate()
        duplicate = self.root / 'duplicate'
        duplicate.mkdir()
        (duplicate / 'Unit.trx').write_text((self.root / 'Unit.trx').read_text())
        with self.assertRaises(ValueError):
            renderer.render(self.root, self.run)

    def test_invalid_counts_rejected(self):
        self.populate(passed=8)
        with self.assertRaises(ValueError):
            renderer.render(self.root, self.run)

    def coverage(self, lines=75, total=100, branches=2, branch_total=3):
        path = self.root / 'Cobertura.xml'
        path.write_text(f'<coverage lines-covered="{lines}" lines-valid="{total}" '
                        f'branches-covered="{branches}" branches-valid="{branch_total}"/>')
        return path

    def test_merged_coverage_and_report_link(self):
        self.populate()
        self.run['coverage_url'] = self.run['html_url'] + '/artifacts/123'
        result = renderer.render(self.root, self.run, self.coverage())
        for text in ('75.00%</strong>line coverage', '66.67%</strong>branch coverage',
                     '75 / 100 covered', '2 / 3 covered', '/artifacts/123', '48</strong>executed'):
            self.assertIn(text, result)

    def test_missing_coverage_preserves_test_results(self):
        self.populate()
        result = renderer.render(self.root, self.run, self.root / 'missing.xml')
        self.assertIn('Code coverage unavailable for this run', result)
        self.assertIn('48</strong>executed', result)
        self.assertNotIn('0.00%', result)

    def test_zero_branches_is_not_zero_percent(self):
        result = renderer.render(self.root, self.run, self.coverage(branches=0, branch_total=0))
        self.assertIn('N/A</strong>branch coverage', result)

    def test_invalid_coverage_rejected(self):
        for values in ({'lines': 101}, {'lines': -1}, {'total': 0}, {'branches': 4}):
            with self.subTest(values=values), self.assertRaises(ValueError):
                renderer.render(self.root, self.run, self.coverage(**values))

    def test_incomplete_tests_still_identify_coverage_source(self):
        result = renderer.render(self.root, self.run, self.coverage())
        self.assertIn('Incomplete test results: 0 of 12', result)
        self.assertIn('75.00%', result)
        self.assertIn('abc1234', result)

    def test_suite_coverage_is_not_used_as_merged_coverage(self):
        self.populate()
        self.coverage()
        result = renderer.render(self.root, self.run)
        self.assertIn('Code coverage unavailable', result)
        self.assertNotIn('75.00%', result)


if __name__ == '__main__':
    unittest.main()
