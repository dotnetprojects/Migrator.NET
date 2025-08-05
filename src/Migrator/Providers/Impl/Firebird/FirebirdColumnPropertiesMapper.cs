using System;
using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;

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

        indexed = PropertySelected(column.ColumnProperty, ColumnProperty.Indexed);

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

        columnSql = string.Join(" ", vals.ToArray());
    }
}
