using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Migrator.Framework
{
	public interface IDialect
	{
        int MaxKeyLength { get; }
        int MaxFieldNameLength { get; }
        bool ColumnNameNeedsQuote { get; }
        bool TableNameNeedsQuote { get; }
        bool ConstraintNameNeedsQuote { get; }
        bool IdentityNeedsType { get; }
        bool NeedsNotNullForIdentity { get; }
        bool SupportsIndex { get; }
        string QuoteTemplate { get; }
        bool NeedsNullForNullableWhenAlteringTable { get; }
        bool IsReservedWord(string reservedWord);
        DbType GetDbTypeFromString(string type);

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        string GetTypeName(DbType type);

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        /// <param name="length"></param>
        string GetTypeName(DbType type, int length);

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        /// <param name="length"></param>
        /// <param name="precision"></param>
        /// <param name="scale"></param>
        string GetTypeName(DbType type, int length, int precision, int scale);

        /// <summary>
        /// <para>Get the type from the specified database type name.</para>
        /// <para>Note: This does not work perfectly, but it will do for most cases.</para>
        /// </summary>
        /// <param name="databaseTypeName">The name of the type.</param>
        /// <returns>The <see cref="DbType"/>.</returns>
        DbType GetDbType(string databaseTypeName);

        void RegisterProperty(ColumnProperty property, string sql);
        string SqlForProperty(ColumnProperty property, Column column);
        string Quote(string value);
        string Default(object defaultValue);
       
        /// <summary>
        /// Determine if a particular database type has an unsigned variant
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>True if the database type has an unsigned variant, otherwise false</returns>
        bool IsUnsignedCompatible(DbType type);
    }
}
