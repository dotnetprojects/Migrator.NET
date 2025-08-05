namespace DotNetProjects.Migrator.Framework;

public class ViewColumn : IViewElement
{
    public string Prefix { get; }
    public string ColumnName { get; }

    public ViewColumn(string prefix, string columnName)
    {
        Prefix = prefix;
        ColumnName = columnName;
    }
}
