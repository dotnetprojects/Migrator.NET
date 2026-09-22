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

using System.Collections.Generic;
using System.Linq;
namespace DotNetProjects.Migrator.Framework.SchemaBuilder;

public class AddTableExpression : ISchemaBuilderExpression
{
    private readonly string _newTable;
    public List<IFluentColumn> Columns { get; } = new();

    public AddTableExpression(string newTable)
    {
        _newTable = newTable;
    }

    public void Create(ITransformationProvider provider)
    {
        var fields = Columns.Select(c => (IDbField)new Column(c.Name, c.Type, c.Size, c.ColumnProperty, c.DefaultValue)).ToList();
        foreach (var c in Columns.Where(c => c.ForeignKey != null))
            fields.Add(new ForeignKeyConstraint("FK_" + _newTable + "_" + c.Name + "_" + c.ForeignKey.PrimaryTable + "_" + c.ForeignKey.PrimaryKey,
                c.ForeignKey.PrimaryTable, new[] { c.ForeignKey.PrimaryKey }, _newTable, new[] { c.Name })
                { OnDelete = new DotNetProjects.Migrator.Providers.ForeignKeyConstraintMapper().SqlForConstraint(c.Constraint) });
        provider.AddTable(_newTable, fields.ToArray());
    }
}