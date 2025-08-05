using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite.Models;

public class SQLiteTableInfo
{
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public MappingInfo TableNameMapping { get; set; }

    /// <summary>
    /// Gets or sets the columns of a table
    /// </summary>
    public List<Column> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the indexes of a table.
    /// </summary>
    public List<Index> Indexes { get; set; } = [];

    /// <summary>
    /// Gets or sets the foreign keys of a table.
    /// </summary>
    public List<ForeignKeyConstraint> ForeignKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the column mappings.
    /// </summary>
    public List<MappingInfo> ColumnMappings { get; set; } = [];

    /// <summary>
    /// Gets or sets the unique definitions.
    /// </summary>
    public List<Unique> Uniques { get; set; } = [];
}