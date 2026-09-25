"""Documentation source. HTML prose is authored here; C# pairs are compiled by verify-docs.py."""
from textwrap import dedent
from html import escape

PAGES = []
EXAMPLES = []


def pair(title, classic, fluent=None, kind="body", smoke=False):
    example = dict(id=f"example-{len(EXAMPLES) + 1}", title=title,
                   classic=dedent(classic).strip(), fluent=dedent(fluent if fluent is not None else classic).strip(),
                   kind=kind, smoke=smoke)
    if kind == "class":
        for style in ("classic", "fluent"):
            if not example[style].startswith("using "):
                imports = "using System;\nusing System.Data;\nusing DotNetProjects.Migrator;\nusing DotNetProjects.Migrator.Framework;\n"
                if style == "fluent":
                    imports += "using DotNetProjects.Migrator.Framework.Fluent;\n"
                example[style] = imports + "\n" + example[style]
    EXAMPLES.append(example)
    return example


def section(title, *blocks):
    return dict(title=title, blocks=list(blocks))


def table(headers, rows):
    return '<div class="table-scroll" tabindex="0" role="region" aria-label="Reference table"><table><thead><tr>' + ''.join(f'<th scope="col">{escape(h)}</th>' for h in headers) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join(f'<td>{v}</td>' for v in row) + '</tr>' for row in rows) + '</tbody></table></div>'


def page(group, slug, title, summary, *sections, source="src/Migrator/Framework/ITransformationProvider.cs"):
    PAGES.append(dict(group=group, slug=slug, title=title, summary=summary, sections=list(sections), source=source))


CREATE_USERS = pair("CreateUsers.cs", '''
using System.Data;
using DotNetProjects.Migrator.Framework;

[Migration(1)]
public class CreateUsers : Migration
{
    public override void Up()
    {
        Database.AddTable("Users",
            new Column("Id", DbType.Int32) { IsNullable = false },
            new Column("Name", DbType.String, 255),
            new PrimaryKeyConstraint("PK_Users", "Id"));
    }

    public override void Down() => Database.RemoveTable("Users");
}
''', '''
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;

[Migration(1)]
public class CreateUsers : FluentMigration
{
    public override void BuildUp(MigrationBuilder migration)
    {
        migration.Create.Table("Users")
            .WithColumn("Id").AsInt32().NotNullable()
            .WithColumn("Name").AsString(255)
            .WithPrimaryKey("PK_Users", "Id");
    }

    public override void BuildDown(MigrationBuilder migration)
        => migration.Delete.Table("Users");
}
''', kind="class", smoke=True)

INSTALL = pair("Terminal · either authoring style", '''
dotnet new console -n MigrationDemo -f net9.0
cd MigrationDemo
dotnet add package DotNetProjects.Migrator
dotnet add package Microsoft.Data.Sqlite --version 9.0.7
''', kind="shell")

HOST = pair("Program.cs · shared runner", '''
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=app.db");
connection.Open();
using var provider = ProviderFactory.Create(
    ProviderTypes.SQLite, connection, defaultSchema: null);

var runner = new Migrator(provider, typeof(CreateUsers).Assembly, trace: false);
runner.MigrateToLastVersion();
''', kind="program")

page("Introduction", "quick-start", "Your first migration", "Create a SQLite database, apply a versioned change, and write its reverse. Choose either C# style; the runner is the same.",
    section("Install the library", '<p>Start with the .NET 9 SDK and a console application. The core package supplies schema operations; your ADO.NET package connects to the database. SQLite needs no separate database server for this example.</p>', INSTALL),
    section("Write the change", '<p>Add <code>CreateUsers.cs</code>. Choose one tab and copy that class. Each public migration has a numeric version; do not put both versions of the same example into one assembly. Classic migrations execute provider methods in <code>Up</code> and <code>Down</code>. Fluent migrations collect structured operations in <code>BuildUp</code> and <code>BuildDown</code>.</p>', CREATE_USERS),
    section("Run it", '<p>Replace <code>Program.cs</code> with the shared host below and run <code>dotnet run</code>. The open connection belongs to this host and is disposed after the provider. The runner discovers public migration classes in the selected assembly.</p>', HOST, '<p>The result is an <code>app.db</code> file containing <code>Users</code> and <code>SchemaInfo</code>. Run again: version 1 is already recorded, so it is skipped. Add a class with <code>[Migration(2)]</code> for your next change.</p>'),
    section("Reverse a change", '<p>Call <code>runner.MigrateTo(0)</code> to execute the reverse methods for this migration set. Here that drops Users and its data. A reverse migration is a schema operation, not a restore of deleted rows. Test both directions on a disposable database before deployment.</p><p>Continue with <a href="creating-tables.html">creating tables</a>, or configure <a href="configuration.html">scopes, filters and transaction behavior</a>.</p>'), source="examples/FluentQuickStart/Program.cs")

page("Introduction", "installation", "Installation", "The core library, database driver, optional DI integration and CLI each have a distinct job.",
    section("Choose your packages", table(["Package", "Purpose"], [["DotNetProjects.Migrator", "Migration classes, providers, runner and fluent operations."], ["An ADO.NET driver", "Install the driver for the database your host opens."], ["DotNetProjects.Migrator.Extensions.DependencyInjection", "Optional scoped runner, constructor injection and Microsoft logging."], ["DotNetProjects.Migrator.Tool", "The <code>migrator</code> command-line tool."]]), INSTALL),
    section("Choose a driver", '<p>Common choices are Microsoft.Data.Sqlite, Microsoft.Data.SqlClient, Npgsql, MySql.Data, Oracle.ManagedDataAccess.Core and FirebirdSql.Data.FirebirdClient. The core library does not directly reference these packages. Passing an open connection makes driver selection explicit and keeps connection ownership with your application.</p><p>Read <a href="providers.html">the provider overview</a> for database families, aliases and CI coverage. A provider name is not a guarantee that every native operation has the same behavior on every server.</p>'),
    section("Use the repository", '<p>To develop against a checkout, replace the core package reference with a project reference to <code>src/Migrator/DotNetProjects.Migrator.csproj</code>. The solution targets .NET 9. Building the <code>.slnx</code> solution requires an SDK that understands that format, such as SDK 9.0.200 or later.</p><p>Keep the library, CLI and optional DI integration on compatible versions. Recompile old migration assemblies when updating a breaking API; the <a href="upgrading.html">upgrade guide</a> explains the column and constraint changes.</p>'), source="src/Migrator/DotNetProjects.Migrator.csproj")

page("Introduction", "configuration", "Configuration", "Select the connection, migration set and history scope first, then set runner options before executing.",
    section("Connect and select a scope", '<p>This host fragment assumes an open ADO.NET <code>connection</code>. The provider scope partitions history and selects explicitly scoped classes. An unscoped migration inherits the provider scope. Scopes do not create separate database objects: two modules can still conflict on a table name.</p>', pair("Host configuration · both styles", '''
using var billingProvider = ProviderFactory.Create(
    ProviderTypes.SQLite, connection, defaultSchema: null, scope: "billing");
billingProvider.CommandTimeout = 60;
var billing = new Migrator(billingProvider, typeof(CreateUsers).Assembly, false);
billing.SchemaInfoTableName = "BillingSchemaInfo";
billing.Options.Tags.Add("core");
billing.Options.TagMatch = TagMatchMode.All;
billing.Options.TransactionMode = MigrationTransactionMode.WholeSession;
billing.MigrateToLastVersion();
''', kind="host")),
    section("Runner options", table(["Option", "Behavior"], [["Tags / TagMatch", "Case-sensitive ordinal tags; match Any or All. No filter selects all versioned migrations."], ["Profiles", "Explicit profile names; selected profiles run after versioned migrations."], ["TransactionMode", "PerMigration, None or WholeSession. WholeSession supports SQLite, PostgreSQL and SQL Server."], ["Activator", "Optional delegate for creating migration instances."], ["Lock / LockTimeout", "Optional cross-process lease acquired before reading history; default timeout is 30 seconds."]])),
    section("History and ownership", '<p>Set the history table before accessing history or running migrations. Its default name is <code>SchemaInfo</code>; the default scope is <code>default</code>. Give each runner its intended assembly or explicit migration types. Duplicate versions within an effective scope fail discovery.</p><p>Load connection strings from application configuration or environment variables. The CLI reads <code>MIGRATOR_CONNECTION</code> by default. Do not store production credentials in migration classes.</p>'), source="src/Migrator/RunnerOptions.cs")

page("Introduction", "faq", "Frequently asked questions", "Decisions to make before adopting the library or moving an existing migration project.",
    section("Do I need an ORM?", '<p>No. Migrations operate on an ADO.NET connection through the transformation provider. Use EF, Dapper, another data layer or direct SQL in the rest of your application. Migrator does not scaffold schema changes from an object model.</p>'),
    section("Can I mix Classic and Fluent?", '<p>Yes. Both implement the same migration contract, run through the same loader and share history. Keep each version unique. The tabs throughout these guides show equivalent choices, not two classes to install together. The authoring method names differ: <code>Up/Down</code> versus <code>BuildUp/BuildDown</code>.</p>', CREATE_USERS),
    section("Why does SQLite rebuilding matter?", '<p>SQLite does not implement every ALTER TABLE operation. Migrator reads the live schema and reconstructs a supported table when a column or constraint change needs it. This is useful without an ORM model. See <a href="sqlite.html">SQLite</a> for preserved objects, foreign-key checks and reconstruction boundaries.</p>'),
    section("Does rollback recover data?", '<p>A transaction can roll back a failed migration when its database operations are transactional. Downgrading a completed version executes your reverse method. Neither mechanism recovers rows already deleted by a successful migration. Use an explicit recovery design and backups for that case.</p>'),
    section("Can I edit an applied migration?", '<p>The journal records versions, scopes and timestamps, not a content checksum. Editing an applied class will not make it rerun. Add a new migration for a change. Consolidated baselines are an explicit history operation; read <a href="versioning.html">versioning and history</a>.</p>'),
    section("Why does SQL preview reject my migration?", '<p>Preview renders a structured subset. A provider callback, unsupported constraint alteration or schema dependency after raw SQL cannot be represented reliably and raises an error. Read <a href="preview.html">planning and SQL preview</a> rather than treating preview as a full execution simulation.</p>'))

