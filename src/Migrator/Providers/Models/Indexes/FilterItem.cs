using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;

namespace DotNetProjects.Migrator.Providers.Models.Indexes;

public class FilterItem
{
    /// <summary>
    /// Gets or sets the not quoted column name. If the column name is not a reserved word it will be converted to lower cased string in Postgre and to upper cased string in Oracle if you use the default settings.
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the filter.
    /// </summary>
    public FilterType Filter { get; set; }

    /// <summary>
    /// Gets or sets the value used in the comparison. It needs to be a static not dynamic value. Currently we support bool, byte, short, int, long
    /// </summary>
    public object Value { get; set; }
}