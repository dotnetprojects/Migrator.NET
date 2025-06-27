namespace DotNetProjects.Migrator.Framework;

public enum ForeignKeyConstraintType
{
    Cascade,
    SetNull,
    NoAction,
    Restrict,
    SetDefault
}