page("Operations", "creating-tables", "Creating tables", "Describe a complete table: columns first, with explicit named keys and constraints.",
    section("Create a table with a key", '<p>The table definition groups related schema objects into one operation. Primary-key columns are emitted as non-nullable. In the fluent API a complete table is collected before execution, so keys can refer to columns declared in the same chain. WithColumn returns a builder bound to that specific column. Table-level methods such as WithPrimaryKey return the table builder; call WithColumn again before supplying more column options.</p>', CREATE_USERS),
    section("Composite keys and uniqueness", '<p>Use the declared key order consistently in both primary and foreign keys. A composite unique constraint applies to the tuple; it does not make each column unique separately. The fully qualified constraint type below avoids the name collision with System.Data.UniqueConstraint.</p>', pair("A table with an ordered composite key", '''
Database.AddTable("Subscriptions",
    new Column("TenantId", DbType.Int32),
    new Column("UserId", DbType.Int32),
    new Column("Email", DbType.String, 255),
    new PrimaryKeyConstraint("PK_Subscriptions", "TenantId", "UserId"),
    new DotNetProjects.Migrator.Framework.UniqueConstraint(
        "UQ_Subscriptions_Email", "TenantId", "Email"));
''', '''
migration.Create.Table("Subscriptions")
    .WithColumn("TenantId").AsInt32()
    .WithColumn("UserId").AsInt32()
    .WithColumn("Email").AsString(255)
    .WithPrimaryKey("PK_Subscriptions", "TenantId", "UserId")
    .WithUniqueConstraint("UQ_Subscriptions_Email", "TenantId", "Email");
''', smoke=True)),
    section("Identity and removal", '<p>Identity generation is a column attribute, separate from primary-key membership. SQLite requires an INTEGER identity column and its single-column primary key in the same definition. Use a complete Create.Table/AddTable operation to satisfy that rule. To reverse creation use <code>Database.RemoveTable</code> or <code>migration.Delete.Table</code>; dropping a table also removes its rows.</p><p>For supported creation operations, <a href="auto-reversing.html">automatic reversal</a> can derive the reverse operation. Explicitly author reverse behavior for destructive changes.</p>'))

page("Operations", "altering-tables", "Altering tables", "Rename objects and evolve populated tables while preserving the schema details you still need.",
    section("Rename a table and column", '<p>Use explicit old and new names. The column rename signature is table, old name, new name in both APIs. A table rename does not rename explicit constraints or their backing indexes. Reusing the original key name for a replacement table may collide on SQL Server or PostgreSQL.</p>', pair("Rename existing objects", '''
Database.RenameTable("Users", "Members");
Database.RenameColumn("Members", "Name", "DisplayName");
''', '''
migration.Rename.Table("Users").To("Members");
migration.Rename.Column("Name").OnTable("Members").To("DisplayName");
''')),
    section("Change a complete column definition", '<p>Supply the type, length, nullability, default and collation you intend to retain. ChangeColumn replaces the column definition; it does not infer that table constraints should be created or removed. Existing rows must remain valid for the new definition.</p>', pair("Widen a required display name", '''
Database.ChangeColumn("Users", new Column("Name", DbType.String, 500)
{
    IsNullable = false
});
''', '''
migration.Alter.Column("Name").OnTable("Users")
    .AsString(500).NotNullable();
''')),
    section("A deployment sequence for populated data", '<p>Add a nullable column, deploy code that can read both forms, backfill values, then enforce the final requirement in a later migration. Large data copies and index creation can hold locks for substantial time; test them against a representative dataset.</p><p>On SQLite, a supported alteration may recreate the table and copy rows. On Oracle and some other engines, DDL may commit implicitly. Review the <a href="transactions.html">transaction guide</a> and your provider page before choosing the deployment boundary.</p>'))

page("Operations", "columns", "Columns and data types", "Type, size, precision, nullability, defaults, identity and collation are explicit column attributes.",
    section("Explicit table and column steps", '<p>Create.Column(name).OnTable(table) and Alter.Column(name).OnTable(table) select the table before exposing type and column options. Delete.Column(name).FromTable(table) completes a removal. For a complete Column model use Create.Column(definition).OnTable(table) or Alter.Column(definition).OnTable(table); the definition is copied. Every named fluent column requires As... or OfType(...) before execution.</p><p>Table columns, added columns and altered columns share the same options, including AsGuid, AsBoolean, AsDecimal(precision, scale), AsDate, AsDateTime and AsDateTime2. AsDateTime maps to DbType.DateTime; AsDateTime2 maps to DbType.DateTime2. OfType(DbType) and OfType(MigratorDbType) remain available for other types. Nullability defaults to nullable.</p>'),
    section("Add and remove a column", '<p>Column builders take column name followed by table name. Classic AddColumn takes table name first. New nullable columns accept existing rows without a backfill. A required column usually needs a compatible default or a staged data migration.</p>', pair("Add an optional email address", '''
Database.AddColumn("Users", new Column("Email", DbType.String, 320));
''', '''
migration.Create.Column("Email").OnTable("Users").AsString(320).Nullable();
'''), pair("Remove the email column", '''
Database.RemoveColumn("Users", "Email");
''', '''
migration.Delete.Column("Email").FromTable("Users");
''')),
    section("Precision and defaults", '<p>For decimal values specify precision and scale. In a Column constructor an integer after the type is the size, not a numeric default. Set DefaultValue explicitly to avoid overload ambiguity. Plain strings are values; trusted SQL expressions use RawSql.Insert.</p>', pair("An amount with four decimal places", '''
Database.AddColumn("Orders", new Column("Amount", DbType.Decimal)
{
    Precision = 12, Scale = 4, IsNullable = false, DefaultValue = 0m
});
''', '''
migration.Create.Column("Amount").OnTable("Orders").OfType(DbType.Decimal)
    .WithPrecision(12, 4).NotNullable().WithDefaultValue(0m);
''')),
    section("Time of day and durations", '<p>Use TimeOnly for time-of-day values and TimeSpan for intervals. A TimeSpan is a duration, including negative and multi-day values, so a TimeSpan default on a Time column is rejected. PostgreSQL and Oracle have native intervals; SQLite, SQL Server and MySQL/MariaDB store intervals as signed .NET ticks.</p>', pair("Clock time and elapsed time", '''
Database.AddTable("Jobs",
    new Column("RunAt", DbType.Time) { DefaultValue = new TimeOnly(9, 30) },
    new Column("Elapsed", MigratorDbType.Interval) { DefaultValue = TimeSpan.Zero });
''', '''
migration.Create.Table("Jobs")
    .WithColumn("RunAt").OfType(DbType.Time).WithDefaultValue(new TimeOnly(9, 30))
    .WithColumn("Elapsed").OfType(MigratorDbType.Interval).WithDefaultValue(TimeSpan.Zero);
''', smoke=True)),
    section("Database storage differs", '<p>SQLite does not enforce declared string lengths or decimal precision. UInt64 values above Int64.MaxValue are rejected there. Oracle character empty strings become NULL; Informix and Sybase have their own trimming and range behavior. Consult the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/data-type-boundary-tests.md">type support and boundary matrix</a> for supported mappings and live-test scope. A shared DbType does not imply identical native storage.</p>'))

page("Operations", "data", "Data operations", "Insert, update and delete using explicit column/value arrays. Keep predicates separate from changed values.",
    section("Insert rows", '<p>Column and value arrays must have the same length. The provider binds values using its driver-specific parameter mappings. For multiple rows issue multiple Insert.IntoTable(...).Row(...) expressions. Each Row completes one insert; its returned builder offers only IfNotExists, so a second Row cannot silently replace the first. Insert, update and delete each expose only their supported steps.</p>', pair("Insert a user", '''
Database.Insert("Users", new[] { "Id", "Name" }, new object[] { 1, "Ada" });
''', '''
migration.Insert.IntoTable("Users")
    .Row(new[] { "Id", "Name" }, new object[] { 1, "Ada" });
''')),
    section("Update and delete with predicates", '<p>Classic update/delete without a predicate affects every row. Fluent update/delete requires Where(...) or an explicit AllRows() to complete the operation. Empty predicate arrays are rejected; an unfinished chain fails during Build, Apply or Preview before any queued operation executes. Fluent WhereSql is available for updates only; its text is trusted SQL, not an escaped user input.</p>', pair("Update one user", '''
Database.Update("Users", new[] { "Name" }, new object[] { "Ada Lovelace" },
    new[] { "Id" }, new object[] { 1 });
''', '''
migration.Update.Table("Users")
    .Set(new[] { "Name" }, new object[] { "Ada Lovelace" })
    .Where(new[] { "Id" }, new object[] { 1 });
'''), pair("Delete one user", '''
Database.Delete("Users", new[] { "Id" }, new object[] { 1 });
''', '''
migration.Delete.FromTable("Users").Where(new[] { "Id" }, new object[] { 1 });
''')),
    section("Delete duplicate rows", '<p>DeleteDuplicateRows keeps one arbitrary row per composite key using database equality and collation. It supports SQLite ordinary rowid tables, PostgreSQL, Oracle ROWID tables and SQL Server. NULL keys compare equal by default; pass DuplicateNullHandling.ExcludeNullKeys to leave rows with any NULL key untouched. The direct API returns the database-reported affected-row count. Non-key values do not influence the survivor. Keys must be non-empty, distinct existing columns.</p><p>The operation executes one DELETE in the existing transaction without changing the schema. Normal DELETE triggers and foreign-key rules apply. It does not prevent concurrent or future duplicates: coordinate writers and add an appropriate unique constraint separately. Deleted data cannot be automatically reversed. SQL preview is unsupported because safe physical row identity selection requires live metadata. Unsupported providers and SQLite tables without an accessible rowid are rejected.</p>', pair("Keep one assignment per role/group pair", '''
int removed = Database.DeleteDuplicateRows("Assignments",
    new[] { "RoleId", "GroupId" }, DuplicateRowRetention.Any);
''', '''
migration.Delete.DuplicateRows().FromTable("Assignments")
    .ByColumns("RoleId", "GroupId").KeepAny();
''')),
    section("Conditional seed data", '<p>Use an explicit identifying predicate when a seed should exist only once. This is distinct from a migration version: a named profile can run repeatedly without a history entry. Coordinate competing writers; a check-then-insert helper is not a substitute for a database unique key.</p>', pair("Insert a missing seed", '''
Database.InsertIfNotExists("Users", new[] { "Id", "Name" },
    new object[] { 1, "Ada" }, new[] { "Id" }, new object[] { 1 });
''', '''
migration.Insert.IntoTable("Users")
    .Row(new[] { "Id", "Name" }, new object[] { 1, "Ada" })
    .IfNotExists(new[] { "Id" }, new object[] { 1 });
''')),
    section("Copying and reversal", '<p>Use the provider CopyDataFromTableToTable helper or fluent Execute.CopyDataFromTable(...).ToTable(...).WithColumns(...) for named-column copies. Both tables must already exist and target columns must accept the source values. Execute.UpdateTable(target).FromTable(source).Set(copyPairs).Match(keyPairs) maps source/target pairs. CopyDataFromTable also supports OrderBy after WithColumns. These operations retain provider limits and are outside the SQL-preview subset. A reverse data migration needs authored recovery logic; auto-reversal cannot recreate deleted or overwritten values.</p>', pair("Copy users into an archive table", '''
Database.CopyDataFromTableToTable("Users",
    new System.Collections.Generic.List<string> { "Id", "Name" }, "ArchivedUsers",
    new System.Collections.Generic.List<string> { "UserId", "DisplayName" });
''', '''
migration.Execute.CopyDataFromTable("Users").ToTable("ArchivedUsers")
    .WithColumns(new[] { "Id", "Name" }, new[] { "UserId", "DisplayName" });
''')))

