using System.Collections.Generic;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;

namespace Migrator.Tests.Providers;

[TestFixture]
public class GenericProviderTests
{
    [Test]
    public void CanJoinColumnsAndValues()
    {
        var provider = new GenericTransformationProvider();
        var result = provider.JoinColumnsAndValues(["foo", "bar"], ["123", "456"]);

        Assert.That("foo='123', bar='456'", Is.EqualTo(result));
    }
}

internal class GenericTransformationProvider : TransformationProvider
{
    public GenericTransformationProvider() : base(null, null as string, null, "default")
    {
    }

    public override bool ConstraintExists(string table, string name)
    {
        return false;
    }

    public override List<string> GetDatabases()
    {
        throw new System.NotImplementedException();
    }

    public override bool IndexExists(string table, string name)
    {
        return false;
    }
}