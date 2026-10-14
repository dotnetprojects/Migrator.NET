using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;

namespace Migrator.Tests;

public class DtoNoOpTests
{
    public class Row { public int Id { get; set; } public string Text { get; set; } }

    [Test]
    public void TypedOperationsHonorAnInactiveProviderCondition()
    {
        ITransformationProvider provider = NoOpTransformationProvider.Instance;
        Assert.That(provider.Select<Row>("Rows", x => x.Text.Contains("needle")), Is.Empty);
        Assert.That(provider.Update<Row>("Rows", x => new Row { Id = 1 }, x => x.Id == 2), Is.Zero);
        Assert.That(provider.Delete<Row>("Rows", x => x.Id == 2), Is.Zero);
        Assert.That(provider.Query<Row>($"SELECT * FROM {Sql.Table("Rows")}"), Is.Empty);
        Assert.That(provider.Query($"SELECT {1}", r => 1), Is.Empty);
    }
}
