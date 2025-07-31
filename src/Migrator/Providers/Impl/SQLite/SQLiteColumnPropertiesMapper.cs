using System;
using System.Collections.Generic;
using Migrator.Framework;
using Migrator.Providers;

namespace DotNetProjects.Migrator.Providers.Impl.SQLite;

public class SQLiteColumnPropertiesMapper : ColumnPropertiesMapper
{
    public SQLiteColumnPropertiesMapper(Dialect dialect, string type) : base(dialect, type)
    {
    }

    protected override void AddNull(Column column, List<string> vals)
    {
        var isPrimaryKeySelected = PropertySelected(column.ColumnProperty, ColumnProperty.PrimaryKey);
        var isNullSelected = PropertySelected(column.ColumnProperty, ColumnProperty.Null);
        var isNotNullSelected = PropertySelected(column.ColumnProperty, ColumnProperty.NotNull);

        if (isNullSelected || (!isNotNullSelected && !isPrimaryKeySelected))
        {
            AddValueIfSelected(column, ColumnProperty.Null, vals);
        }
    }

    protected override void AddNotNull(Column column, List<string> vals)
    {
        var isPrimaryKeySelected = PropertySelected(column.ColumnProperty, ColumnProperty.PrimaryKey);
        var isNullSelected = PropertySelected(column.ColumnProperty, ColumnProperty.Null);
        var isNotNullSelected = PropertySelected(column.ColumnProperty, ColumnProperty.NotNull);

        if (isNullSelected && isPrimaryKeySelected)
        {
            throw new Exception("This is currently not supported by the migrator see issue #44. You need to use NOT NULL for a PK column.");
        }

        if (isNotNullSelected || isPrimaryKeySelected)
        {
            AddValueIfSelected(column, ColumnProperty.NotNull, vals);
        }
    }

    protected virtual void AddValueIfSelected(Column column, ColumnProperty property, ICollection<string> vals)
    {
        vals.Add(dialect.SqlForProperty(property, column));
    }
}