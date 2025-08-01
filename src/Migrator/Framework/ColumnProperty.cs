using System;

namespace Migrator.Framework;

/// <summary>
/// Represents a table column properties.
/// </summary>
[Flags]
public enum ColumnProperty
{
    None = 0,

    /// <summary>
    /// Null is allowable
    /// </summary>
    Null = 1 << 0,

    /// <summary>
    /// Null is not allowable
    /// </summary>
    NotNull = 1 << 1,

    /// <summary>
    /// Identity column, autoinc
    /// </summary>
    Identity = 1 << 2,

    /// <summary>
    /// Unique Column. This is marked being obsolete since you cannot add a name for the constraint which makes it difficult to remove the constraint again.
    /// </summary>
    [Obsolete("Use method 'AddUniqueConstraint' instead. This is marked being obsolete since you cannot add a name for the constraint which makes it difficult to remove the constraint again.")]
    Unique = 1 << 3,

    /// <summary>
    /// Indexed Column
    /// </summary>
    Indexed = 1 << 4,

    /// <summary>
    /// Unsigned Column. Not used in SQLite there is only one integer data type => INTEGER.
    /// </summary>
    Unsigned = 1 << 5,

    /// <summary>
    /// CaseSensitive. Currently only used in SQLite, MySQL and SQL Server
    /// </summary>
    CaseSensitive = 1 << 6,

    // /// <summary>
    // /// Foreign Key
    // /// </summary>
    // [Obsolete("Use method 'AddForeignKey' instead. The flag does not make sense on column level.")]
    // ForeignKey = 1 << 7,

    /// <summary>
    /// Primary Key.
    /// </summary>
    PrimaryKey = 1 << 8,

    /// <summary>
    /// Primary key with identity. This is shorthand for <see cref="PrimaryKey"/> and <see cref="Identity"/>
    /// </summary>
    PrimaryKeyWithIdentity = 1 << 9 | PrimaryKey | Identity,

    /// <summary>
    /// Primary key non clustered. 
    /// </summary>
    PrimaryKeyNonClustered = 1 << 10 | PrimaryKey
}
