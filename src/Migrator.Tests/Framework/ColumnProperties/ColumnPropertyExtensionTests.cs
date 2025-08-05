using NUnit.Framework;
using DotNetProjects.Migrator.Framework;
using System;
using System.Linq;

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
        var columnProperty = ColumnProperty.PrimaryKeyWithIdentity | ColumnProperty.NotNull;

        // Act
        var actualData = GetAllSingleColumnProperties().Select(x => new
        {
            ColumnPropertyString = x.ToString(),
            IsSet = columnProperty.IsSet(x),
            IsNotSet = columnProperty.IsNotSet(x)
        })
        .ToList();

        // Assert
        string[] expectedSet = [nameof(ColumnProperty.PrimaryKey), nameof(ColumnProperty.NotNull), nameof(ColumnProperty.Identity)];
        var actualDataShouldBeTrue = actualData.Where(x => expectedSet.Any(y => y == x.ColumnPropertyString)).ToList();
        var actualDataShouldBeFalse = actualData.Where(x => !expectedSet.Any(y => y == x.ColumnPropertyString)).ToList();

        Assert.That(actualDataShouldBeTrue.Select(x => x.IsSet), Has.All.True);
        Assert.That(actualDataShouldBeFalse.Select(x => x.IsSet), Has.All.False);
    }

    [Test]
    public void IsNotSet()
    {
        // Arrange
        var columnProperty = ColumnProperty.PrimaryKeyWithIdentity | ColumnProperty.NotNull;

        // Act
        var actualData = GetAllSingleColumnProperties().Select(x => new
        {
            ColumnPropertyString = x.ToString(),
            IsSet = columnProperty.IsNotSet(x),
            IsNotSet = columnProperty.IsNotSet(x)
        })
        .ToList();

        // Assert
        string[] expectedSet = [nameof(ColumnProperty.PrimaryKey), nameof(ColumnProperty.NotNull), nameof(ColumnProperty.Identity)];
        var actualDataShouldBeFalse = actualData.Where(x => expectedSet.Any(y => y == x.ColumnPropertyString)).ToList();
        var actualDataShouldBeTrue = actualData.Where(x => !expectedSet.Any(y => y == x.ColumnPropertyString)).ToList();

        Assert.That(actualDataShouldBeTrue.Select(x => x.IsNotSet), Has.All.True);
        Assert.That(actualDataShouldBeFalse.Select(x => x.IsNotSet), Has.All.False);
    }

    [Test]
    public void Set_Success()
    {
        // Arrange
        var columnProperty = ColumnProperty.NotNull;

        // Act
        var result = columnProperty.Set(ColumnProperty.PrimaryKeyWithIdentity);

        // Assert
        var expected = ColumnProperty.NotNull | ColumnProperty.PrimaryKeyWithIdentity;

        Assert.That(result, Is.EqualTo(expected));
    }

    private ColumnProperty[] GetAllSingleColumnProperties()
    {
        return [.. Enum.GetValues<ColumnProperty>().Where(x => x == 0 || (x & (x - 1)) == 0)];
    }
}