page("Operations", "schema", "Schema inspection", "Read the connected database before deciding what to change. Metadata is different from a model snapshot.",
    section("Inspect tables and columns", '<p>Classic migrations read through Database. FluentMigration exposes Schema for queries and Context for the full provider API. A fluent authoring method runs before its queued operations: an inspection cannot see a table merely queued earlier in the same builder.</p>', pair("Add a column only when it is missing", '''
if (!Database.ColumnExists("Users", "Email"))
    Database.AddColumn("Users", new Column("Email", DbType.String, 320));
''', '''
if (!Schema.Table("Users").ColumnExists("Email"))
    migration.Create.Column("Email").OnTable("Users").AsString(320);
''')),
    section("Read ordered constraints", '<p>GetColumns returns inferred column attributes, not primary/unique membership flags. It is obsolete because native types and defaults cannot be mapped back to exact .NET definitions; use migration history for the original definition. Read typed table constraints to retain ordered composite keys. Unique indexes remain index metadata. MySQL/MariaDB catalogs cannot distinguish every original unique-index versus UNIQUE-clause authoring choice.</p>', pair("Read table constraint definitions", '''
var constraints = Database.GetTableConstraints("Users");
foreach (var constraint in constraints)
    Console.WriteLine(constraint.Name);
''', '''
var constraints = Schema.Table("Users").ConstraintDefinitions();
foreach (var constraint in constraints)
    Console.WriteLine(constraint.Name);
''')),
    section("Create a view", '<p>ViewField selects columns from a base table. The alternative IViewElement overload represents explicit columns and joins. View definitions are provider-dependent and outside SQL preview and automatic reversal. Write a provider-appropriate DROP VIEW statement in the reverse method, and manage dependent views when changing their underlying tables.</p>', pair("A projection over Users", '''
Database.AddView("UserNames", "Users", new ViewField("Id"), new ViewField("Name"));
''', '''
migration.Create.View("UserNames").FromTable("Users")
    .WithFields(new ViewField("Id"), new ViewField("Name"));
''')),
    section("Reads and portability", '<p>Dispose readers and commands obtained from the provider. Fluent Schema.Query and Schema.Table(name).Select accept a reader callback and handle disposal. Use Schema.Table(name).SelectScalar(columns, where) for a scalar selection. Use provider quoting helpers for table and column identifiers separately: quoting a table may introduce schema qualification, which is not valid for a column expression.</p><p>Metadata fidelity depends on the provider. Unsupported readers throw instead of pretending that an empty schema was found. A successful existence check is not a full schema-drift report.</p>'), source="src/Migrator/Framework/Fluent/FluentMigration.cs")

page("Operations", "sql", "Execute SQL and scripts", "Use schema operations where they fit, and keep database-specific SQL explicit.",
    section("Execute a statement", '<p>Raw SQL passes through to the selected database. It does not translate between dialects. Values from application input should be bound through a command; migration SQL is trusted application code.</p>', pair("A SQL data change", '''
Database.ExecuteNonQuery("UPDATE Users SET Name = 'Unknown' WHERE Name IS NULL");
''', '''
migration.Execute.Sql("UPDATE Users SET Name = 'Unknown' WHERE Name IS NULL");
''')),
    section("Files and embedded resources", '<p>ExecuteScript reads a file; ExecuteResourceScript reads an assembly resource. Fluent equivalents capture script text as dedicated operations. Make files available at deployment and set resource names explicitly. Relative file paths are resolved against the process working directory.</p>', pair("Execute a SQL file", '''
Database.ExecuteScript("Scripts/backfill.sql");
''', '''
migration.Execute.Script("Scripts/backfill.sql");
'''), pair("Execute an embedded SQL resource", '''
Database.ExecuteResourceScript(GetType().Assembly, "MyMigrations.Scripts.backfill.sql");
''', '''
migration.Execute.EmbeddedScript(GetType().Assembly, "MyMigrations.Scripts.backfill.sql");
'''), '<p>For the second example mark backfill.sql as an EmbeddedResource in the migration project and use its actual manifest resource name. Missing resources fail before script execution.</p>'),
    section("Client batch separators", '<p>The script APIs split standalone SQL Server GO lines, including optional line comments, while respecting strings, quoted identifiers and nested comments. GO repetition and SQLCMD directives fail before any batches execute. ExecuteNonQuery and Execute.Sql do not split client separators.</p><p>Other providers receive one command unless they implement IScriptBatchProvider. A database SQL file is not necessarily compatible with SQL*Plus, mysql-client or isql command syntax. Raw SQL also invalidates planned schema knowledge during SQL preview.</p>'))

page("Operations", "connections", "Commands and callbacks", "Use the active provider connection when a migration needs driver-level work.",
    section("Bind command parameters", '<p>The provider creates a command associated with its current transaction. Dispose it after use. Generate parameter names through the provider, rather than assuming every driver uses the same convention. The callback is deferred until fluent execution reaches it.</p>', pair("Execute a parameterized command", '''
using var command = Database.CreateCommand();
var name = Database.GenerateParameterName(0);
command.CommandText = "UPDATE Users SET Name = " + name + " WHERE Id = 1";
var value = command.CreateParameter();
value.ParameterName = name;
value.Value = "Ada";
command.Parameters.Add(value);
command.ExecuteNonQuery();
''', '''
migration.Execute.WithProvider(provider =>
{
    using var command = provider.CreateCommand();
    var name = provider.GenerateParameterName(0);
    command.CommandText = "UPDATE Users SET Name = " + name + " WHERE Id = 1";
    var value = command.CreateParameter();
    value.ParameterName = name;
    value.Value = "Ada";
    command.Parameters.Add(value);
    command.ExecuteNonQuery();
});
''')),
    section("Connection ownership", '<p>WithCommand creates and disposes a provider command around your action. WithConnection exposes the connection; WithProvider exposes the complete transformation provider. Do not close or replace a runner-owned connection, commit its transaction or switch databases while holding a native migration lock.</p>'),
    section("Database administration", '<p>Database creation and other administration require a connection and identity authorized for that operation. Use a dedicated host with TransactionMode.None. Fluent administration rejects an active transaction; do not combine it with WholeSession or assume a Classic provider call can participate in transactional DDL.</p>', pair("Create a database on a supporting server", '''
Database.CreateDatabases("Reporting");
''', '''
migration.Administration.CreateDatabase("Reporting");
'''), '<p>The remaining mappings are DropDatabases / Administration.DropDatabase, SwitchDatabase / Administration.SwitchDatabase, and KillDatabaseConnections / Administration.KillConnections. These are explicit administrative actions with provider-specific support. Database switches invalidate assumptions about migration history and session locks: keep provisioning separate from ordinary schema migrations. They are outside SQL preview and automatic reversal.</p>'),
    section("Preview and reversal", '<p>Callbacks can perform arbitrary C# work and cannot be translated into SQL preview. They require explicit reverse behavior. Keeping external network calls out of migration bodies makes failures easier to reason about: a database rollback cannot undo an email or an HTTP request.</p>'))

page("Schema basics", "indexes", "Indexes", "An index is a separate schema object, even when it enforces uniqueness.",
    section("Create and remove an index", '<p>Use an explicit name so the index can be inspected or removed later. Fluent Create.Index(name).OnTable(table).WithColumns(...) names each part explicitly and preserves column order. Append Unique(), Clustered(), IncludeColumns(...) or WithFilter(...). For an existing Index definition, use Create.Index(definition).OnTable(table); fully qualify the model type if System.Index is also in scope.</p>', pair("Index a user name", '''
Database.AddIndex("Users", new DotNetProjects.Migrator.Framework.Index
{
    Name = "IX_Users_Name", KeyColumns = new[] { "Name" }, Unique = false
});
''', '''
migration.Create.Index("IX_Users_Name").OnTable("Users").WithColumns("Name");
'''), pair("Drop an index", '''
Database.RemoveIndex("Users", "IX_Users_Name");
''', '''
migration.Delete.Index("IX_Users_Name").FromTable("Users");
''')),
    section("Provider options", '<p>Index definitions also expose IncludeColumns, FilterItems and Clustered. SQL Server (2008+), PostgreSQL and SQLite support filters on any table column, including columns outside KeyColumns. EqualTo or NotEqualTo with null (or DBNull.Value) becomes IS NULL or IS NOT NULL. GetIndexes(table), also available as Schema.Table(table).Indexes() in fluent migrations, reads back the keys, included columns, flags and supported FilterItems. Filters preserve null checks and escaped string values.</p><p>UnsupportedFilterBehavior defaults to UnsupportedIndexFilterBehavior.Throw. Set it to Ignore, or append OnUnsupportedFilter(UnsupportedIndexFilterBehavior.Ignore) in fluent code, to create an unfiltered index on a provider that cannot apply the filters. For unique indexes this enforces uniqueness across all rows. This option only affects unsupported filters; it does not suppress other invalid options or execution failures.</p><p>Oracle retains its limited non-unique, key-column expression emulation and cannot read those expressions back as FilterItems; non-key filters and unique filtered indexes use the chosen unsupported behavior. Oracle rejects included and clustered index requests; SQLite reconstruction rejects existing index SQL with explicit COLLATE clauses.</p><p>The fallback policy is an authoring option and is not stored in database metadata. Preview handles simple indexes and rejects filtered indexes even in Ignore mode.</p>'),
    section("Unique index or unique constraint?", '<p>Use UniqueConstraint for a table-level invariant and an Index with Unique for an index definition. Do not infer ownership from a generated name. SQLite RemoveAllIndexes preserves declared table UNIQUE constraints; remove those through the constraint APIs. Check query plans and data cardinality when choosing index keys.</p>'), source="src/Migrator/Framework/Index.cs")

