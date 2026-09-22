namespace DotNetProjects.Migrator.Framework;

/// <summary>A table-level constraint. Column order is significant for keys.</summary>
public abstract class TableConstraint : IDbField
{
    public string Name { get; set; }
}

public sealed class PrimaryKeyConstraint : TableConstraint
{
    public PrimaryKeyConstraint() { }
    public PrimaryKeyConstraint(string name, params string[] columns)
    { Name = name; KeyColumns = (string[])columns.Clone(); }
    public string[] KeyColumns { get; set; } = [];
    public bool NonClustered { get; set; }
}

public class UniqueConstraint : TableConstraint
{
    public UniqueConstraint() { }
    public UniqueConstraint(string name, params string[] columns)
    { Name = name; KeyColumns = (string[])columns.Clone(); }
    public string[] KeyColumns { get; set; } = [];
}
