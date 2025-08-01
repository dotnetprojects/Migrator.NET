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

namespace Migrator.Framework.SchemaBuilder;

public class RenameTableExpression : ISchemaBuilderExpression
{
    private readonly string _newName;
    private readonly string _oldName;

    public RenameTableExpression(string oldName, string newName)
    {
        _oldName = oldName;
        _newName = newName;
    }

    public void Create(ITransformationProvider provider)
    {
        provider.RenameTable(_oldName, _newName);
    }
}