page("Schema basics", "constraints", "Keys and constraints", "Declare table invariants independently of column attributes.",
    section("Add uniqueness and a check", '<p>Existing rows must satisfy a new constraint. A rebuild or ALTER operation can fail if duplicate or invalid data is present. CHECK expressions are trusted SQL and depend on the target engine. Primary keys, unique constraints, foreign keys and checks have typed definitions.</p>', pair("Add two named constraints", '''
Database.AddUniqueConstraint("UQ_Users_Name", "Users", "Name");
Database.AddCheckConstraint("CK_Users_Id", "Users", "Id > 0");
''', '''
migration.Create.UniqueConstraint("UQ_Users_Name").OnTable("Users").WithColumns("Name");
migration.Create.CheckConstraint("CK_Users_Id").OnTable("Users").WithExpression("Id > 0");
''')),
    section("Remove the intended object", '<p>Use dedicated primary-key and foreign-key removal methods; generic RemoveConstraint is for unique/check constraints in the SQLite provider. Avoid RemoveAllConstraints unless the migration deliberately replaces every invariant.</p>', pair("Remove a check constraint", '''
Database.RemoveConstraint("Users", "CK_Users_Id");
''', '''
migration.Delete.Constraint("CK_Users_Id").FromTable("Users");
''')),
    section("Constraint identity", '<p>GetTableConstraints returns ordered typed definitions. SQLite can return a null name for an unnamed legacy constraint; an autoindex name is not a substitute constraint name. PrimaryKeyExists checks the actual key name. MySQL reports the primary key name as PRIMARY.</p><p>Altering a column does not give that column ownership of a unique constraint. SQL Server implicit ownership markers are no longer used for deletion. Explicitly remove only the object your migration intends to change.</p>'))

page("Schema basics", "foreign-keys", "Foreign keys", "Define ordered child/parent columns and independent actions for update and delete.",
    section("Add a relationship", '<p>Parent key columns must identify a suitable primary/unique key. Child and parent arrays are positional: each child column corresponds to the parent column at the same index. Both tables and their compatible columns must already exist for this example. The Classic example uses IForeignKeyActions for independent update/delete actions; the older AddForeignKey overload supplies one action for both.</p>', pair("Orders belong to users", '''
((IForeignKeyActions)Database).AddForeignKey(
    "FK_Orders_Users", "Orders", new[] { "UserId" },
    "Users", new[] { "Id" }, ForeignKeyConstraintType.Cascade,
    ForeignKeyConstraintType.NoAction);
''', '''
migration.Create.ForeignKey("FK_Orders_Users")
    .FromTable("Orders").WithColumns("UserId")
    .ToTable("Users").WithColumns("Id")
    .OnDelete(ForeignKeyConstraintType.Cascade)
    .OnUpdate(ForeignKeyConstraintType.NoAction);
''')),
    section("Remove a relationship", '<p>Remove dependent keys before incompatible table or key changes. Restore them only after the existing data satisfies the replacement relationship.</p>', pair("Remove the foreign key", '''
Database.RemoveForeignKey("Orders", "FK_Orders_Users");
''', '''
migration.Delete.ForeignKey("FK_Orders_Users").FromTable("Orders");
''')),
    section("Database semantics", '<p>Supported actions depend on the database; do not assume every engine implements CASCADE, RESTRICT, SET NULL and SET DEFAULT identically. SQLite rebuilds preserve separate update/delete actions and validate integrity before an owned transaction commits. MATCH FULL and MATCH PARTIAL requests are rejected because SQLite does not enforce those semantics.</p><p>SetNull needs nullable child columns. Test action behavior using actual data, especially composite keys and partially NULL values. Oracle supports its own subset of foreign-key actions.</p>'))

page("Schema basics", "defaults-collations", "Defaults and collations", "Distinguish values from SQL expressions, and comparison intent from a provider's installed collation name.",
    section("Literal and expression defaults", '<p>Ordinary strings are quoted literal values. RawSql.Insert marks trusted SQL to evaluate on the database. The expression below works on SQLite; provider function names and return types can differ.</p>', pair("A database-generated timestamp", '''
Database.AddTable("Events", new Column("CreatedAt", DbType.DateTime)
{
    DefaultValue = RawSql.Insert("CURRENT_TIMESTAMP"), IsNullable = false
});
''', '''
migration.Create.Table("Events")
    .WithColumn("CreatedAt").OfType(DbType.DateTime).NotNullable()
    .WithDefaultValue(RawSql.Insert("CURRENT_TIMESTAMP"));
''', smoke=True)),
    section("Comparison behavior", '<p>Collation presets request semantics. Unsupported mappings fail before DDL. SQLite AsciiIgnoreCase maps to NOCASE and folds ASCII only; it is not Unicode case folding. Named custom SQLite collations must be registered on the connection before schema or data operations use them.</p>', pair("ASCII-insensitive SQLite text", '''
Database.AddTable("Labels", new Column("Name", DbType.String, 100)
{
    Collation = Collation.AsciiIgnoreCase
});
''', '''
migration.Create.Table("Labels")
    .WithColumn("Name").AsString(100)
    .WithCollation(Collation.AsciiIgnoreCase);
''', smoke=True)),
    section("Presets and provider names", table(["Request", "Meaning"], [["CaseInsensitive / CaseSensitive", "Case behavior with accent sensitivity; supported SQL Server/MySQL/MariaDB mappings, or an explicit installed name on other engines."], ["Binary", "Provider binary comparison; not a promise of identical linguistic ordering."], ["AsciiIgnoreCase", "SQLite NOCASE; ASCII letters only."], ["Collation.Named(name)", "An installed or registered provider-specific collation."]]), '<p>Use named collations for language-specific or exact comparison behavior. PostgreSQL ICU nondeterministic collations must be created explicitly; SQL rendering does not create shared database objects. Read the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/migration-guide-12.1-to-13.md#explicit-sql-defaults-and-semantic-collations">mapping table</a> for engine versions and restrictions.</p>'))

page("Migration runners", "runners", "Choose a runner", "Use the same migration assembly in a dedicated host, a DI scope or the command-line tool.",
    section("Execution choices", table(["Runner", "A good fit"], [["Library host", "A small deployment executable with explicit connection ownership and full provider access."], ["Microsoft DI integration", "A service collection supplying constructor dependencies, options and logging."], ["migrator CLI", "Automation that selects assemblies, providers, scopes, tags and target versions."]])),
    section("A dedicated host", '<p>Run schema changes before application instances need the new schema. The host below works with either migration style and scans the assembly containing CreateUsers. Use explicit type selection when an assembly also contains migrations for other purposes.</p>', HOST),
    section("Deployment responsibilities", '<p>Give the deployment identity the schema privileges needed by the selected migrations. Coordinate concurrent deploys through an external orchestrator or supported native lock. Configure the history table and scope consistently across invocations. Log the target and result without exposing connection strings.</p><p>Choose <a href="cli.html">the CLI</a> for a ready command surface, or <a href="dependency-injection.html">DI</a> for application services. Read <a href="transactions.html">transaction and lock semantics</a> before relying on atomicity.</p>'), source="src/Migrator/Migrator.cs")

page("Migration runners", "cli", "Command-line tool", "List, validate, plan, preview, apply and reverse migrations from a deployment script.",
    section("Install and connect", '<p>Install DotNetProjects.Migrator.Tool as a .NET tool. Set MIGRATOR_CONNECTION in the deployment environment or select another variable with --connection-env. Both Classic and Fluent classes use the same commands. The tool does not print the connection-string value.</p>', pair("Install the CLI", 'dotnet tool install --global DotNetProjects.Migrator.Tool', kind="shell")),
    section("Inspect before applying", pair("Inspect a migration assembly", '''
migrator list --assembly MyMigrations.dll --provider SQLite
migrator status --assembly MyMigrations.dll --provider SQLite
migrator validate --assembly MyMigrations.dll --provider SQLite
migrator plan --assembly MyMigrations.dll --provider SQLite --target 10
migrator sql --assembly MyMigrations.dll --provider SQLite --output migration.sql
''', kind="shell"), '<p>Validate checks version planning, not arbitrary migration-body behavior. Plan lists version steps without running bodies. SQL generation renders a supported operation subset; it does not produce an idempotent history-managed bundle.</p>'),
    section("Apply and roll back", pair("Deploy a selected scope", '''
migrator migrate --assembly MyMigrations.dll --provider SQLite --scope billing --transaction WholeSession
migrator rollback --assembly MyMigrations.dll --provider SQLite --scope billing --target 0
''', kind="shell"), '<p>Rollback requires an explicit lower target and rejects any plan containing upward steps. Target checks run after taking the configured lock and refreshing history. Tags, profiles and provider choice must match the intended deployment.</p>'),
    section("Options and exit codes", table(["Option", "Purpose"], [["--tags a,b / --tag-match Any|All", "Filter versioned migrations."], ["--profiles a,b", "Select named profiles."], ["--schema / --scope", "Provider schema and migration history scope."], ["--timeout SECONDS", "Database command timeout."], ["--lock / --lock-timeout SECONDS", "Native migration lock on supported providers."], ["--offline", "SQL generation assuming empty history; profiles/maintenance rejected."]]), '<p>Exit codes: 0 success; 1 load/execution failure; 2 invalid arguments; 3 unsupported provider/operation; 4 lock timeout. SQL output can contain data authored in migrations. The packaged drivers cover SQLite, SQL Server, PostgreSQL, MySQL/MariaDB, Oracle and Firebird; use a custom host for other library providers.</p>'), source="src/Migrator.Tool/Program.cs")

