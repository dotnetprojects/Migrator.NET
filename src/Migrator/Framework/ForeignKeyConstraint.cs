using Migrator.Framework;

namespace DotNetProjects.Migrator.Framework;

public class ForeignKeyConstraint : IDbField
{
    public ForeignKeyConstraint()
    { }

    public ForeignKeyConstraint(string name, string table, string[] columns, string pkTable, string[] pkColumns, string stringId = null)
    {
        StringId = stringId;
        Name = name;
        Table = table;
        Columns = columns;
        PkTable = pkTable;
        PkColumns = pkColumns;
    }

    /// <summary>
    /// Gets or sets the Id of the FK. This is not the name of the FK.
    /// SQLite: 
    /// </summary>
    public string StringId { get; set; }
    public string Name { get; set; }
    public string Table { get; set; }
    public string[] Columns { get; set; }
    public string PkTable { get; set; }
    public string[] PkColumns { get; set; }
}
