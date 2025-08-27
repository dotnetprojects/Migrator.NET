using System.Collections.Generic;
using DotNetProjects.Migrator.Providers.Models.Indexes;
using DotNetProjects.Migrator.Providers;

namespace DotNetProjects.Migrator.Framework;

public class Index : IDbField
{
    public string Name { get; set; }

    public bool Unique { get; set; }

    /// <summary>
    /// Indicates whether the index is clustered (false for NONCLUSTERED). 
    /// Please mind that this is ignored in Oracle and SQLite (supported in SQLite but not in this migrator)
    /// </summary>
    public bool Clustered { get; set; }

    /// <summary>
    /// Indicates whether it is a primary key constraint. If you want to set a primary key use <see cref="ColumnProperty.PrimaryKey"/> in <see cref="TransformationProvider.AddTable"/>
    /// </summary>
    public bool PrimaryKey { get; internal set; }

    /// <summary>
    /// Indicates whether it is a unique constraint. If you want to set a unique constraint use the method <see cref="TransformationProvider.AddUniqueConstraint"/>
    /// </summary>
    public bool UniqueConstraint { get; internal set; }

    /// <summary>
    /// Gets or sets the column names in the index (not included columns).
    /// </summary>
    public string[] KeyColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets the included columns. Not supported in SQLite and Oracle.
    /// </summary>
    public string[] IncludeColumns { get; set; } = [];

    /// <summary>
    /// Gets or sets items that represent filter expressions in filtered indexes. Currently string, integer and boolean values are supported.
    /// Attention: In SQL Server the column used in the filter must be NOT NULL.
    /// Oracle: Not supported for Oracle
    /// </summary>
    public List<FilterItem> FilterItems { get; set; } = [];
}