page("Migration runners", "dependency-injection", "Dependency injection and logging", "Resolve the runner and migration dependencies inside one service scope.",
    section("Register the integration", '<p>Install DotNetProjects.Migrator.Extensions.DependencyInjection and Microsoft.Extensions.Logging alongside the core and database driver. This example uses the connection-string provider factory so provider disposal belongs to the DI scope. The providerName explicitly selects the SQLite driver.</p>', pair("A scoped migration host", '''
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddMigrator(_ => ProviderFactory.Create(
    ProviderTypes.SQLite, "Data Source=app.db", defaultSchema: null,
    providerName: "Microsoft.Data.Sqlite"), typeof(CreateUsers).Assembly,
    options => options.TransactionMode = MigrationTransactionMode.PerMigration);

using var container = services.BuildServiceProvider();
using var scope = container.CreateScope();
scope.ServiceProvider.GetRequiredService<Migrator>().MigrateToLastVersion();
''', kind="program")),
    section("Constructor dependencies", '<p>Migration classes are registered for activation through the service provider. Register your own constructor dependencies before resolving the runner. Options are scoped snapshots; a custom Activator can override construction. Fluent and Classic migrations use the same activation mechanism.</p>'),
    section("Logging boundaries", '<p>The integration adapts runner lifecycle events to Microsoft logging. It omits SQL text and raw exception messages from these events. Configure your own logging providers through AddLogging. The core retains its lightweight logger API when you do not use DI.</p><p>Dispose the scope after migration execution. When supplying a caller-owned open connection, keep its owner alive until after the scope is disposed; the provider does not acquire ownership of an externally supplied connection.</p>'), source="src/Migrator.Extensions.DependencyInjection/ServiceCollectionExtensions.cs")

page("Migration runners", "preview", "Planning and SQL preview", "A version plan answers what runs. SQL preview shows the supported operation SQL.",
    section("Read-only version planning", '<p>Plan and DryRun inspect applied versions through IMigrationHistory without creating/upgrading history or invoking migration bodies, callbacks, transactions or SQLite PRAGMA changes. Set the same scope, tags and assembly you intend to deploy. The fragment below assumes an initialized runner.</p>', pair("Inspect version steps", '''
var plan = runner.Plan(10);
foreach (var step in plan)
    Console.WriteLine($"{step.Version}: {(step.IsUp ? "up" : "down")}");
runner.DryRun = true;
runner.MigrateTo(10);
''', kind="host")),
    section("Preview connected SQL", '<p>PreviewSql reads connected history and schema. Classic bodies require explicit opt-in; provider calls are captured through a proxy that rejects unsupported access. Fluent authoring builds operations directly. This is trusted C# execution in both cases, not a security sandbox.</p>', pair("Generate operation SQL", '''
var sql = runner.PreviewSql(10, ProviderTypes.SQLite, allowLegacyBodies: true);
Console.WriteLine(sql);
''', '''
var sql = runner.PreviewSql(10, ProviderTypes.SQLite);
Console.WriteLine(sql);
''', kind="host")),
    section("Offline generation and boundaries", '<p>MigrationSqlPreview.Generate can render supported operations without connecting; the CLI exposes --offline. Earlier structured create/rename operations update the planned schema. Raw SQL invalidates that knowledge, so later dependencies can fail.</p><p>Basic tables/columns, supported renames, simple indexes, inserts and raw SQL form the preview subset. Unsupported alterations, constraint changes, filters, callbacks and schema dependencies throw. InitializeOnce overrides are rejected rather than skipped silently. Post-commit callbacks do not run. Output contains operation SQL, not history guards or an idempotent deployment bundle.</p>'), source="src/Migrator/Migrator.cs")

page("Migration runners", "transactions", "Transactions and locks", "Transaction rollback and cross-process coordination solve different problems.",
    section("Choose the transaction boundary", table(["Mode", "Behavior"], [["PerMigration", "Default. Each successful migration commits independently."], ["None", "Provider/operation transaction behavior; no runner-managed migration transaction."], ["WholeSession", "One session transaction on SQLite, PostgreSQL or SQL Server; history initialization happens first."]]), '<p>Actual atomicity depends on the database and operation. Administration commands or implicit-commit DDL can violate assumptions. AfterUp/AfterDown run after commit; WholeSession defers them until the session commit. A callback failure cannot undo durable changes.</p>', pair("Configure a session transaction", '''
runner.Options.TransactionMode = MigrationTransactionMode.WholeSession;
runner.MigrateToLastVersion();
''', kind="host")),
    section("Coordinate competing runners", '<p>DatabaseMigrationLock uses SQL Server application locks, PostgreSQL advisory locks or MySQL/MariaDB named locks. The lease is session-owned and remains held across migration commits. This host fragment assumes a supported provider; SQLite rejects this built-in lock.</p>', pair("Acquire a native deployment lock", '''
runner.Options.Lock = new DatabaseMigrationLock();
runner.Options.LockTimeout = TimeSpan.FromSeconds(60);
runner.MigrateToLastVersion();
''', kind="host")),
    section("Scope of protection", '<p>Native locks are keyed by database, history table and scope. Coordinate separately if different scopes modify shared objects. Do not close/replace the connection, switch databases or manipulate the native lock inside a migration. MySQL named locks coordinate one server, not an entire distributed cluster.</p><p>Implement IMigrationLock for another lease mechanism, or serialize deployments outside the process. A transaction, history primary key or ordinary database write lock alone does not prove that the whole migration sequence is serialized.</p>'), source="src/Migrator/DatabaseMigrationLock.cs")

page("Migration types", "versioning", "Versioning and scoped history", "Number changes, keep applied source immutable and give independent modules explicit histories.",
    section("Choose a version scheme", '<p>Migration accepts a numeric version or year/month/day/hour/minute/second components. Use one monotonic scheme per migration set. The date constructor builds a numeric identifier; it does not consult a clock or resolve branch collisions for you. Missing lower-numbered versions up to the target can still be applied.</p>', pair("A dated migration", '''
[Migration(2026, 9, 23, 10, 0, 0)]
public class AddUserEmail : Migration
{
    public override void Up() => Database.AddColumn("Users", new Column("Email", DbType.String, 320));
    public override void Down() => Database.RemoveColumn("Users", "Email");
}
''', '''
[Migration(2026, 9, 23, 10, 0, 0)]
public class AddUserEmail : FluentMigration
{
    public override void BuildUp(MigrationBuilder migration)
        => migration.Create.Column("Email").OnTable("Users").AsString(320);
    public override void BuildDown(MigrationBuilder migration)
        => migration.Delete.Column("Email").FromTable("Users");
}
''', kind="class")),
    section("Scope selection", '<p>An explicit MigrationAttribute.Scope selects that migration only for the matching provider scope. Unscoped migrations inherit the runner scope. Discovery, duplicate validation and history reads use the effective scope. Duplicate numeric versions in distinct explicit scopes are independent; physical tables are not isolated.</p><p>Set SchemaInfoTableName before any history access if you need a different table. AppliedMigrations lists recorded versions; LastAppliedMigrationVersion is nullable when history is empty. AssemblyLastMigrationVersion describes the loaded set.</p>'),
    section("Consolidated baselines", '<p>A baseline can record versions whose schema it already includes. The runner rechecks active-scope history before each planned step, skipping newly covered versions and their AfterUp callbacks. Downward runs similarly skip versions removed by an earlier Down. Recording the baseline version itself does not create a duplicate.</p>', pair("Mark a version included by a baseline", '''
Database.MigrationApplied(1, "billing");
''', '''
migration.Execute.WithProvider(provider => provider.MigrationApplied(1, "billing"));
'''), '<p>Only record a version after establishing the schema it represents. History entries are not a substitute for verifying an existing database. Schema/history rollback follows the selected transaction mode. No migration-content checksum is stored.</p>'), source="src/Migrator/Migrator.cs")

page("Migration types", "tags", "Tags", "Select a subset of versioned migrations using explicit ordinal names.",
    section("Tag migration classes", '<p>Tags is in DotNetProjects.Migrator. One class can declare multiple names. Choose names for deployment intent such as core or reporting; do not use a tag to hide a dependency that a selected migration still requires.</p>', pair("A reporting migration", '''
[Migration(2), Tags("reporting")]
public class CreateReportLog : Migration
{
    public override void Up() => Database.AddTable("ReportLog", new Column("Name", DbType.String, 255));
    public override void Down() => Database.RemoveTable("ReportLog");
}
''', '''
[Migration(2), Tags("reporting")]
public class CreateReportLog : FluentMigration
{
    public override void BuildUp(MigrationBuilder migration)
        => migration.Create.Table("ReportLog").WithColumn("Name").AsString(255);
    public override void BuildDown(MigrationBuilder migration)
        => migration.Delete.Table("ReportLog");
}
''', kind="class", smoke=True)),
    section("Configure matching", pair("Select tags on the runner", '''
runner.Options.Tags.Add("reporting");
runner.Options.TagMatch = TagMatchMode.Any;
runner.MigrateToLastVersion();
''', kind="host"), '<p>Any requires at least one selected tag; All requires every selected tag. Matching is ordinal and case-sensitive. Without a tag filter all eligible versioned migrations are selected. Profiles have their own explicit name selection.</p>'),
    section("Downgrade behavior", '<p>Applied versions excluded by the active filter remain applied during downgrade. A filtered run is therefore not a promise that the whole database matches one contiguous global version range. Keep deployment filters stable and inspect the plan before reversing selected changes.</p>'), source="src/Migrator/MigrationLoader.cs")

