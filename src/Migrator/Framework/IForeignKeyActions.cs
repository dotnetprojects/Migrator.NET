namespace DotNetProjects.Migrator.Framework;
public interface IForeignKeyActions
{
    void AddForeignKey(string name, string childTable, string[] childColumns, string parentTable, string[] parentColumns,
        ForeignKeyConstraintType onDelete, ForeignKeyConstraintType onUpdate);
}
