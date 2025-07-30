using Migrator.Framework;

namespace DotNetProjects.Migrator.Framework;

public static class ColumnPropertyExtensions
{
    public static bool IsSet(this ColumnProperty columnProperty, ColumnProperty flags)
    {
        return flags != ColumnProperty.None && (columnProperty & flags) == flags;
    }

    public static bool IsNotSet(this ColumnProperty columnProperty, ColumnProperty flags)
    {
        return (columnProperty & flags) == 0;
    }

    public static ColumnProperty Set(this ColumnProperty columnProperty, ColumnProperty flags)
    {
        return columnProperty | flags;
    }

    public static ColumnProperty Clear(this ColumnProperty columnProperty, ColumnProperty flags)
    {
        return columnProperty & (~flags);
    }
}