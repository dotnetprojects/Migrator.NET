namespace DotNetProjects.Migrator.Providers.Impl.SQLite.Models;

public class PragmaIndexListItem
{
    public int Seq { get; set; }

    public string Name { get; set; }

    public bool Unique { get; set; }

    public string Origin { get; set; }

    public bool Partial { get; set; }
}