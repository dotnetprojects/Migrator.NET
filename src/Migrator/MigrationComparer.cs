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

using System;
using System.Collections.Generic;
using System.Reflection;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator;

/// <summary>
/// Comparer of Migration by their version attribute.
/// </summary>
public class MigrationTypeComparer : IComparer<Type>
{
    private readonly bool _ascending = true;

    public MigrationTypeComparer(bool ascending)
    {
        _ascending = ascending;
    }

    public int Compare(Type x, Type y)
    {
#if NETSTANDARD
        var attribOfX = x.GetTypeInfo().GetCustomAttribute<MigrationAttribute>();
        var attribOfY = y.GetTypeInfo().GetCustomAttribute<MigrationAttribute>();
#else
        var attribOfX = (MigrationAttribute)Attribute.GetCustomAttribute(x, typeof(MigrationAttribute));
        var attribOfY = (MigrationAttribute)Attribute.GetCustomAttribute(y, typeof(MigrationAttribute));
#endif

        if (_ascending)
        {
            return attribOfX.Version.CompareTo(attribOfY.Version);
        }
        else
        {
            return attribOfY.Version.CompareTo(attribOfX.Version);
        }
    }
}