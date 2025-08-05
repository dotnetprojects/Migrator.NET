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
public class ViewField : IViewField
{
    public ViewField(string ColumnName)
    {
        this.ColumnName = ColumnName;
    }

    public ViewField(string ColumnName, string TableName, string KeyColumnName, string ParentTableName, string ParentKeyColumnName)
    {
        this.ColumnName = ColumnName;
        this.TableName = TableName;
        this.KeyColumnName = KeyColumnName;
        this.ParentTableName = ParentTableName;
        this.ParentKeyColumnName = ParentKeyColumnName;
    }

    public string TableName { get; set; }
    public string ColumnName { get; set; }
    public string KeyColumnName { get; set; }
    public string ParentTableName { get; set; }
    public string ParentKeyColumnName { get; set; }
}
