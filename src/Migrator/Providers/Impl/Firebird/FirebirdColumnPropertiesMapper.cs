using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Firebird;

public class FirebirdColumnPropertiesMapper : ColumnPropertiesMapper
{
    public FirebirdColumnPropertiesMapper(Dialect dialect, string type)
        : base(dialect, type)
    {
    }

    public override void MapColumnProperties(Column column)
    {
        Name = column.Name;

        _Indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);

        var vals = new List<string>();

        AddName(vals);

        AddType(vals);

        AddIdentity(column, vals);

        AddPrimaryKey(column, vals);

        AddIdentityAgain(column, vals);

        AddUnique(column, vals);

        AddForeignKey(column, vals);

        AddDefaultValue(column, vals);

        AddNotNull(column, vals);

        AddNull(column, vals);

        _ColumnSql = string.Join(" ", vals.ToArray());
    }
}
