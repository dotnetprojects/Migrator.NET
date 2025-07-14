using System.Collections.Generic;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite.Models;

public class ForeignKeyExtract
{
    /// <summary>
    /// Gets or sets the complete foreign key string - CONSTRAINT MyFKName FOREIGN KEY (asdf, asdf) REFERENCES ParentTable(asdf,asdf)
    /// </summary>
    public string ForeignKeyString { get; set; }

    /// <summary>
    /// Gets or sets the foreign key name
    /// </summary>
    public string ForeignKeyName { get; set; }

    /// <summary>
    /// Gets or sets the child column names.
    /// </summary>
    public List<string> ChildColumnNames { get; set; }

    /// <summary>
    /// Gets or sets the parent column names.
    /// </summary>
    public List<string> ParentColumnNames { get; set; }
}