using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle;

internal static class OracleCatalog
{
    internal static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

    internal static string Predicate(ITransformationProvider provider, string table, string tableColumn = "TABLE_NAME", string ownerColumn = "OWNER")
    {
        var name = SqlIdentifier.Catalog(provider.QuoteTableNameIfRequired(table), true);
        return tableColumn + "=" + Literal(name.Name) + " AND " + ownerColumn + "=" +
            (name.Schema == null ? "SYS_CONTEXT('USERENV','CURRENT_SCHEMA')" : Literal(name.Schema));
    }
}
