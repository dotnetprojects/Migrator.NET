namespace DotNetProjects.Migrator.Framework;

/// <summary>SQL clauses for column attributes; table constraints are modeled separately.</summary>
public enum ColumnAttribute
{
    Null,
    NotNull,
    Identity,
    Unsigned
}
