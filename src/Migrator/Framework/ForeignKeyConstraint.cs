using Migrator.Framework;

namespace DotNetProjects.Migrator.Framework;

public class ForeignKeyConstraint : IDbField
{
    public ForeignKeyConstraint()
    { }

    public ForeignKeyConstraint(string name, string parentTable, string[] parentcolumns, string childTable, string[] childColumns)
    {
        Name = name;
        ParentTable = parentTable;
        ParentColumns = parentcolumns;
        ChildTable = childTable;
        ChildColumns = childColumns;
    }

    /// <summary>
    /// Gets or sets the Id of the FK. This is not the name of the FK.
    /// Currently used for SQLite
    /// </summary>
    public int? Id { get; set; }
    public string Name { get; set; }
    public string ParentTable { get; set; }
    public string[] ParentColumns { get; set; }
    public string ChildTable { get; set; }
    public string[] ChildColumns { get; set; }

    /// <summary>
    /// Gets or sets the on update text. Currently only used for SQLite.
    /// </summary>
    public string OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the on update text. Currently only used for SQLite.
    /// </summary>
    public string OnUpdate { get; set; }

    /// <summary>
    /// /// Gets or sets the match text. Currently only used for SQLite.
    /// </summary>
    public string Match { get; set; }
}
