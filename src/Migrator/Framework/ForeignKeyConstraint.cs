namespace DotNetProjects.Migrator.Framework;

public class ForeignKeyConstraint : TableConstraint
{
    public ForeignKeyConstraint()
    { }

    public ForeignKeyConstraint(string name, string parentTable, string[] parentcolumns, string childTable, string[] childColumns)
    {
        Name = name;
        ParentTable = parentTable;
        ParentColumns = (string[])parentcolumns.Clone();
        ChildTable = childTable;
        ChildColumns = (string[])childColumns.Clone();
    }

    /// <summary>
    /// Gets or sets the Id of the FK. This is not the name of the FK.
    /// Currently used for SQLite
    /// </summary>
    public int? Id { get; set; }
    public string ParentTable { get; set; }
    public string[] ParentColumns { get; set; }
    public string ChildTable { get; set; }
    public string[] ChildColumns { get; set; }

    /// <summary>
    /// Gets or sets the on delete text. Currently only used for SQLite.
    /// </summary>
    public string OnDelete { get; set; }

    /// <summary>
    /// Gets or sets the on update text. Currently only used for SQLite.
    /// </summary>
    public string OnUpdate { get; set; }

    /// <summary>
    /// Gets or sets the declared match text. Currently only used for SQLite.
    /// SQLite enforces only SIMPLE. Null, empty and the PRAGMA value NONE use
    /// that default; other modes are rejected when creating or rebuilding tables.
    /// </summary>
    public string Match { get; set; }
}
