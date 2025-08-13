using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Oracle;
using DryIoc;
using Migrator.Tests.Database;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Providers.Base;
using Migrator.Tests.Settings;
using Migrator.Tests.Settings.Config;
using Migrator.Tests.Settings.Models;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider.Base;

public class OracleTransformationProviderTestBase : TransformationProviderSimpleBase
{
    [SetUp]
    public async Task SetUpAsync()
    {


        AddDefaultTable();
    }
}