page("Migration types", "profiles", "Profiles", "Run explicitly selected work after versioned migrations without recording a version.",
    section("Define a named profile", '<p>A profile is useful for optional seed data or environment setup. It runs every time its name is selected. Make repeated execution deliberate: use an identifying predicate or other idempotent operation where appropriate.</p>', pair("A development seed profile", '''
[Profile("demo", Order = 10)]
public class DemoData : Migration
{
    public override void Up() => Database.InsertIfNotExists("Users",
        new[] { "Id", "Name" }, new object[] { 1, "Ada" },
        new[] { "Id" }, new object[] { 1 });
    public override void Down() { }
}
''', '''
[Profile("demo", Order = 10)]
public class DemoData : FluentMigration
{
    public override void BuildUp(MigrationBuilder migration)
        => migration.Insert.IntoTable("Users")
            .Row(new[] { "Id", "Name" }, new object[] { 1, "Ada" })
            .IfNotExists(new[] { "Id" }, new object[] { 1 });
    public override void BuildDown(MigrationBuilder migration) { }
}
''', kind="class")),
    section("Select a profile", pair("Run the demo profile", '''
runner.Options.Profiles.Add("demo");
runner.MigrateToLastVersion();
''', kind="host"), '<p>Profiles accept Order and Scope. Execution orders by Order and then ordinal full type name. Profile execution uses Up and does not create a migration-version entry or use Down as an undo history. An auxiliary-only run preserves existing version history.</p>'),
    section("Execution versus repeatables", '<p>A selected profile runs because it was selected, not because its source checksum changed. Treat this separately from versioned migrations and checksum-based repeatable SQL. Offline CLI SQL generation rejects profiles because it cannot represent the complete lifecycle.</p>'), source="src/Migrator/RunnerOptions.cs")

page("Migration types", "maintenance", "Maintenance migrations", "Place ordered work at the runner's lifecycle stages.",
    section("Choose a stage", table(["Stage", "When"], [["BeforeRun", "Before versioned migration work in this run."], ["BeforeMigration", "Before each executed versioned migration."], ["AfterMigration", "After each executed versioned migration."], ["AfterRun", "After the run's migration/profile work."]]), '<p>Maintenance classes accept Order and Scope. They use Up and do not acquire version records. Hooks stop on failure; later stages are not finally blocks or guaranteed cleanup paths. Lock release and connection/transaction restoration are runner responsibilities.</p>'),
    section("A scoped maintenance operation", '<p>The example expects an existing DeploymentLog table. Choose a table that already exists at the selected stage. Fluent callbacks execute at the corresponding operation position.</p>', pair("Write a deployment marker", '''
[Maintenance(MaintenanceStage.AfterRun, Order = 10)]
public class RecordDeployment : Migration
{
    public override void Up()
        => Database.Insert("DeploymentLog", new[] { "Message" }, new object[] { "Migration run finished" });
    public override void Down() { }
}
''', '''
[Maintenance(MaintenanceStage.AfterRun, Order = 10)]
public class RecordDeployment : FluentMigration
{
    public override void BuildUp(MigrationBuilder migration)
        => migration.Insert.IntoTable("DeploymentLog")
            .Row(new[] { "Message" }, new object[] { "Migration run finished" });
    public override void BuildDown(MigrationBuilder migration) { }
}
''', kind="class")),
    section("Post-commit callbacks", '<p>Migration.AfterUp and AfterDown run after commit, with the migration context restored. In WholeSession they wait until the entire session commits. Their failure reports an error after durable changes; it cannot reverse that commit. Do not confuse maintenance stages with a guaranteed post-commit delivery system.</p>'), source="src/Migrator/Migrator.cs")

page("Migration types", "auto-reversing", "Automatic reversal", "Fluent operations can describe a supported reverse sequence; Classic migrations author it directly.",
    section("Creation and its reverse", '<p>AutoReversingMigration derives reverse operations in reverse order and validates reversal support before its first change. The Classic equivalent makes the Down operation explicit. Both examples below create the same table and remove it on downgrade.</p>', pair("A reversible table creation", '''
[Migration(3)]
public class CreateNotes : Migration
{
    public override void Up() => Database.AddTable("Notes", new Column("Text", DbType.String, 500));
    public override void Down() => Database.RemoveTable("Notes");
}
''', '''
[Migration(3)]
public class CreateNotes : AutoReversingMigration
{
    public override void BuildUp(MigrationBuilder migration)
        => migration.Create.Table("Notes").WithColumn("Text").AsString(500);
}
''', kind="class", smoke=True)),
    section("What needs an authored reverse", '<p>Destructive changes, data operations, SQL and callbacks require explicit reverse behavior. Reverse support is narrower than execution support. An operation that can run is not necessarily one that can be inverted from its definition alone.</p><p>Use FluentMigration with BuildDown when the reverse needs its own steps. MigrationBuilder.WithReverse can attach an explicit backward operation to a forward operation. Dropping a newly created table on downgrade still destroys any data inserted since creation; automatic reversal is not data recovery.</p>'), source="src/Migrator/Framework/Fluent/FluentMigration.cs")

page("Database providers", "providers", "Provider overview", "One authoring contract, explicit database behavior. Choose the driver and provider together.",
    section("Database families", table(["Database", "ProviderTypes", "Guide"], [["SQLite", "SQLite / MonoSQLite", '<a href="sqlite.html">Live-schema reconstruction</a>'], ["SQL Server", "SqlServer / SqlServer2005", '<a href="sql-server.html">Constraints, batches and locks</a>'], ["PostgreSQL", "PostgreSQL / PostgreSQL82", '<a href="postgresql.html">Schemas, types and locks</a>'], ["MySQL / MariaDB", "Mysql / MariaDB", '<a href="mysql.html">DDL and collation behavior</a>'], ["Oracle", "Oracle / MsOracle", '<a href="oracle.html">Identity and metadata</a>'], ["SAP HANA", "Hana", '<a href="other-providers.html">Additional providers</a>'], ["Db2 / Informix / Firebird / Ingres / Sybase", "IBM_DB2 / IBM_Informix / Firebird / Ingres / Sybase", '<a href="other-providers.html">Engine-specific guidance</a>']])),
    section("Bring a connection", '<p>Pass an open IDbConnection to ProviderFactory.Create. Both migration styles use that provider. Alternatively use the connection-string overload and configure providerName so the provider can resolve the ADO.NET factory. Use a matching driver and test the exact server version you deploy.</p>', pair("Provider selection · open connection supplied by the host", '''
using var selectedProvider = ProviderFactory.Create(
    ProviderTypes.PostgreSQL, connection, defaultSchema: "public", scope: "billing");
var selectedRunner = new Migrator(selectedProvider, typeof(CreateUsers).Assembly, false);
selectedRunner.MigrateToLastVersion();
''', kind="host")),
    section("Qualification", '<p>The CI matrix includes SQLite, SQL Server, PostgreSQL, Oracle, MySQL, MariaDB, Firebird, Db2, Informix, Sybase and SAP HANA. Ingres and historical provider aliases have separate qualification needs. Read <a href="testing.html">testing</a> and the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/live-database-tests.md">live-engine matrix</a> for exact drivers and server setup.</p><p>Database support is operation-specific. Column types, collation presets, index options, DDL transactions and metadata readers can differ. Test stored values and preserved schema, not only the generated SQL.</p>'), source="src/Migrator/ProviderFactory.cs")

SQLITE_ALTER = pair("Change a column on an existing SQLite table", '''
Database.ChangeColumn("Users", new Column("Name", DbType.String, 500)
{
    IsNullable = false, DefaultValue = "Unknown",
    Collation = Collation.AsciiIgnoreCase
});
''', '''
migration.Alter.Column("Name").OnTable("Users")
    .AsString(500).NotNullable().WithDefaultValue("Unknown")
    .WithCollation(Collation.AsciiIgnoreCase);
''')

page("Database providers", "sqlite", "SQLite", "Change existing tables from the live schema, without maintaining an ORM model.",
    section("Automatic reconstruction", '<p>For supported changes, Migrator reads SQLiteTableInfo, changes its representation, creates a replacement table, copies mapped rows, swaps tables and recreates represented dependent objects. This provides column type/default/nullability changes and adding/removing primary, foreign, unique and check constraints.</p><p>Native rename and eligible drop-column paths are used when supported by the engine. Complex alterations use reconstruction. Existing rows must satisfy the new definition; a default does not rewrite every existing NULL during a column change.</p>', SQLITE_ALTER),
    section("What survives a rebuild", table(["Detail", "Behavior"], [["Mapped data", "Named-column copy preserves mapped values, subject to the new definition accepting them."], ["Keys and constraints", "Named/composite keys, ordered foreign-key pairs and separate update/delete actions are retained."], ["Column collations", "Declared names are retained. Register custom collations on the connection."], ["Indexes and triggers", "Supported definitions are recreated; unsafe trigger rename/drop-column cases are rejected."], ["AUTOINCREMENT", "The sequence high-water mark survives, including previously deleted identities."], ["Hidden rowid", "Not part of the mapped data and may change."]])),
    section("Boundaries are explicit", '<p>Reconstruction rejects generated columns, STRICT, WITHOUT ROWID and indexes with explicit COLLATE clauses. It is not an arbitrary SQL dependency rewriter. Adjust dependent views, complex expressions and triggers explicitly when required. MATCH FULL and MATCH PARTIAL are rejected because SQLite does not enforce their semantics.</p><p>Owned rebuild transactions and runner transactions validate foreign-key integrity before commit and restore the prior enforcement setting. For caller-owned active transactions configure foreign keys before beginning the transaction. A SQLite write lock is not a session-wide migration lease; coordinate deployment externally or provide IMigrationLock.</p>'),
    section("Values and identity", '<p>CLR Guid defaults use blobs from Guid.ToByteArray(), matching inserted parameters. Legacy text GUID defaults remain SQL expressions during unrelated rebuilds, so storage is not silently converted. Convert mixed text/blob keys explicitly and consistently across related tables.</p><p>SQLite INTEGER is signed 64-bit. Declared text lengths and decimal precision do not impose SQL Server-like enforcement. An identity needs a single INTEGER primary key in the same definition. For adding identity to an existing table, use an atomic SQLite RecreateTable definition containing both objects.</p>'),
    section("How this differs from other tools", '<p>FluentMigrator leaves general column alterations and later foreign-key changes to manual reconstruction. DbUp and Evolve run supplied scripts. EF Core also rebuilds SQLite tables using model-represented artifacts. Migrator reconstructs from live metadata without an ORM. The <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/migration-framework-comparison.md#sqlite-emulation-comparison">sourced operation comparison</a> distinguishes native SQL, emulation and manual work.</p>'), source="src/Migrator/Providers/Impl/SQLite/SQLiteTransformationProvider.cs")

