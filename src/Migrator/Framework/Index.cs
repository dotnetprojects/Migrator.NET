namespace Migrator.Framework;

public class Index : IDbField
{
    public string Name { get; set; }
    public bool Unique { get; set; }
    public bool Clustered { get; set; }
    public bool PrimaryKey { get; set; }
    public bool UniqueConstraint { get; set; }
    public string[] KeyColumns { get; set; }
    public string[] IncludeColumns { get; set; }
}
