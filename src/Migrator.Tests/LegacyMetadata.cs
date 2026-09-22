using DotNetProjects.Migrator.Framework;

namespace Migrator.Tests;

// These tests deliberately verify the compatibility metadata API. Keep the
// obsolete calls here so other accidental obsolete API usage still warns.
internal static class LegacyMetadata
{
#pragma warning disable CS0618 // Explicit compatibility coverage of these three APIs.
    internal static Column[] ReadLegacyColumns(this ITransformationProvider provider, string table) =>
        provider.GetColumns(table);

    internal static Column ReadLegacyColumn(this ITransformationProvider provider, string table, string column) =>
        provider.GetColumnByName(table, column);

    internal static void RemoveLegacyConstraints(this ITransformationProvider provider, string table) =>
        provider.RemoveAllConstraints(table);
#pragma warning restore CS0618
}
