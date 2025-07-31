using System;
using System.Collections.Generic;
using System.Data;
using Migrator.Framework;

namespace Migrator.Providers
{
    /// <summary>
    /// Defines the implementations specific details for a particular database.
    /// </summary>
    public abstract class Dialect : IDialect
    {
        readonly Dictionary<ColumnProperty, string> propertyMap = [];
        readonly HashSet<string> reservedWords = [];
        readonly TypeNames typeNames = new();
        readonly List<DbType> unsignedCompatibleTypes = [];

        protected Dialect()
        {
            RegisterProperty(ColumnProperty.Null, "NULL");
            RegisterProperty(ColumnProperty.NotNull, "NOT NULL");
            RegisterProperty(ColumnProperty.Unique, "UNIQUE");
            RegisterProperty(ColumnProperty.PrimaryKey, "PRIMARY KEY");
            RegisterProperty(ColumnProperty.PrimaryKeyNonClustered, " NONCLUSTERED");
        }

        public virtual int MaxKeyLength
        {
            get { return 900; }
        }

        public virtual int MaxFieldNameLength
        {
            get { return int.MaxValue; }
        }

        public virtual bool ColumnNameNeedsQuote
        {
            get { return false; }
        }

        public virtual bool TableNameNeedsQuote
        {
            get { return false; }
        }

        public virtual bool ConstraintNameNeedsQuote
        {
            get { return false; }
        }

        public virtual bool IdentityNeedsType
        {
            get { return true; }
        }
        public virtual bool SupportsNonClustered
        {
            get { return false; }
        }

        public virtual bool NeedsNotNullForIdentity
        {
            get { return true; }
        }

        public virtual bool SupportsIndex
        {
            get { return true; }
        }

        public virtual string QuoteTemplate
        {
            get { return "\"{0}\""; }
        }

        public virtual bool NeedsNullForNullableWhenAlteringTable
        {
            get { return false; }
        }

        protected void AddReservedWord(string reservedWord)
        {
            reservedWords.Add(reservedWord.ToUpperInvariant());
        }

        protected void AddReservedWords(params string[] words)
        {
            if (words == null) return;
            foreach (var word in words) reservedWords.Add(word);
        }

        public virtual bool IsReservedWord(string reservedWord)
        {
            if (string.IsNullOrEmpty(reservedWord)) throw new ArgumentNullException("reservedWord");

            if (reservedWords == null) return false;

            var isReserved = reservedWords.Contains(reservedWord.ToUpperInvariant());

            if (isReserved)
            {
                //Console.WriteLine("Reserved word: {0}", reservedWord);
            }

            return isReserved;
        }