page("Database providers", "sql-server", "SQL Server", "Explicit keys, provider-specific indexes, transactional DDL and application locks.",
    section("Select the provider", '<p>Use ProviderTypes.SqlServer with an open Microsoft.Data.SqlClient connection. Pass the intended default schema, commonly dbo. Historical SqlServer2005 is a separate alias with older type mappings. WholeSession transactions and DatabaseMigrationLock are available for SQL Server.</p>'),
    section("Name constraints explicitly", '<p>Column changes preserve explicit constraints and indexes. Add/remove uniqueness independently. For a nonclustered primary key on an existing compatible table use the dedicated API shown below. Review existing clustered indexes before changing key layout.</p>', pair("Add a nonclustered primary key", '''
Database.AddPrimaryKeyNonClustered("PK_Users", "Users", "Id");
''', '''
migration.Create.NonClusteredPrimaryKey("PK_Users").OnTable("Users").WithColumns("Id");
''')),
    section("Indexes and SQL batches", '<p>Index definitions can express included/filter/cluster options where supported. The script APIs split standalone GO lines; raw ExecuteNonQuery/Execute.Sql does not. SQLCMD directives and GO repetition are rejected before executing script batches. Prefer scripts for client batch syntax and commands for parameterized statements.</p>'),
    section("Types and object names", '<p>Use TimeOnly for time values and TimeSpan for interval ticks. SqlServer2005 uses its older DATETIME precision behavior. Use separate quoting helpers for table and column names. A table rename leaves named constraints/indexes attached with their old names; assign distinct names when creating a replacement table.</p>'), source="src/Migrator/Providers/Impl/SqlServer/SqlServerTransformationProvider.cs")

page("Database providers", "postgresql", "PostgreSQL", "Native intervals, schema-aware metadata, transactional DDL and advisory locks.",
    section("Connect with Npgsql", '<p>Use ProviderTypes.PostgreSQL and an open Npgsql connection, with the intended default schema. Connection search_path affects unqualified relation lookup. Metadata readers resolve the requested relation through PostgreSQL and distinguish same-named tables in different schemas.</p>'),
    section("Use native interval values", '<p>PostgreSQL maps duration values to native intervals. Time without time zone maps to a time of day; use TimeOnly for that input. Parameter mappings and scalar CLR return types are separate concerns: raw ADO.NET values remain driver-specific.</p>', pair("Store a job duration", '''
Database.AddColumn("Jobs", new Column("Elapsed", MigratorDbType.Interval)
{
    DefaultValue = TimeSpan.FromDays(2)
});
''', '''
migration.Create.Column("Elapsed").OnTable("Jobs").OfType(MigratorDbType.Interval)
    .WithDefaultValue(TimeSpan.FromDays(2));
''')),
    section("Collations and schemas", '<p>Create any ICU nondeterministic collation explicitly, then select it with Collation.Named. Column rendering does not silently create shared collation objects. Binary maps to C; language and case semantics should use a specific installed name.</p><p>Schema-aware metadata does not establish complete qualification for every operation. Test quoted names and search-path behavior with your migration. Renaming a table retains its named constraints; avoid colliding names when recreating the old table.</p>'),
    section("Transactions and coordination", '<p>WholeSession is supported for verified transactional DDL, and DatabaseMigrationLock uses a session advisory lock. Statements that require special transaction treatment need a separate deployment design. Keep the connection stable while the lease is held.</p>'), source="src/Migrator/Providers/Impl/PostgreSQL/PostgreSQLTransformationProvider.cs")

page("Database providers", "mysql", "MySQL and MariaDB", "Related providers with explicit engine, collation and DDL transaction differences.",
    section("Choose the matching dialect", '<p>Select ProviderTypes.Mysql for MySQL and MariaDB for MariaDB. Use an open driver connection or configure the factory. Do not treat compatible wire protocols as proof of identical server syntax or metadata behavior. DDL can commit implicitly; the runner rejects WholeSession for these dialects.</p>'),
    section("Select a supported collation", '<p>Semantic presets require utf8mb4-compatible text and the documented server versions: MySQL 8 and MariaDB 10.10+ have different mappings. Use a named installed collation if exact linguistic or trailing-space behavior matters.</p>', pair("Case-insensitive, accent-sensitive text", '''
Database.AddTable("Labels", new Column("Name", DbType.String, 100)
{
    Collation = Collation.CaseInsensitive
});
''', '''
migration.Create.Table("Labels").WithColumn("Name").AsString(100)
    .WithCollation(Collation.CaseInsensitive);
''')),
    section("Constraint metadata", '<p>MySQL reports primary keys as PRIMARY even if the migration supplied a symbolic name. MySQL/MariaDB catalogs expose unique indexes as unique constraints, so metadata cannot recover every original CREATE UNIQUE INDEX versus UNIQUE-clause choice. Do not derive ownership from that distinction.</p>'),
    section("Locking and values", '<p>DatabaseMigrationLock uses named session locks. These coordinate one server, not a distributed cluster. Interval values use signed .NET ticks. String overflow behavior depends on SQL mode; boundary CI uses STRICT_ALL_TABLES. Check server settings when evaluating length and decimal errors.</p>'), source="src/Migrator/Providers/Impl/Mysql/MySqlTransformationProvider.cs")

page("Database providers", "oracle", "Oracle", "Preserve explicit constraints and be deliberate about identity, sequences and implicit DDL commits.",
    section("Connection and schema", '<p>Use ProviderTypes.Oracle with the Oracle managed ADO.NET driver and the intended schema. MsOracle is a historical variant. Oracle DDL is not generally atomic across a migration; WholeSession is rejected. Some quoted qualified metadata lookups are explicitly rejected.</p>'),
    section("Create an identity definition", '<p>Identity is a column attribute and is validated before table creation. It need not be a primary key on every engine, but the example pairs it with an explicit key. Use a server/driver combination qualified for native identity.</p>', pair("An identity table with an explicit key", '''
Database.AddTable("Entries",
    new Column("Id", DbType.Int32) { IsIdentity = true, IsNullable = false },
    new Column("Text", DbType.String, 255),
    new PrimaryKeyConstraint("PK_Entries", "Id"));
''', '''
migration.Create.Table("Entries")
    .WithColumn("Id").AsInt32().Identity().NotNullable()
    .WithColumn("Text").AsString(255)
    .WithPrimaryKey("PK_Entries", "Id");
''', smoke=True)),
    section("Object cleanup", '<p>RemoveTable leaves unrelated sequences intact. Oracle removes table-owned triggers and native identity objects. For a legacy sequence that the migration explicitly owns, OracleTransformationProvider.RemoveTableWithOwnedSequences validates named sequences and propagates cleanup errors. It does not infer sequence ownership from naming patterns.</p>'),
    section("Values and options", '<p>Oracle empty character strings become NULL. Time uses DATE with a fixed 1970-01-01 date and whole-second precision; fractional Time inputs are rejected. Intervals use native storage. Changes that require an unsupported in-place type conversion need an explicit data migration.</p><p>Included/clustered index options are rejected rather than ignored. Ordered foreign-key pairs and delete actions are preserved by structured metadata. A SQL Server clustered-index request is not translated into an Oracle index-organized table.</p>'), source="src/Migrator/Providers/Impl/Oracle/OracleTransformationProvider.cs")

page("Database providers", "other-providers", "HANA and additional providers", "Use the live-engine matrix to qualify operations beyond the common database families.",
    section("SAP HANA", '<p>ProviderTypes.Hana uses SAP’s native .NET driver. The CI job runs HANA Express and exercises schema/data operations, metadata, constraints, history, restart and DML rollback. DDL may autocommit. Use a custom host; the CLI driver bundle does not include an online HANA host.</p><p>HANA has its own supported type set; Guid and DateTimeOffset are outside the current matrix mappings. Review the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/data-type-boundary-tests.md">type matrix</a> before choosing shared column definitions.</p>'),
    section("Db2, Informix, Firebird and Sybase", table(["Provider", "Things to check"], [["Db2 LUW", "Driver runtime dependencies, decimal/storage capacity and ordinary/unique index options."], ["Informix", "Native driver and database encoding; TEXT reads, integer NULL sentinels, whole-second Time and trailing-space trimming."], ["Firebird", "Decimal storage capacity, ordinary/unique index operations and transaction behavior."], ["Sybase ASE", "TEXTSIZE and string truncation settings, nullable BIT restrictions, trimmed strings and constraint-name limitations."]])),
    section("A shared table definition", '<p>The same authoring API describes a portable subset. This does not imply that every native extension or type maps identically. Start with simple definitions, then qualify your actual data and schema operations on each target.</p>', CREATE_USERS),
    section("Source inventory and evidence", '<p>Ingres remains a source dialect outside the eleven-engine matrix. Redshift, Snowflake and Db2 for IBM i require separate provider/infrastructure qualification; PostgreSQL tests do not qualify Redshift, and Db2 LUW tests do not qualify IBM i.</p><p>See <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/additional-database-qualification.md">qualification requirements</a> and <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/live-database-tests.md">live test setup</a> for exact coverage and reproduction commands.</p>'), source="src/Migrator/ProviderFactory.cs")

