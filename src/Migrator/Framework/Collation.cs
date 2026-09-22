using System;

namespace DotNetProjects.Migrator.Framework;

/// <summary>Comparison intent, resolved by the dialect. Linguistic ordering,
/// normalization and trailing-space behavior remain database-specific.</summary>
public sealed record Collation
{
    public CollationKind Kind { get; }
    public string Name { get; }
    private Collation(CollationKind kind, string name = null) { Kind = kind; Name = name; }
    public static Collation Binary { get; } = new(CollationKind.Binary);
    /// <summary>Unicode, case-sensitive and accent-sensitive comparison.</summary>
    public static Collation CaseSensitive { get; } = new(CollationKind.CaseSensitive);
    /// <summary>Unicode, case-insensitive and accent-sensitive comparison.</summary>
    public static Collation CaseInsensitive { get; } = new(CollationKind.CaseInsensitive);
    /// <summary>Fold ASCII A-Z only. Does not request Unicode case folding.</summary>
    public static Collation AsciiIgnoreCase { get; } = new(CollationKind.AsciiIgnoreCase);
    public static Collation Named(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A collation name is required.", nameof(name));
        return new(CollationKind.Named, name);
    }
    public static implicit operator Collation(string name) => name == null ? null : Named(name);
    public override string ToString() => Name ?? Kind.ToString();
}

public enum CollationKind { Named, Binary, CaseSensitive, CaseInsensitive, AsciiIgnoreCase }
