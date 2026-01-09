using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;

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


    protected virtual void AddValueIfSelected(Column column, ColumnProperty property, ICollection<string> vals)
    {
        vals.Add(_Dialect.SqlForProperty(property, column));
    }
}