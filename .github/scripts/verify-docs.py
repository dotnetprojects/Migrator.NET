"""Check generated links/pairs; optionally compile every C# sample and run SQLite parity checks."""
import argparse
from html.parser import HTMLParser
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import tempfile
from urllib.parse import unquote, urlsplit
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
spec = importlib.util.spec_from_file_location("documentation_content", DOCS / "_src/content.py")
content = importlib.util.module_from_spec(spec)
spec.loader.exec_module(content)


class Page(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.path, self.ids, self.links, self.references, self.panels = path, set(), [], [], []
        self.feed(path.read_text(encoding="utf-8"))

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if "id" in attrs:
            assert attrs["id"] not in self.ids, f"Duplicate ID: {self.path}: {attrs['id']}"
            self.ids.add(attrs["id"])
        for name in ("href", "src"):
            if name in attrs:
                self.links.append(attrs[name])
        for name in ("aria-controls", "aria-labelledby", "aria-describedby", "data-copy"):
            self.references.extend(attrs.get(name, "").split())
        if "data-code-style" in attrs:
            assert "hidden" not in attrs, f"No-JS example hidden: {self.path}"
            self.panels.append(attrs["data-code-style"])


def check_site():
    pages = {path.resolve(): Page(path) for path in [DOCS / "index.html", *sorted((DOCS / "guide").glob("*.html"))]}
    for path, page in pages.items():
        assert page.panels == [style for _ in range(len(page.panels) // 2) for style in ("classic", "fluent")], f"Unpaired samples: {path}"
        for ref in page.references:
            assert ref in page.ids, f"Broken control reference: {path}: {ref}"
        for link in page.links:
            parsed = urlsplit(link)
            if parsed.scheme or parsed.netloc:
                continue
            target = (path.parent / unquote(parsed.path)).resolve() if parsed.path else path
            assert target.exists(), f"Broken link: {path}: {link}"
            if parsed.fragment and target in pages:
                assert unquote(parsed.fragment) in pages[target].ids, f"Broken anchor: {path}: {link}"
    for page in content.PAGES:
        assert (ROOT / page["source"]).exists(), f"Missing implementation reference: {page['source']}"
    entries = json.loads((DOCS / "assets/search-index.json").read_text(encoding="utf-8"))
    assert len(entries) == len(content.PAGES)
    for entry in entries:
        assert (DOCS / entry["url"]).resolve() in pages
    print(f"Checked {len(pages)} HTML pages: local links, anchors, control references, search entries and paired samples.")


USINGS = """global using System;
global using System.Data;
global using DotNetProjects.Migrator;
global using DotNetProjects.Migrator.Framework;
global using DotNetProjects.Migrator.Framework.Fluent;
global using DotNetProjects.Migrator.Providers;
global using DotNetProjects.Migrator.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Data.Sqlite;
"""
STUB = "public class CreateUsers : Migration { public override void Up() {} public override void Down() {} }"


def compile_samples():
    with tempfile.TemporaryDirectory(prefix="migrator-docs-") as directory:
        folder = Path(directory)
        project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable></PropertyGroup><ItemGroup>'
        for path in ("src/Migrator/DotNetProjects.Migrator.csproj", "src/Migrator.Extensions.DependencyInjection/DotNetProjects.Migrator.Extensions.DependencyInjection.csproj"):
            project += f'<ProjectReference Include="{escape(str(ROOT / path))}" />'
        project += '<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.7" /><PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.5" /></ItemGroup></Project>'
        (folder / "Examples.csproj").write_text(project, encoding="utf-8")
        (folder / "Usings.cs").write_text(USINGS, encoding="utf-8")
        cases, count = [], 0
        for i, example in enumerate(content.EXAMPLES):
            if example["kind"] == "shell":
                continue
            types = []
            for style in ("classic", "fluent"):
                namespace = f"Example{i}_{style}"
                code = example[style]
                directives = re.findall(r"^using [\w.]+;\s*$", code, flags=re.M)
                code = re.sub(r"^using [\w.]+;\s*$", "", code, flags=re.M).strip()
                if example["kind"] == "body":
                    base = "Migration" if style == "classic" else "FluentMigration"
                    signature = "Up()" if style == "classic" else "BuildUp(MigrationBuilder migration)"
                    reverse = "public override void Down() {}" if style == "classic" else "public override void BuildDown(MigrationBuilder migration) {}"
                    code = f"public class Sample : {base} {{ public override void {signature} {{\n{code}\n}} {reverse} }}"
                    classname = "Sample"
                elif example["kind"] in ("host", "program"):
                    parameters = "IDbConnection connection, ITransformationProvider provider, Migrator runner, IServiceCollection services" if example["kind"] == "host" else ""
                    code = f"public static class Host {{ public static void Run({parameters}) {{\n{code}\n}} }}\n{STUB}"
                    classname = "Host"
                else:
                    classname = re.search(r"class (\w+)", code).group(1)
                source = f"namespace {namespace} {{\n" + '\n'.join(directives) + f'\n#line 1 "{example["id"]}-{style}.cs"\n{code}\n}}'
                (folder / f"{namespace}.cs").write_text(source, encoding="utf-8")
                types.append(f"{namespace}.{classname}")
                count += 1
            if example["smoke"]:
                cases.append(f'Check("{example["id"]}", new {types[0]}(), new {types[1]}(), {str(example["kind"] == "class").lower()});')
        program = r'''
__CASES__
Console.WriteLine("SQLite Classic/Fluent schema parity and reverse checks passed.");

static void Check(string label, IMigration classic, IMigration fluent, bool reverse)
{
    var left = Run(classic, reverse);
    var right = Run(fluent, reverse);
    if (left != right) throw new Exception(label + " schema mismatch:\n" + left + "\n" + right);
}
static string Run(IMigration migration, bool reverse)
{
    using var connection = new SqliteConnection("Data Source=:memory:");
    connection.Open();
    using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null);
    migration.Database = provider;
    migration.Up();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT name, sql FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
    var definitions = new System.Text.StringBuilder();
    using (var reader = command.ExecuteReader())
        while (reader.Read()) definitions.AppendLine(reader.GetString(0) + ":" + reader.GetString(1));
    if (definitions.Length == 0) throw new Exception("Creation example did not create a table");
    if (reverse)
    {
        migration.Down();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        if (Convert.ToInt32(command.ExecuteScalar()) != 0) throw new Exception("Reverse left an example table behind");
    }
    return definitions.ToString();
}
'''.replace("__CASES__", "\n".join(cases))
        (folder / "Program.cs").write_text(program, encoding="utf-8")
        result = subprocess.run(["dotnet", "run", "--project", str(folder / "Examples.csproj"), "--verbosity", "quiet"], cwd=ROOT, capture_output=True, text=True)
        if result.returncode:
            print(result.stdout)
            print(result.stderr)
            raise SystemExit(result.returncode)
        print(f"Compiled {count} published C# samples and ran {len(cases)} Classic/Fluent SQLite schema parity checks (including authored/automatic reversal).")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--compile", action="store_true", help="Compile all C# examples and execute SQLite creation/reversal pairs.")
    args = parser.parse_args()
    check_site()
    if args.compile:
        compile_samples()
