using DotNetProjects.Migrator.Providers.Models.Indexes;

namespace DotNetProjects.Migrator.Framework;

public class Index : IDbField
{
    public string Name { get; set; }

    public bool Unique { get; set; }

    public bool Clustered { get; set; }

    public bool PrimaryKey { get; set; }

    public bool UniqueConstraint { get; set; }

    public string[] KeyColumns { get; set; } = [];

    public string[] IncludeColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets items that represent filter expressions in filtered indexes. Currently string, integer and boolean values are supported.
    /// </summary>
    public FilterItem[] FilterItems { get; set; } = [];
}
