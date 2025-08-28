using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;

namespace DotNetProjects.Migrator.Providers.Models;

/// <summary>
/// Model for filter type => filter string mapping.
/// </summary>
public class FilterTypeToString
{
    /// <summary>
    /// Gets or sets the filter type
    /// </summary>
    public FilterType FilterType { get; set; }

    /// <summary>
    /// Gets or sets the filter string like >, <, =, >= etc.
    /// </summary>
    public string FilterString { get; set; }
}