"""Regression checks for documentation paths on both Windows and Linux."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("verify_docs", Path(__file__).with_name("verify-docs.py"))
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class ExactPathTests(unittest.TestCase):
    def test_checks_file_and_directory_case_and_relative_links(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Mysql").mkdir()
            (root / "Mysql/MySqlTransformationProvider.cs").write_text("", encoding="utf-8")
            (root / "index.html").write_text("", encoding="utf-8")
            for relative, expected in (
                ("Mysql/MySqlTransformationProvider.cs", True),
                ("Mysql/MysqlTransformationProvider.cs", False),
                ("mysql/MySqlTransformationProvider.cs", False),
                ("Mysql/Missing.cs", False),
                ("Mysql/../index.html", True),
                ("Mysql/../Index.html", False),
            ):
                with self.subTest(path=relative):
                    self.assertEqual(verifier.exact_path_exists(root, relative), expected)


if __name__ == "__main__":
    unittest.main()
