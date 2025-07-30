using Migrator.Framework;
using NUnit.Framework;
using DotNetProjects.Migrator.Framework;
using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Migrator.Tests.Framework.ColumnProperties;

public class ColumnPropertyExtensionsTests
{
    [Test]
    public void Clear()
    {
        // Arrange
        var columnProperty = ColumnProperty.PrimaryKey | ColumnProperty.NotNull;

        // Act
        columnProperty = columnProperty.Clear(ColumnProperty.PrimaryKey);

        // Assert
        Assert.That(columnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
    }

    [Test]
    public void IsSet()
    {
        // Arrange
        var columnProperty = ColumnProperty.PrimaryKey | ColumnProperty.NotNull;

        // Act
        var isSetInfos = GetAllSingleColumnProperties().Select(x => new
        {
            ColumnPropertyString = x.ToString(),
            IsSet = columnProperty.IsSet(x),
            IsNotSet = columnProperty.IsNotSet(x)
        }).Where(x => x.ColumnPropertyString != ColumnProperty.None.ToString());

        // Assert
        string[] expectedSet = [nameof(ColumnProperty.PrimaryKey), nameof(ColumnProperty.NotNull)];
        var isSetInfosSet = isSetInfos.Where(x => expectedSet.Any(y => y == x.ColumnPropertyString)).ToList();

        Assert.That(isSetInfos.Select(x => x.IsSet), Has.All.True);
    }

    private ColumnProperty[] GetAllSingleColumnProperties()
    {
        return [.. Enum.GetValues<ColumnProperty>().Where(x => x == 0 || (x & (x - 1)) == 0)];
    }
}