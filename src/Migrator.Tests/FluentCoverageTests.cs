using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;
namespace Migrator.Tests;
public class FluentCoverageTests
{
    [Test] public void EveryNormalApiMethodFamilyHasAnExplicitFluentOrContextMapping()
    {
        var inventory = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(System.AppContext.BaseDirectory, "fluent-operation-coverage.json")));
        var methods = typeof(ITransformationProvider).GetMethods().Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().ToArray();
        Assert.That(inventory.Keys, Is.EquivalentTo(methods));
        Assert.That(inventory.Values.All(v => !string.IsNullOrWhiteSpace(v)), Is.True);
    }
}
