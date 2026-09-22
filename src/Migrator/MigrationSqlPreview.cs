using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
namespace DotNetProjects.Migrator;

public static class MigrationSqlPreview
{
    /// <summary>Generates SQL without a database. C# authoring code still executes and must be trusted.</summary>
    public static string Generate(ProviderTypes provider, IEnumerable<(IMigration Migration, bool Up)> migrations,
        bool allowLegacyBodies = false, Func<string, Column[]> existingTables = null)
    {
        var context = new SqlGenerationContext(provider, existingTables);
        var sql = new List<string>();
        foreach (var (migration, up) in migrations)
        {
            var original = migration.Database;
            var proxy = DispatchProxy.Create<ITransformationProvider, PreviewProvider>();
            var recorder = (PreviewProvider)(object)proxy;
            try
            {
                migration.Database = proxy;
                IReadOnlyList<MigrationOperation> operations;
                if (migration is FluentMigration fluent) operations = fluent.GetOperations(up);
                else
                {
                    if (!allowLegacyBodies) throw new NotSupportedException("Imperative SQL preview requires explicit allowLegacyBodies opt-in. Arbitrary C# cannot be sandboxed.");
                    if (up) migration.Up(); else migration.Down();
                    operations = recorder.Operations;
                }
                foreach (var operation in operations) sql.Add(operation.ToSql(context));
            }
            finally { migration.Database = original; }
        }
        return string.Join(Environment.NewLine, sql.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    // Every method is denied unless explicitly mapped to a captured operation. No connection is exposed.
    public class PreviewProvider : DispatchProxy
    {
        internal readonly List<MigrationOperation> Operations = new();
        protected override object Invoke(MethodInfo method, object[] args)
        {
            MigrationOperation operation = method.Name switch
            {
                "AddTable" when args.Length == 2 && args[1] is IDbField[] fields => new CreateTableOperation((string)args[0], null, fields.Select(Definitions.Copy).ToArray()),
                "AddColumn" when args.Length == 2 && args[1] is Column column => new ColumnOperation((string)args[0], Definitions.CopyColumn(column)),
                "RemoveTable" => new RemoveOperation(RemoveKind.Table, (string)args[0]),
                "RenameTable" => new RenameOperation((string)args[0], (string)args[1]),
                "RenameColumn" => new RenameOperation((string)args[0], (string)args[2], (string)args[1]),
                "Insert" when args.Length == 3 && args[1] is string[] columns && args[2] is object[] values => new DataOperation(DataKind.Insert, (string)args[0], (string[])columns.Clone(), (object[])values.Clone()),
                "ExecuteNonQuery" when args.Length == 1 => new SqlOperation((string)args[0]),
                _ => throw new NotSupportedException("SQL preview blocks provider member " + method.Name + ". Use a structured operation or an explicit SQL script.")
            };
            Operations.Add(operation);
            return method.ReturnType == typeof(int) ? 0 : null;
        }
    }
}
