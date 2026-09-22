using System;
using System.Collections.Generic;
using System.Data;
using ForeignKeyConstraint = DotNetProjects.Migrator.Framework.ForeignKeyConstraint;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using NUnit.Framework;
using NSubstitute;
namespace Migrator.Tests;
public class ProviderDefinitionTests
{
    private sealed class RecordingProvider() : TransformationProvider(new SqlServerDialect(), (IDbConnection)null, null, "default")
    {
        public string ColumnSql;
        public (string Child, string[] ChildColumns, string Parent, string[] ParentColumns, ForeignKeyConstraintType Delete, ForeignKeyConstraintType Update) ForeignKey;
        public override void ChangeColumn(string table, string sqlColumn) => ColumnSql = sqlColumn;
        public override void AddUniqueConstraint(string name, string table, params string[] columns) { }
        public override void AddColumn(string table, string sqlColumn) => ColumnSql = sqlColumn;
        public override void AddForeignKey(string name, string child, string[] columns, string parent, string[] parentColumns, ForeignKeyConstraintType delete, ForeignKeyConstraintType update)
            => ForeignKey = (child, columns, parent, parentColumns, delete, update);
        public override List<string> GetDatabases() => new();
        public override bool ConstraintExists(string table, string name) => false;
        public override bool IndexExists(string table, string name) => false;
    }
    [Test] public void NullableScalarRetainsTypedValuesAndHandlesNulls()
    {
        var provider = Substitute.For<ITransformationProvider>();
        var id = Guid.NewGuid();
        provider.ExecuteScalar("guid").Returns(id);
        provider.ExecuteScalar("null").Returns(DBNull.Value);
        provider.ExecuteScalar("number").Returns(12L);
        Assert.That(provider.ExecuteNullableScalar<Guid>("guid"), Is.EqualTo(id));
        Assert.That(provider.ExecuteNullableScalar<int>("null"), Is.Null);
        Assert.That(provider.ExecuteNullableScalar<int>("number"), Is.EqualTo(12));
    }
    [Test] public void ChangeColumnDoesNotClearCallerUniqueFlag()
    {
        using var provider = new RecordingProvider();
        var column = new Column("Value", DbType.Int32, ColumnProperty.Unique | ColumnProperty.NotNull);
        provider.ChangeColumn("Example", column);
        Assert.That(column.ColumnProperty, Is.EqualTo(ColumnProperty.Unique | ColumnProperty.NotNull));
    }
    [Test] public void QuotingReturnsNewArrayWithoutChangingCallerNames()
    {
        using var provider = new RecordingProvider();
        var names = new[] { "select", "Normal" };
        var quoted = provider.QuoteColumnNamesIfRequired(names);
        Assert.That(quoted, Is.Not.SameAs(names));
        Assert.That(names, Is.EqualTo(new[] { "select", "Normal" }));
        Assert.That(quoted[0], Is.EqualTo("[select]"));
    }
    [Test] public void AddColumnCarriesPrecisionAndScaleIntoDialectMapping()
    {
        using var provider = new RecordingProvider();
        var column = new Column("Amount", DbType.Decimal) { Precision = 12, Scale = 4 };
        provider.AddColumn("Example", column);
        Assert.That(provider.ColumnSql.Replace(" ", "").ToUpperInvariant(), Does.Contain("DECIMAL(12,4)"));
        Assert.That(column.Precision, Is.EqualTo(12));
        Assert.That(column.Scale, Is.EqualTo(4));
    }
    [Test] public void ForeignKeyDefinitionRetainsDirectionAndIndependentActions()
    {
        using var provider = new RecordingProvider();
        var fk = new ForeignKeyConstraint("fk", "Parent", new[] { "ParentId" }, "Child", new[] { "ParentReference" }) { OnDelete = "SET NULL", OnUpdate = "CASCADE" };
        provider.AddForeignKey("Child", fk);
        Assert.That(provider.ForeignKey.Child, Is.EqualTo("Child"));
        Assert.That(provider.ForeignKey.ChildColumns, Is.EqualTo(fk.ChildColumns));
        Assert.That(provider.ForeignKey.Parent, Is.EqualTo("Parent"));
        Assert.That(provider.ForeignKey.ParentColumns, Is.EqualTo(fk.ParentColumns));
        Assert.That(provider.ForeignKey.Delete, Is.EqualTo(ForeignKeyConstraintType.SetNull));
        Assert.That(provider.ForeignKey.Update, Is.EqualTo(ForeignKeyConstraintType.Cascade));
        Assert.That(provider.ForeignKey.ChildColumns, Is.Not.SameAs(fk.ChildColumns));
    }
}

