using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
connection.Open();
using var provider = ProviderFactory.Create(ProviderTypes.SQLite, connection, null, "demo");
var runner = new Migrator(provider, false, typeof(CreateUsers));
runner.Options.Tags.Add("core");
runner.Options.TransactionMode = MigrationTransactionMode.WholeSession;
Console.WriteLine(runner.PreviewSql(1, ProviderTypes.SQLite));
if (provider.TableExists("Users") || provider.TableExists(provider.SchemaInfoTable)) throw new Exception("Preview wrote to the database.");
runner.MigrateToLastVersion();
if (!provider.ColumnExists("Users", "Name")) throw new Exception("Migration failed.");
runner.MigrateTo(0);
if (provider.TableExists("Users")) throw new Exception("Automatic reversal failed.");
Console.WriteLine("Quick-start migration, preview and reversal passed.");

[Migration(1, Scope = "demo"), Tags("core")]
public class CreateUsers : AutoReversingMigration
{
    public override void BuildUp(MigrationBuilder migration)
    {
        migration.Create.Table("Users")
            .WithColumn("Id").AsInt32().WithPrimaryKey("PK_Id", "Id")
            .WithColumn("Name").AsString(255).NotNullable();
    }
}
