using System;

namespace Migrator.Framework
{
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
        Null = 1,

        /// <summary>
        /// Null is not allowable
        /// </summary>
        NotNull = 2,

        /// <summary>
        /// Identity column, autoinc
        /// </summary>
        Identity = 4,

        /// <summary>
        /// Unique Column. This is marked being obsolete since you cannot add a name for the constraint which makes it difficult to remove the constraint again.
        /// </summary>
        [Obsolete("Use method 'AddUniqueConstraint' instead. This is marked being obsolete since you cannot add a name for the constraint which makes it difficult to remove the constraint again.")]
        Unique = 8,

        /// <summary>
        /// Indexed Column
        /// </summary>
        Indexed = 16,

        /// <summary>
        /// Unsigned Column
        /// </summary>
        Unsigned = 32,

        CaseSensitive = 64,

        /// <summary>
        /// Foreign Key
        /// </summary>
        ForeignKey = Unsigned | Null,

        /// <summary>
        /// Primary Key
        /// </summary>
        PrimaryKey = 128 | Unsigned | NotNull,

        /// <summary>
        /// Primary key. Make the column a PrimaryKey and unsigned
        /// </summary>
        PrimaryKeyWithIdentity = PrimaryKey | Identity,

        /// <summary>
        /// Primary key. Make the column a PrimaryKey and unsigned
        /// </summary>
        PrimaryKeyNonClustered = 256 | PrimaryKey
    }

    public static class ColumnPropertyExtensions
    {
        public static bool IsSet(this ColumnProperty fruits, ColumnProperty flags)
        {
            return (fruits & flags) == flags;
        }

        public static bool IsNotSet(this ColumnProperty fruits, ColumnProperty flags)
        {
            return (fruits & (~flags)) == 0;
        }

        public static ColumnProperty Set(this ColumnProperty fruits, ColumnProperty flags)
        {
            return fruits | flags;
        }

        public static ColumnProperty Clear(this ColumnProperty fruits, ColumnProperty flags)
        {
            return fruits & (~flags);
        }
    }
}
