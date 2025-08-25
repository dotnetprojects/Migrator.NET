using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers.Models;
using DotNetProjects.Migrator.Providers.Models.Indexes.Enums;

namespace DotNetProjects.Migrator.Providers;

/// <summary>
/// Defines the implementations specific details for a particular database.
/// </summary>
public abstract class Dialect : IDialect
{
    private readonly Dictionary<ColumnProperty, string> _propertyMap = [];
    private readonly HashSet<string> _reservedWords = [];
    private readonly TypeNames _typeNames = new();
    private readonly List<DbType> _unsignedCompatibleTypes = [];

    private readonly List<FilterTypeToString> _filterTypeToStrings = [
        new() { FilterType = FilterType.EqualTo, FilterString = "=" },
        new() { FilterType = FilterType.GreaterThan, FilterString = ">" },
        new() { FilterType = FilterType.GreaterThanOrEqualTo, FilterString = ">=" },
        new() { FilterType = FilterType.SmallerThan, FilterString = "<" },
        new() { FilterType = FilterType.SmallerThanOrEqualTo, FilterString = "<=" }
    ];

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
        _reservedWords.Add(reservedWord.ToUpperInvariant());
    }

    protected void AddReservedWords(params string[] words)
    {
        if (words == null)
        {
            return;
        }

        foreach (var word in words)
        {
            _reservedWords.Add(word);
        }
    }

    public virtual bool IsReservedWord(string reservedWord)
    {
        if (string.IsNullOrEmpty(reservedWord))
        {
            throw new ArgumentNullException("reservedWord");
        }

        if (_reservedWords == null)
        {
            return false;
        }

        var isReserved = _reservedWords.Contains(reservedWord.ToUpperInvariant());

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
        _typeNames.Put(code, capacity, name);
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
        _typeNames.Put(code, capacity, name);
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
        _typeNames.Put(code, -1, name);
    }

    /// <summary>
    /// Suclasses register a typename for the given type code. <c>$l</c> in the 
    /// typename will be replaced by the column length (if appropriate).
    /// </summary>
    /// <param name="code">The typecode</param>
    /// <param name="name">The database type name</param>
    protected void RegisterColumnType(MigratorDbType code, string name)
    {
        _typeNames.Put(code, name);
    }

    /// <summary>
    /// Suclasses register a typename for the given type code. <c>$l</c> in the 
    /// typename will be replaced by the column length (if appropriate).
    /// </summary>
    /// <param name="code">The typecode</param>
    /// <param name="name">The database type name</param>
    protected void RegisterColumnType(DbType code, string name)
    {
        _typeNames.Put(code, name);
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
        _typeNames.PutParametrized(code, name);
    }


    protected void RegisterColumnTypeAlias(DbType code, string alias)
    {
        _typeNames.PutAlias(code, alias);
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
            type = string.Empty;
        }

        return new ColumnPropertiesMapper(this, type);
    }

    public virtual DbType GetDbTypeFromString(string type)
    {
        return _typeNames.GetDbType(type);
    }

    /// <summary>
    /// Get the name of the database type associated with the given 
    /// </summary>
    /// <param name="type">The DbType</param>
    /// <returns>The database type name used by ddl.</returns>
    public virtual string GetTypeName(DbType type)
    {
        var result = _typeNames.Get(type);

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
        var resultWithLength = _typeNames.Get(type, length, precision, scale);
        if (resultWithLength != null)
        {
            return resultWithLength;
        }

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
        var result = _typeNames.GetParametrized(type);
        if (result != null)
        {
            return result.Replace("{length}", length.ToString())
                .Replace("{precision}", precision.ToString())
                .Replace("{scale}", scale.ToString());
        }

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
        return _typeNames.GetDbType(databaseTypeName);
    }

    public void RegisterProperty(ColumnProperty property, string sql)
    {
        if (!_propertyMap.ContainsKey(property))
        {
            _propertyMap.Add(property, sql);
        }
        _propertyMap[property] = sql;
    }

    public virtual string SqlForProperty(ColumnProperty property, Column column)
    {
        if (_propertyMap.ContainsKey(property))
        {
            return _propertyMap[property];
        }
        return string.Empty;
    }

    public virtual string Quote(string value)
    {
        return string.Format(QuoteTemplate, value);
    }

    public virtual string Default(object defaultValue)
    {
        if (defaultValue is string && defaultValue.ToString() == string.Empty)
        {
            defaultValue = "''";
        }
        else if (defaultValue is Guid)
        {
            var guidValue = string.Format("DEFAULT '{0}'", defaultValue.ToString());

            return guidValue;
        }
        else if (defaultValue is DateTime dateTime)
        {
            if (dateTime.Kind != DateTimeKind.Utc)
            {
                throw new Exception("Use DateTimeKind.Utc for default date time values.");
            }

            return string.Format("DEFAULT '{0}'", ((DateTime)defaultValue).ToString("yyyy-MM-dd HH:mm:ss"));
        }
        else if (defaultValue is string)
        {
            defaultValue = ((string)defaultValue).Replace("'", "''");
            defaultValue = "'" + defaultValue + "'";
        }
        else if (defaultValue is decimal)
        {
            // .ToString("N") does not exist in old .NET version
            defaultValue = Convert.ToString(defaultValue, CultureInfo.InvariantCulture);
        }
        else if (defaultValue is byte[] byteArray)
        {
            var convertedString = BitConverter.ToString(byteArray).Replace("-", "").ToLower();
            defaultValue = $"0x{convertedString}";
        }
        else if (defaultValue is double doubleValue)
        {
            defaultValue = Convert.ToString(doubleValue, CultureInfo.InvariantCulture);
        }

        return string.Format("DEFAULT {0}", defaultValue);
    }

    public ColumnPropertiesMapper GetAndMapColumnProperties(Column column)
    {
        var mapper = GetColumnMapper(column);
        mapper.MapColumnProperties(column);

        if (column.DefaultValue != null && column.DefaultValue != DBNull.Value)
        {
            mapper.Default = column.DefaultValue;
        }

        return mapper;
    }

    public ColumnPropertiesMapper GetAndMapColumnPropertiesWithoutDefault(Column column)
    {
        var mapper = GetColumnMapper(column);
        mapper.MapColumnPropertiesWithoutDefault(column);
        if (column.DefaultValue != null && column.DefaultValue != DBNull.Value)
        {
            mapper.Default = column.DefaultValue;
        }

        return mapper;
    }

    public string GetComparisonStringByFilterType(FilterType filterType)
    {
        var exceptionString = $"The {nameof(FilterType)} '{filterType}' is not implemented.";
        var result = _filterTypeToStrings.FirstOrDefault(x => x.FilterType == filterType) ?? throw new NotImplementedException(exceptionString);

        return result.FilterString;
    }

    /// <summary>
    /// Resolves the comparison string for filtered indexes.
    /// </summary>
    /// <param name="filterType"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public FilterType GetFilterTypeByComparisonString(string comparisonString)
    {
        var exceptionString = $"The {comparisonString} cannot be resolved.";
        var result = _filterTypeToStrings.FirstOrDefault(x => x.FilterString == comparisonString) ?? throw new Exception(exceptionString);

        return result.FilterType;
    }

    /// <summary>
    /// Subclasses register which DbTypes are unsigned-compatible (ie, available in signed and unsigned variants)
    /// </summary>
    /// <param name="type"></param>
    protected void RegisterUnsignedCompatible(DbType type)
    {
        _unsignedCompatibleTypes.Add(type);
    }

    /// <summary>
    /// Determine if a particular database type has an unsigned variant
    /// </summary>
    /// <param name="type">The DbType</param>
    /// <returns>True if the database type has an unsigned variant, otherwise false</returns>
    public bool IsUnsignedCompatible(DbType type)
    {
        return _unsignedCompatibleTypes.Contains(type);
    }

}
