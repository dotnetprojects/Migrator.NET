using Migrator.Tests.Providers.Base;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_GetColumnsTestsBase : TransformationProviderBase
{
    [Test]
    public void NamedTableConstraintsEnforceCompositeKeysAndReturnOrderedMetadata()
    {
        Provider.AddTable("NamedConstraintModel",
            new Column("IdA", DbType.Int32), new Column("IdB", DbType.Int32), new Column("Amount", DbType.Int32),
            new PrimaryKeyConstraint("PK_NamedModel", "IdB", "IdA"),
            new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ_NamedModel", "IdA", "Amount"),
            new CheckConstraint("CK_NamedModel", "Amount >= 0"));
        Provider.Insert("NamedConstraintModel", new[] { "IdA", "IdB", "Amount" }, new object[] { 1, 2, 3 });
        Provider.Insert("NamedConstraintModel", new[] { "IdA", "IdB", "Amount" }, new object[] { 1, 3, 4 });
        var dialect = Provider.Dialect;
        if (dialect is DotNetProjects.Migrator.Providers.Impl.SQLite.SQLiteDialect or
            DotNetProjects.Migrator.Providers.Impl.SqlServer.SqlServerDialect or
            DotNetProjects.Migrator.Providers.Impl.PostgreSQL.PostgreSQLDialect or
            DotNetProjects.Migrator.Providers.Impl.Oracle.OracleDialect or
            DotNetProjects.Migrator.Providers.Impl.Mysql.MysqlDialect)
        {
            var constraints = Provider.GetTableConstraints("NamedConstraintModel");
            Assert.That(constraints.OfType<PrimaryKeyConstraint>().Single().KeyColumns.Select(c => c.ToUpperInvariant()), Is.EqualTo(new[] { "IDB", "IDA" }));
            Assert.That(constraints.OfType<DotNetProjects.Migrator.Framework.UniqueConstraint>().Single().KeyColumns.Select(c => c.ToUpperInvariant()), Is.EqualTo(new[] { "IDA", "AMOUNT" }));
            Assert.That(constraints.OfType<CheckConstraint>().Any(c => c.Name.ToUpperInvariant() == "CK_NAMEDMODEL"), Is.True);
        }
        else Assert.Throws<System.NotSupportedException>(() => Provider.GetTableConstraints("NamedConstraintModel"));
        // On PostgreSQL a failing statement aborts this test's transaction, so check one complete-key violation last.
        Assert.Catch(() => Provider.Insert("NamedConstraintModel", new[] { "IdA", "IdB", "Amount" }, new object[] { 1, 2, 5 }));
    }

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