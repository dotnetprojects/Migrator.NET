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
    /// Gets or sets the constant comparison value: a string, boolean or integer.
    /// With EqualTo or NotEqualTo, null and DBNull.Value represent IS NULL and IS NOT NULL.
    /// </summary>
    public object Value { get; set; }
}