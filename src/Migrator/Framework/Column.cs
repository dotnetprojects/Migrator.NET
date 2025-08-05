#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System.Data;

namespace DotNetProjects.Migrator.Framework;

/// <summary>
/// Represents a table column.
/// </summary>
public class Column : IColumn, IDbField
{
    public Column(string name)
    {
        Name = name;
    }

    public Column(string name, DbType type)
    {
        Name = name;
        Type = type;
    }

    public Column(string name, DbType type, int size)
    {
        Name = name;
        Type = type;
        Size = size;
    }

    public Column(string name, DbType type, object defaultValue)
    {
        Name = name;
        Type = type;
        DefaultValue = defaultValue;
    }

    public Column(string name, DbType type, ColumnProperty property)
    {
        Name = name;
        Type = type;
        ColumnProperty = property;
    }

    public Column(string name, DbType type, int size, ColumnProperty property)
    {
        Name = name;
        Type = type;
        Size = size;
        ColumnProperty = property;
    }

    public Column(string name, DbType type, int size, ColumnProperty property, object defaultValue)
    {
        Name = name;
        Type = type;
        Size = size;
        ColumnProperty = property;
        DefaultValue = defaultValue;
    }

    public Column(string name, DbType type, ColumnProperty property, object defaultValue)
    {
        Name = name;
        Type = type;
        ColumnProperty = property;
        DefaultValue = defaultValue;
    }

    public Column(string name, MigratorDbType type)
    {
        Name = name;
        MigratorDbType = type;
    }

    public Column(string name, MigratorDbType type, int size)
    {
        Name = name;
        MigratorDbType = type;
        Size = size;
    }

    public Column(string name, MigratorDbType type, object defaultValue)
    {
        Name = name;
        MigratorDbType = type;
        DefaultValue = defaultValue;
    }

    public Column(string name, MigratorDbType type, ColumnProperty property)
    {
        Name = name;
        MigratorDbType = type;
        ColumnProperty = property;
    }

    public Column(string name, MigratorDbType type, int size, ColumnProperty property)
    {
        Name = name;
        MigratorDbType = type;
        Size = size;
        ColumnProperty = property;
    }

    public Column(string name, MigratorDbType type, int size, ColumnProperty property, object defaultValue)
    {
        Name = name;
        MigratorDbType = type;
        Size = size;
        ColumnProperty = property;
        DefaultValue = defaultValue;
    }

    public Column(string name, MigratorDbType type, ColumnProperty property, object defaultValue)
    {
        Name = name;
        MigratorDbType = type;
        ColumnProperty = property;
        DefaultValue = defaultValue;
    }

    public string Name { get; set; }

    public DbType Type
    {
        get
        {
            return (DbType)MigratorDbType;
        }
        set
        {
            MigratorDbType = (MigratorDbType)value;
        }
    }

    public MigratorDbType MigratorDbType { get; set; }

    public int Size { get; set; }

    public int? Precision { get; set; }

    public int? Scale { get; set; }

    public ColumnProperty ColumnProperty { get; set; }

    public object DefaultValue { get; set; }

    public bool IsIdentity
    {
        get { return (ColumnProperty & ColumnProperty.Identity) == ColumnProperty.Identity; }
    }

    public bool IsPrimaryKey
    {
        get { return (ColumnProperty & ColumnProperty.PrimaryKey) == ColumnProperty.PrimaryKey; }
    }

    public bool IsPrimaryKeyNonClustered
    {
        get { return (ColumnProperty & ColumnProperty.PrimaryKeyNonClustered) == ColumnProperty.PrimaryKeyNonClustered; }
    }
}
