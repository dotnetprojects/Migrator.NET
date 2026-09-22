using System;
using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers;

/// <summary>Renders column attributes. Keys, constraints and indexes belong to the table definition.</summary>
public class ColumnPropertiesMapper
{
    protected string _ColumnSql;
    protected object _DefaultVal;
    protected Dialect _Dialect;
    protected string _Name;
    public string Type { get; }
    public ColumnPropertiesMapper(Dialect dialect, string typeString) { _Dialect = dialect; Type = typeString; }
    public virtual string ColumnSql => _ColumnSql;
    public string Name { get => _Name; set => _Name = value; }
    public object Default { get => _DefaultVal; set => _DefaultVal = value; }
    public string QuotedName => _Dialect.QuoteIdentifier(Name);
    public virtual void MapColumnProperties(Column column) => Map(column, true);
    public virtual void MapColumnPropertiesWithoutDefault(Column column) => Map(column, false);
    private void Map(Column column, bool includeDefault)
    {
        Name = column.Name;
        var values = new List<string>();
        AddName(values); AddType(values); AddCollation(column, values);
        AddIdentity(column, values); AddUnsigned(column, values);
        AddNotNull(column, values); AddNull(column, values);
        AddIdentityAgain(column, values);
        if (includeDefault) AddDefaultValue(column, values);
        _ColumnSql = string.Join(" ", values);
    }
    protected virtual void AddCollation(Column column, List<string> values)
    {
        if (column.Collation != null)
        {
            if (column.Type is not (System.Data.DbType.String or System.Data.DbType.AnsiString or System.Data.DbType.StringFixedLength or System.Data.DbType.AnsiStringFixedLength))
                throw new NotSupportedException("Collation requires a text column.");
            values.Add(_Dialect.GetCollationSql(column.Collation));
        }
    }
    protected virtual void AddDefaultValue(Column column, List<string> values)
    {
        if (column.DefaultValue != null) values.Add(_Dialect.Default(column.DefaultValue));
    }
    protected virtual void AddIdentity(Column column, List<string> values)
    {
        if (!_Dialect.IdentityNeedsType && column.IsIdentity) values.Add(_Dialect.SqlForColumnAttribute(ColumnAttribute.Identity, column));
    }
    protected virtual void AddIdentityAgain(Column column, List<string> values)
    {
        if (_Dialect.IdentityNeedsType && column.IsIdentity) values.Add(_Dialect.SqlForColumnAttribute(ColumnAttribute.Identity, column));
    }
    protected virtual void AddNull(Column column, List<string> values)
    {
        if (column.IsNullable && _Dialect.NeedsNullForNullableWhenAlteringTable)
            values.Add(_Dialect.SqlForColumnAttribute(ColumnAttribute.Null, column));
    }
    protected virtual void AddNotNull(Column column, List<string> values)
    {
        if (!column.IsNullable) values.Add(_Dialect.SqlForColumnAttribute(ColumnAttribute.NotNull, column));
    }
    protected virtual void AddUnsigned(Column column, List<string> values)
    {
        if (!column.IsUnsigned) return;
        if (!_Dialect.IsUnsignedCompatible(column.Type)) throw new NotSupportedException("Unsigned is unsupported for this column type.");
        var sql = _Dialect.SqlForColumnAttribute(ColumnAttribute.Unsigned, column);
        if (string.IsNullOrWhiteSpace(sql)) throw new NotSupportedException("Unsigned columns are unsupported by this dialect.");
        values.Add(sql);
    }
    protected virtual void AddType(List<string> values) => values.Add(Type);
    protected virtual void AddName(List<string> values) => values.Add(_Dialect.QuoteColumnNameIfRequired(Name));
}
