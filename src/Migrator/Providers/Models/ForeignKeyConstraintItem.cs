namespace DotNetProjects.Migrator.Providers.Models;

public class ForeignKeyConstraintItem
{
    public string SchemaName { get; set; }
    public string ForeignKeyName { get; set; }
    public string ChildTableName { get; set; }
    public string ChildColumnName { get; set; }
    public string ParentTableName { get; set; }
    public string ParentColumnName { get; set; }
}