using System.Collections.Generic;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle;

public class OracleColumnPropertiesMapper : ColumnPropertiesMapper
{
    public OracleColumnPropertiesMapper(Dialect dialect, string typeString) : base(dialect, typeString)
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

        AddUnsigned(column, vals);

        AddPrimaryKey(column, vals);

        AddIdentityAgain(column, vals);

        AddUnique(column, vals);

        AddForeignKey(column, vals);

        AddDefaultValue(column, vals);

        // null / not-null comes last on Oracle - otherwise if use Null/Not-null + default, bad things happen
        // (http://geekswithblogs.net/faizanahmad/archive/2009/08/07/add-new-columnfield-in-oracle-db-table---ora.aspx)

        AddNotNull(column, vals);

        AddNull(column, vals);

        _ColumnSql = string.Join(" ", vals.ToArray());
    }
}