page("Advanced topics", "conditional", "Conditional logic", "Choose between inspecting the live schema and declaring a provider-specific operation.",
    section("Provider-specific operations", '<p>The Classic provider indexer selects a named provider or a no-op provider. Fluent IfProvider wraps structured operations in a provider condition. Use the provider names understood by the dialect; this SQLite example leaves other providers unchanged.</p>', pair("Run a SQLite-specific statement", '''
Database["SQLite"].ExecuteNonQuery("UPDATE Users SET Name = upper(Name)");
''', '''
migration.IfProvider("SQLite", sqlite =>
    sqlite.Execute.Sql("UPDATE Users SET Name = upper(Name)"));
''')),
    section("Schema-dependent decisions", '<p>Use Database.TableExists/ColumnExists or FluentMigration.Schema for connected checks. These inspect the current database. A fluent BuildUp method collects operations before they execute, so queued creation is not visible to a live metadata read in the same method.</p><p>For execution-time decisions after earlier operations, use an explicit provider callback. That callback cannot be previewed and requires an authored reverse. Avoid making a migration silently succeed with the wrong schema: an existence check alone does not validate a column’s type or constraint definition.</p>'), source="src/Migrator/Framework/Fluent/MigrationBuilder.cs")

page("Advanced topics", "extensions", "Custom extensions", "Reuse schema conventions without hiding provider behavior or changing the migration contract.",
    section("Share a schema convention", '<p>A small helper can express a repeated column policy in both styles. Keep helper behavior stable for historical migrations; changing a helper can change what an old migration does on a fresh database. The example uses static methods to keep its dependencies explicit.</p>', pair("Reusable audit-column helpers", '''
public static class AuditColumns
{
    public static void Add(ITransformationProvider database, string table)
        => database.AddColumn(table, new Column("CreatedAt", DbType.DateTime)
        {
            IsNullable = false, DefaultValue = RawSql.Insert("CURRENT_TIMESTAMP")
        });
}
''', '''
public static class AuditColumns
{
    public static void Add(MigrationBuilder migration, string table)
        => migration.Create.Column("CreatedAt").OnTable(table).OfType(DbType.DateTime)
            .NotNullable().WithDefaultValue(RawSql.Insert("CURRENT_TIMESTAMP"));
}
''', kind="class")),
    section("Custom activation and locks", '<p>RunnerOptions.Activator constructs migrations when a DI container is not appropriate. IMigrationLock supplies a disposable lease for custom deployment coordination. Release must work on success and failure. Configure these at the host boundary rather than in individual migrations.</p>'),
    section("Provider authors", '<p>ITransformationProvider defines execution and metadata operations. A custom provider needs accurate typed constraint definitions or an explicit unsupported error. IMigrationHistory enables read-only planning and effective-scope history selection. IScriptBatchProvider extends script processing.</p><p>Keep SQL rendering independent of a live connection. Custom MigrationOperation implementations need deliberate validation, application, SQL rendering and reversal behavior. See the <a href="api-map.html">API map</a> and implementation contracts before claiming preview or reversal support.</p>'), source="src/Migrator/Framework/Fluent/Operations.cs")

page("Advanced topics", "testing", "Testing and deployment", "Verify stored data, preserved schema and repeat execution on the actual target engine.",
    section("Test a migration lifecycle", '<p>Create a disposable database, apply the migration, check the schema and rows, run to the same target again, then downgrade and verify the intended reverse. Both authoring styles use the same runner. This fragment assumes an initialized runner whose migration set creates Users.</p>', pair("A host-level smoke check", '''
runner.MigrateToLastVersion();
if (!provider.TableExists("Users")) throw new Exception("Users missing");
runner.MigrateToLastVersion();
runner.MigrateTo(0);
if (provider.TableExists("Users")) throw new Exception("Users was not removed");
''', kind="host")),
    section("What to assert", '<p>Test defaults by omitting a value, and nullability by explicitly sending NULL. Verify composite-key order, foreign-key actions and constraint names. After a SQLite rebuild check real rows, collations, supported indexes/triggers and identity high-water state. Test failure paths as well as successful SQL generation.</p><p>Use representative production-sized data to measure lock duration and backfill cost. A passing SQL-string assertion does not establish that a database accepts a command or preserves its semantics.</p>'),
    section("Repository checks", '<p>Build with dotnet build Migrator.slnx, then use .github/scripts/test.ps1 -Database Unit or SQLite for local suites. The <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/live-database-tests.md">live-engine guide</a> gives the external database setup. <a href="../index.html#test-results">Homepage CI results</a> include commit provenance and skipped/missing-suite status.</p>'),
    section("Production rollout", '<p>Keep applied migrations immutable, review SQL and explicit reverse behavior, and serialize competing deploys. Validate against a restored database before making a breaking change. Plan application compatibility around expand/backfill/contract phases. Treat post-commit callback failures as durable migrations requiring follow-up handling.</p>'), source=".github/workflows/dotnetpull.yml")

page("Advanced topics", "upgrading", "Upgrading existing migrations", "Update source definitions while preserving the history your databases already contain.",
    section("Explicit column attributes", '<p>Replace old ColumnProperty flags with IsNullable, IsIdentity and IsUnsigned. Primary, unique, foreign and check constraints belong to the table. GetColumns returns column attributes; use GetTableConstraints for key membership and ordered columns.</p><p>Keep the same applied migration versions and effective scope when recompiling. Do not create a new history table merely to make an incompatible source assembly run. Verify the upgrade against a restored database and a fresh database.</p>'),
    section("One fluent authoring surface", '<p>FluentMigration.BuildUp/BuildDown replaces the duplicate legacy builder. Use complete table definitions for keys, explicit operations for indexes, and independent foreign-key update/delete actions. Classic Up/Down migrations remain first-class.</p>', CREATE_USERS),
    section("Behavior changes to review", '<p>Column changes preserve explicit uniqueness; old SQL Server ownership markers no longer control deletion. TimeSpan inputs mean intervals, so convert clock-time inputs to TimeOnly. SQLite GUID defaults use the same blob representation as inserted parameters; unrelated rebuilds preserve existing text defaults.</p><p>Read the complete <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/migration-guide-12.1-to-13.md">compatibility migration guide</a> for constructor replacements, custom-provider contracts, identity, constraint metadata and collation mappings. Version-specific details live there; these chapters describe the current API.</p>'), source="docs/migration-guide-12.1-to-13.md")

page("Reference", "api-map", "Classic / Fluent API map", "A practical index of the two authoring surfaces and their shared provider contracts.",
    section("Schema operations", table(["Classic", "Fluent"], [["AddTable", "Create.Table"], ["AddColumn(table, column)", "Create.Column(name).OnTable(table)"], ["ChangeColumn(table, column)", "Alter.Column(name).OnTable(table) / Alter.Column(definition).OnTable(table)"], ["RemoveTable / RemoveColumn", "Delete.Table(table) / Delete.Column(name).FromTable(table)"], ["RenameTable / RenameColumn", "Rename.Table(old).To(new) / Rename.Column(old).OnTable(table).To(new)"], ["AddPrimaryKey / AddUniqueConstraint / AddCheckConstraint", "Create.PrimaryKey(name).OnTable(table).WithColumns(...) / Create.UniqueConstraint / Create.CheckConstraint"], ["AddForeignKey / RemoveForeignKey", "Create.ForeignKey(name).FromTable(child).WithColumns(...).ToTable(parent).WithColumns(...) / Delete.ForeignKey(name).FromTable(table)"], ["AddIndex / RemoveIndex", "Create.Index(name).OnTable(table).WithColumns(...) / Delete.Index(name).FromTable(table)"], ["GetTableConstraints / GetColumns", "Schema.Table(name).ConstraintDefinitions() / Columns()"]])),
    section("Data and execution", table(["Classic", "Fluent"], [["Insert / InsertIfNotExists", "Insert.IntoTable(...).Row(...) / IfNotExists(...)"], ["Update / Delete", "Update.Table(...).Set(...).Where(...) / Delete.FromTable(...).Where(...)"], ["ExecuteNonQuery / ExecuteScript / ExecuteResourceScript", "Execute.Sql / Execute.Script / Execute.EmbeddedScript"], ["CopyDataFromTableToTable / UpdateTargetFromSource", "Execute.CopyDataFromTable(source).ToTable(target).WithColumns(...) / Execute.UpdateTable(target).FromTable(source).Set(...).Match(...)"], ["TruncateTable", "Execute.Truncate"], ["CreateCommand / Connection", "Execute.WithCommand / Execute.WithConnection"], ["Database provider access", "Context or Execute.WithProvider"], ["History / transactions", "Shared runner and explicit provider context"]])),
    section("Execution is not preview or reversal", '<p>Both APIs reach the same provider layer, but not every operation has SQL-preview or automatic-reversal support. Provider capabilities still govern execution. Read <a href="preview.html">preview</a>, <a href="auto-reversing.html">reversal</a> and the <a href="https://github.com/dotnetprojects/Migrator.NET/blob/master/docs/fluent-operation-coverage.md">machine-checked method-family inventory</a> for the distinction.</p>'), source="src/Migrator/Framework/Fluent/MigrationBuilder.cs")

page("Reference", "contributing", "Contributing", "Make a provider change reproducible, then verify its observable behavior.",
    section("A useful report", '<p>Include package, database and driver versions, a minimal migration, relevant schema/data, and expected versus actual behavior. Remove secrets from connection strings and logs. File reports in the <a href="https://github.com/dotnetprojects/Migrator.NET/issues">issue tracker</a>.</p>'),
    section("A focused change", '<p>Add a regression that fails before the fix and checks the real result afterward. Provider-specific changes need actual-engine evidence; skipped tests and generated SQL alone do not qualify support. Keep mutable definition inputs independent from caller arrays and check failure paths.</p>'),
    section("Documentation changes", '<p>Edit docs/_src/content.py for chapters and docs/_src/home.html for the homepage. Run python .github/scripts/build-docs.py to regenerate static HTML and the search index. Run python .github/scripts/verify-docs.py --compile to validate links, paired examples and compilable C# samples. Site assets live in docs/assets.</p><p>Each migration-operation example should provide Classic and Fluent versions. Shared runner and shell commands intentionally appear in both tabs. Keep provider restrictions precise and features described in the present tense. Run the homepage CI-count renderer tests when changing the site template.</p>'),
    section("Design references", '<p>The chapter organization follows the learning path of <a href="https://fluentmigrator.github.io/intro/quick-start.html">FluentMigrator’s documentation</a>, adapted to this API. Visual references include <a href="https://resend.com/">Resend’s typography and code tabs</a> and <a href="https://www.geldata.com/">Gel’s code walkthroughs</a>. The site uses its own palette, layout, copy and migration illustrations.</p>'), source="docs/_src/content.py")
