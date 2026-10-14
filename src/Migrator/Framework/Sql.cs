namespace DotNetProjects.Migrator.Framework;

/// <summary>A column identifier atom, quoted by the current provider when interpolated into Query.</summary>
public readonly record struct SqlColumn(string Name);

/// <summary>A table identifier path, quoted by the current provider when interpolated into Query.</summary>
public readonly record struct SqlTable(string Name);

/// <summary>Explicit identifier wrappers for interpolated SQL. Ordinary arguments become parameters.</summary>
public static class Sql
{
    public static SqlColumn Col(string name) => new(name);
    public static SqlTable Table(string name) => new(name);
}
