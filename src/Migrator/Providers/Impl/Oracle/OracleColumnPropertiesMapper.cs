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


        var vals = new List<string>();

        AddName(vals);

        AddType(vals);
        AddCollation(column, vals);

        AddIdentity(column, vals);

        AddUnsigned(column, vals);


        AddIdentityAgain(column, vals);


        AddDefaultValue(column, vals);

        // null / not-null comes last on Oracle - otherwise if use Null/Not-null + default, bad things happen
        // (http://geekswithblogs.net/faizanahmad/archive/2009/08/07/add-new-columnfield-in-oracle-db-table---ora.aspx)

        AddNotNull(column, vals);

        AddNull(column, vals);

        _ColumnSql = string.Join(" ", vals.ToArray());
    }
}