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
using System.Configuration;
using Migrator.Providers.SqlServer;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

[TestFixture]
[Category("SqlServer2005")]
public class SqlServer2005TransformationProviderTest : TransformationProviderConstraintBase
{
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
        string constr = ConfigurationManager.AppSettings["SqlServer2005ConnectionString"];


        if (constr == null)
            throw new ArgumentNullException("SqlServer2005ConnectionString", "No config file");

        Provider = new SqlServerTransformationProvider(new SqlServer2005Dialect(), constr, null, "default", null);
        Provider.BeginTransaction();

        AddDefaultTable();
    }

    #endregion
}