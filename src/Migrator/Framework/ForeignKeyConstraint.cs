using Migrator.Framework;

namespace DotNetProjects.Migrator.Framework;

public class ForeignKeyConstraint : IDbField
{
    public ForeignKeyConstraint()
    { }

    public ForeignKeyConstraint(string name, string table, string[] columns, string pkTable, string[] pkColumns)
    {
        Name = name;
        Table = table;
        Columns = columns;
        PkTable = pkTable;
        PkColumns = pkColumns;
    }

    /// <summary>
    /// Gets or sets the Id of the FK. This is not the name of the FK.
    /// Currently used for SQLite
    /// </summary>
    public int? Id { get; set; }
    public string Name { get; set; }
    public string Table { get; set; }
    public string[] Columns { get; set; }
    public string PkTable { get; set; }
    public string[] PkColumns { get; set; }

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
