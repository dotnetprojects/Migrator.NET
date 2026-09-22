using Migrator.Tests.Providers.Base;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_GetColumnsTestsBase : TransformationProviderBase
{
    [Test]
    public void CompositeUniqueDoesNotMarkItsIndividualColumnsUnique()
    {
        Provider.AddTable("CompositeUniqueMetadata", new Column("FirstId", DbType.Int32), new Column("SecondId", DbType.Int32));
        Provider.AddUniqueConstraint("CompositeUniqueKey", "CompositeUniqueMetadata", "FirstId", "SecondId");
        Assert.That(Provider.GetColumns("CompositeUniqueMetadata").All(c => !c.ColumnProperty.HasFlag(ColumnProperty.Unique)), Is.True);
    }

    [Test]
    public void GetColumns_UniqueButNotPrimaryKey_ReturnsFalse()
    {
        // Arrange
        const string tableName = "GetColumnsTest";
        Provider.AddTable(tableName, new Column("Id", DbType.Int32, ColumnProperty.Unique));

        // Act
        var columns = Provider.GetColumns(tableName);

        // Assert
        Assert.That(columns.Single().ColumnProperty, Is.EqualTo(ColumnProperty.Null | ColumnProperty.Unique));
    }

}