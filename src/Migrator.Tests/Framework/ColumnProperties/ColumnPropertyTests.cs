using Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Framework.ColumnProperties;

public class ColumnPropertyExtensionsTests
{
    [Test]
    public void Clear()
    {
        // Arrange
        var columnProperty = ColumnProperty.PrimaryKey | ColumnProperty.Unique;

        // Act
        columnProperty = columnProperty.Clear(ColumnProperty.PrimaryKey);

        // Assert
        Assert.That(columnProperty.HasFlag(ColumnProperty.PrimaryKey), Is.False);
    }
}