        public abstract ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString, string defaultSchema, string scope, string providerName);
        public abstract ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection, string defaultSchema, string scope, string providerName);

        public ITransformationProvider NewProviderForDialect(string connectionString, string defaultSchema, string scope, string providerName)
        {
            return GetTransformationProvider(this, connectionString, defaultSchema, scope, providerName);
        }

        public ITransformationProvider NewProviderForDialect(IDbConnection connection, string defaultSchema, string scope, string providerName)
        {
            return GetTransformationProvider(this, connection, defaultSchema, scope, providerName);
        }

        /// <summary>
        /// Subclasses register a typename for the given type code and maximum
        /// column length. <c>$l</c> in the type name will be replaced by the column
        /// length (if appropriate)
        /// </summary>
        /// <param name="code">The typecode</param>
        /// <param name="capacity">Maximum length of database type</param>
        /// <param name="name">The database type name</param>
        protected void RegisterColumnType(DbType code, int capacity, string name)
        {
            typeNames.Put(code, capacity, name);
        }

        /// <summary>
        /// Subclasses register a typename for the given type code and maximum
        /// column length. <c>$l</c> in the type name will be replaced by the column
        /// length (if appropriate)
        /// </summary>
        /// <param name="code">The typecode</param>
        /// <param name="capacity">Maximum length of database type</param>
        /// <param name="name">The database type name</param>
        protected void RegisterColumnType(MigratorDbType code, int capacity, string name)
        {
            typeNames.Put(code, capacity, name);
        }

        /// <summary>
        /// Subclasses register a typename for the given type code and maximum
        /// column length. <c>$l</c> in the type name will be replaced by the column
        /// length (if appropriate)
        /// <c>$2</c> in the type name will be replaced by the column
        /// precision (if appropriate)
        /// </summary>
        /// <param name="code">The typecode</param>
        /// <param name="capacity">Maximum length of database type</param>
        /// <param name="name">The database type name</param>
        protected void RegisterColumnTypeWithPrecision(DbType code, string name)
        {
            typeNames.Put(code, -1, name);
        }

        /// <summary>
        /// Suclasses register a typename for the given type code. <c>$l</c> in the 
        /// typename will be replaced by the column length (if appropriate).
        /// </summary>
        /// <param name="code">The typecode</param>
        /// <param name="name">The database type name</param>
        protected void RegisterColumnType(MigratorDbType code, string name)
        {
            typeNames.Put(code, name);
        }

        /// <summary>
        /// Suclasses register a typename for the given type code. <c>$l</c> in the 
        /// typename will be replaced by the column length (if appropriate).
        /// </summary>
        /// <param name="code">The typecode</param>
        /// <param name="name">The database type name</param>
        protected void RegisterColumnType(DbType code, string name)
        {
            typeNames.Put(code, name);
        }

        /// <summary>
        /// Suclasses register a typename for the given type code.
        /// <c>{length}</c>, <c>{precision}</c> & <c>{scale}</c> in the 
        /// typename will be replaced.
        // /// </summary>
        /// <param name="code">The typecode</param>
        /// <param name="name">The database type name</param>
        protected void RegisterColumnTypeWithParameters(DbType code, string name)
        {
            typeNames.PutParametrized(code, name);
        }


        protected void RegisterColumnTypeAlias(DbType code, string alias)
        {
            typeNames.PutAlias(code, alias);
        }

        public virtual ColumnPropertiesMapper GetColumnMapper(Column column)
        {
            var type = column.Size > 0 ? GetTypeName(column.Type, column.Size) : GetTypeName(column.Type);

            if (column.Precision.HasValue || column.Scale.HasValue)
            {
                type = GetTypeNameParametrized(column.Type, column.Size, column.Precision ?? 0, column.Scale ?? 0);
            }

            if (!IdentityNeedsType && column.IsIdentity)
            {
                type = String.Empty;
            }

            return new ColumnPropertiesMapper(this, type);
        }

        public virtual DbType GetDbTypeFromString(string type)
        {
            return typeNames.GetDbType(type);
        }

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        public virtual string GetTypeName(DbType type)
        {
            var result = typeNames.Get(type);

            if (result == null)
            {
                throw new Exception(string.Format("No default type mapping for DbType {0}", type));
            }

            return result;
        }

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        /// <param name="length"></param>
        public virtual string GetTypeName(DbType type, int length)
        {
            return GetTypeName(type, length, 0, 0);
        }

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        /// <param name="length"></param>
        /// <param name="precision"></param>
        /// <param name="scale"></param>
        public virtual string GetTypeName(DbType type, int length, int precision, int scale)
        {
            var resultWithLength = typeNames.Get(type, length, precision, scale);
            if (resultWithLength != null)
                return resultWithLength;

            return GetTypeName(type);
        }

        /// <summary>
        /// Get the name of the database type associated with the given 
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>The database type name used by ddl.</returns>
        /// <param name="length"></param>
        /// <param name="precision"></param>
        /// <param name="scale"></param>
        public virtual string GetTypeNameParametrized(DbType type, int length, int precision, int scale)
        {
            var result = typeNames.GetParametrized(type);
            if (result != null)
                return result.Replace("{length}", length.ToString())
                    .Replace("{precision}", precision.ToString())
                    .Replace("{scale}", scale.ToString());
            return GetTypeName(type, length, precision, scale);
        }

        /// <summary>
        /// <para>Get the type from the specified database type name.</para>
        /// <para>Note: This does not work perfectly, but it will do for most cases.</para>
        /// </summary>
        /// <param name="databaseTypeName">The name of the type.</param>
        /// <returns>The <see cref="DbType"/>.</returns>
        public virtual DbType GetDbType(string databaseTypeName)
        {
            return typeNames.GetDbType(databaseTypeName);
        }

        public void RegisterProperty(ColumnProperty property, string sql)
        {
            if (!propertyMap.ContainsKey(property))
            {
                propertyMap.Add(property, sql);
            }
            propertyMap[property] = sql;
        }

        public virtual string SqlForProperty(ColumnProperty property, Column column)
        {
            if (propertyMap.ContainsKey(property))
            {
                return propertyMap[property];
            }
            return String.Empty;
        }

        public virtual string Quote(string value)
        {
            return String.Format(QuoteTemplate, value);
        }

        public virtual string Default(object defaultValue)
        {
            if (defaultValue is String && defaultValue.ToString() == String.Empty)
            {
                defaultValue = "''";
            }
            else if (defaultValue is Guid)
            {
                return String.Format("DEFAULT '{0}'", defaultValue.ToString());
            }
            else if (defaultValue is DateTime)
            {
                return String.Format("DEFAULT '{0}'", ((DateTime)defaultValue).ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else if (defaultValue is String)
            {
                defaultValue = ((String)defaultValue).Replace("'", "''");
                defaultValue = "'" + defaultValue + "'";
            }

            return String.Format("DEFAULT {0}", defaultValue);
        }

        public ColumnPropertiesMapper GetAndMapColumnProperties(Column column)
        {
            var mapper = GetColumnMapper(column);
            mapper.MapColumnProperties(column);
            if (column.DefaultValue != null && column.DefaultValue != DBNull.Value)
                mapper.Default = column.DefaultValue;
            return mapper;
        }

        public ColumnPropertiesMapper GetAndMapColumnPropertiesWithoutDefault(Column column)
        {
            var mapper = GetColumnMapper(column);
            mapper.MapColumnPropertiesWithoutDefault(column);
            if (column.DefaultValue != null && column.DefaultValue != DBNull.Value)
                mapper.Default = column.DefaultValue;
            return mapper;
        }

        /// <summary>
        /// Subclasses register which DbTypes are unsigned-compatible (ie, available in signed and unsigned variants)
        /// </summary>
        /// <param name="type"></param>
        protected void RegisterUnsignedCompatible(DbType type)
        {
            unsignedCompatibleTypes.Add(type);
        }

        /// <summary>
        /// Determine if a particular database type has an unsigned variant
        /// </summary>
        /// <param name="type">The DbType</param>
        /// <returns>True if the database type has an unsigned variant, otherwise false</returns>
        public bool IsUnsignedCompatible(DbType type)
        {
            return unsignedCompatibleTypes.Contains(type);
        }

    }
}
