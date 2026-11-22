using System;
using System.Data;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;
using NSubstitute;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests;

[Category("Unit")]
public class PortableCheckOperationTests
{
    [TestCase(ProviderTypes.Hana)]
    [TestCase(ProviderTypes.Firebird)]
    [TestCase(ProviderTypes.IBM_DB2)]
    [TestCase(ProviderTypes.IBM_Informix)]
    [TestCase(ProviderTypes.Sybase)]
    [TestCase(ProviderTypes.Ingres)]
    public void UnsupportedInlineCheckFailsBeforeAnyQueuedTableDdl(ProviderTypes type)
    {
        var p = Substitute.For<ITransformationProvider>();
        p.Dialect.Returns(ProviderFactory.DialectForProvider(type));
        var b = new MigrationBuilder();
        b.Create.Table("Plain").WithColumn("Id").AsInt32();
        b.Create.Table("Checked").WithColumn("Payload").AsString(100).WithCheckConstraint("ck_json", Column("Payload").IsJson());
        Assert.Throws<NotSupportedException>(() => b.Apply(p));
        p.DidNotReceiveWithAnyArgs().AddTable("", Array.Empty<IDbField>());
    }
    [Test]
    public void CheckCanBeReversedAndInformixPreviewUsesConstraintKeyword()
    {
        var op = new PortableCheckOperation("ck", "Items", Column("Amount").GreaterThan(Value(0)));
        Assert.That(op.Reverse(), Is.EqualTo(new RemoveOperation(RemoveKind.Constraint, "Items", "ck")));
        var context = new SqlGenerationContext(ProviderTypes.IBM_Informix, _ => [new Column("Amount", DbType.Int32)]);
        Assert.That(op.ToSql(context), Does.Contain(" ADD CONSTRAINT CHECK "));
    }
    [Test]
    public void PreviewRequiresAnExistingTableAndKnownSchema()
    {
        var op = new PortableCheckOperation("ck", "Items", Column("Amount").GreaterThan(Value(0)));
        var context = new SqlGenerationContext(ProviderTypes.PostgreSQL);
        context.AddTable("Items", [new Column("Amount", DbType.Int32)]); context.RemoveTable("Items");
        Assert.Throws<MigrationException>(() => op.ToSql(context));
        context = new SqlGenerationContext(ProviderTypes.PostgreSQL, _ => [new Column("Amount", DbType.Int32)]); context.InvalidateSchema();
        Assert.Throws<NotSupportedException>(() => op.ToSql(context));
    }
    [Test]
    public void FirebirdNonblankCheckUsesBuiltInFunctions()
    {
        var d = ProviderFactory.DialectForProvider(ProviderTypes.Firebird);
        var sql = Column("Reason").Trim().TextLength().GreaterThan(Value(0)).ToSql(new SqlExpressionContext(d));
        Assert.That(sql, Does.Contain("CHAR_LENGTH(TRIM("));
    }
}
