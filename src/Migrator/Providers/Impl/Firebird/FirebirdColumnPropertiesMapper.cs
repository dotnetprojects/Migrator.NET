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


        var vals = new List<string>();

        AddName(vals);

        AddType(vals);
        AddCollation(column, vals);
        AddUnsigned(column, vals);

        AddIdentity(column, vals);

        AddIdentityAgain(column, vals);


        AddDefaultValue(column, vals);

        AddNotNull(column, vals);

        AddNull(column, vals);

        _ColumnSql = string.Join(" ", vals.ToArray());
    }
}
