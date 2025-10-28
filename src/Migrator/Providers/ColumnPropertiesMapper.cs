using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers;

/// <summary>
/// This is basically a just a helper base class
/// per-database implementors may want to override ColumnSql
/// </summary>
public class ColumnPropertiesMapper
{
    /// <summary>
    /// the type of the column
    /// </summary>
    protected string _ColumnSql;

    /// <summary>
    /// Sql if this column has a default value
    /// </summary>
    protected object _DefaultVal;

    protected Dialect _Dialect;

    /// <summary>
    /// Sql if This column is Indexed
    /// </summary>
    protected bool _Indexed;

    /// <summary>The name of the column</summary>
    protected string _Name;

    /// <summary>The SQL type</summary>
    public string Type { get; private set; }

    public ColumnPropertiesMapper(Dialect dialect, string typeString)
    {
        _Dialect = dialect;
        Type = typeString;
    }

    /// <summary>
    /// The sql for this column, override in database-specific implementation classes
    /// </summary>
    public virtual string ColumnSql
    {
        get { return _ColumnSql; }
    }

    public string Name
    {
        get { return _Name; }
        set { _Name = value; }
    }

    public object Default
    {
        get { return _DefaultVal; }
        set { _DefaultVal = value; }
    }

    public string QuotedName
    {
        get { return _Dialect.Quote(Name); }
    }

    public string IndexSql
    {
        get
        {
            if (_Dialect.SupportsIndex && _Indexed)
            {
                return string.Format("INDEX({0})", _Dialect.Quote(_Name));
            }

            return null;
        }
    }

    public virtual void MapColumnProperties(Column column)
    {
        Name = column.Name;

        _Indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);

        var vals = new List<string>();

        AddName(vals);

        AddType(vals);

        AddCaseSensitive(column, vals);

        AddIdentity(column, vals);

        AddUnsigned(column, vals);

        AddNotNull(column, vals);

        AddNull(column, vals);

        AddPrimaryKey(column, vals);

        AddPrimaryKeyNonClustered(column, vals);

        AddIdentityAgain(column, vals);

        AddUnique(column, vals);

        AddForeignKey(column, vals);

        AddDefaultValue(column, vals);

        _ColumnSql = string.Join(" ", vals.ToArray());
    }

    public virtual void MapColumnPropertiesWithoutDefault(Column column)
    {
        Name = column.Name;

        _Indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);

        var vals = new List<string>();

        AddName(vals);

        AddType(vals);

        AddCaseSensitive(column, vals);

        AddIdentity(column, vals);

        AddUnsigned(column, vals);

        AddNotNull(column, vals);

        AddNull(column, vals);

        AddPrimaryKey(column, vals);

        AddIdentityAgain(column, vals);

        AddPrimaryKeyNonClustered(column, vals);

        AddUnique(column, vals);

        AddForeignKey(column, vals);

        _ColumnSql = string.Join(" ", vals.ToArray());
    }

    protected virtual void AddCaseSensitive(Column column, List<string> vals)
    {
        AddValueIfSelected(column, ColumnProperty.CaseSensitive, vals);
    }

    protected virtual void AddDefaultValue(Column column, List<string> vals)
    {
        if (column.DefaultValue != null)
        {
            vals.Add(_Dialect.Default(column.DefaultValue));
        }
    }

    protected virtual void AddForeignKey(Column column, List<string> vals)
    {
        // TODO Does that really make sense?
        // AddValueIfSelected(column, ColumnProperty.ForeignKey, vals);
    }

    protected virtual void AddUnique(Column column, List<string> vals)
    {
        AddValueIfSelected(column, ColumnProperty.Unique, vals);
    }

    protected virtual void AddIdentityAgain(Column column, List<string> vals)
    {
        if (_Dialect.IdentityNeedsType)
        {
            AddValueIfSelected(column, ColumnProperty.Identity, vals);
        }
    }
    protected virtual void AddPrimaryKeyNonClustered(Column column, List<string> vals)
    {
        if (_Dialect.SupportsNonClustered)
        {
            AddValueIfSelected(column, ColumnProperty.PrimaryKeyNonClustered, vals);
        }
    }
    protected virtual void AddPrimaryKey(Column column, List<string> vals)
    {
        AddValueIfSelected(column, ColumnProperty.PrimaryKey, vals);
    }

    protected virtual void AddNull(Column column, List<string> vals)
    {
        if (!PropertySelected(column.ColumnProperty, ColumnProperty.PrimaryKey))
        {
            if (_Dialect.NeedsNullForNullableWhenAlteringTable)
            {
                AddValueIfSelected(column, ColumnProperty.Null, vals);
            }
        }
    }

    protected virtual void AddNotNull(Column column, List<string> vals)
    {
        if (!PropertySelected(column.ColumnProperty, ColumnProperty.Null) && (!PropertySelected(column.ColumnProperty, ColumnProperty.PrimaryKey) || _Dialect.NeedsNotNullForIdentity))
        {
            AddValueIfSelected(column, ColumnProperty.NotNull, vals);
        }
    }

    protected virtual void AddUnsigned(Column column, List<string> vals)
    {
        if (_Dialect.IsUnsignedCompatible(column.Type))
        {
            AddValueIfSelected(column, ColumnProperty.Unsigned, vals);
        }
    }

    protected virtual void AddIdentity(Column column, List<string> vals)
    {
        if (!_Dialect.IdentityNeedsType)
        {
            AddValueIfSelected(column, ColumnProperty.Identity, vals);
        }
    }

    protected virtual void AddType(List<string> vals)
    {
        vals.Add(Type);
    }

    protected virtual void AddName(List<string> vals)
    {
        vals.Add(_Dialect.ColumnNameNeedsQuote || _Dialect.IsReservedWord(Name) ? QuotedName : Name);
    }

    protected virtual void AddValueIfSelected(Column column, ColumnProperty property, ICollection<string> vals)
    {
        if (PropertySelected(column.ColumnProperty, property))
        {
            vals.Add(_Dialect.SqlForProperty(property, column));
        }
    }

    public static bool PropertySelected(ColumnProperty source, ColumnProperty comparison)
    {
        return (source & comparison) == comparison;
    }
}
