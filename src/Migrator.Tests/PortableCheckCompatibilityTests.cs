using System;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.SQLite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests;

[Category("Unit")]
public class PortableCheckCompatibilityTests
{
    [Test]
    public void OriginalNullConstructorCallStillCreatesAPlaceholder()
    {
        var check = new CheckConstraint("ck", null);
        Assert.That(check.CheckConstraintString, Is.Null);
        Assert.That(check.Expression, Is.Null);
        Assert.That(new CheckConstraint("default", default).CheckConstraintString, Is.Null);
        check.CheckConstraintString = "Amount > 0";
        Assert.That(new SQLiteDialect().GetTableConstraintSql(check), Does.Contain("Amount > 0"));
    }

    [Test]
    public void UntypedNullFluentCallsStillSelectTheOriginalStringOverloads()
    {
        var b = new MigrationBuilder();
        Assert.Throws<ArgumentException>(() => b.Create.CheckConstraint("ck").OnTable("Data").WithExpression(null));
        Assert.Throws<ArgumentException>(() => b.Create.Table("Data").WithCheckConstraint("ck", null));
        Assert.Throws<ArgumentException>(() => b.Create.Table("Data").WithColumn("Amount").AsInt32().WithCheckConstraint("ck", null));
        Assert.Throws<ArgumentException>(() => b.Create.CheckConstraint("default").OnTable("Data").WithExpression(default));
        Assert.Throws<ArgumentException>(() => b.Create.Table("Default").WithCheckConstraint("ck", default));
    }

    [Test]
    public void RawAndPortableMethodGroupsRemainDistinct()
    {
        var b = new MigrationBuilder(); b.Create.Table("Data").WithColumn("Amount").AsInt32();
        Action<string> raw = b.Create.CheckConstraint("ck_raw").OnTable("Data").WithExpression;
        Action<SqlExpression> portable = b.Create.CheckConstraint("ck_portable").OnTable("Data").WithExpression;
        raw("Amount > 0"); portable(Column("Amount").GreaterThan(Value(0)));
        var operations = b.Build();
        Assert.That(operations[1], Is.TypeOf<ConstraintOperation>());
        Assert.That(operations[2], Is.TypeOf<PortableCheckOperation>());
        var context = new SqlGenerationContext(ProviderTypes.PostgreSQL);
        operations[0].ToSql(context);
        Assert.That(operations[2].ToSql(context), Does.Contain("CHECK"));

        var inline = new MigrationBuilder(); var table = inline.Create.Table("Inline");
        Func<string, string, TableBuilder> rawInline = table.WithCheckConstraint;
        Func<string, SqlExpression, TableBuilder> portableInline = table.WithCheckConstraint;
        rawInline("ck_raw", "Amount > 0"); portableInline("ck_portable", Column("Amount").GreaterThan(Value(0)));
        table.WithColumn("Amount").AsInt32();
        Assert.That(inline.Preview(new SqlGenerationContext(ProviderTypes.SQLite))[0], Does.Contain("CHECK"));
    }

    [Test]
    public void NamedFactoryRetainsThePortableExpression()
    {
        var expression = Column("Amount").GreaterThan(Value(0));
        var check = CheckConstraint.FromExpression("ck", expression);
        Assert.That(check.Expression, Is.SameAs(expression));
        Assert.That(check.CheckConstraintString, Is.Null);
        Assert.Throws<ArgumentNullException>(() => CheckConstraint.FromExpression("ck", null));
    }

    [Test, Category("SQLite")]
    public void InferredPortableInlineOverloadsStillEnforceWrites()
    {
        var b = new MigrationBuilder();
        b.Create.Table("Direct").WithCheckConstraint("ck_direct", Column("Amount").GreaterThan(Value(0))).WithColumn("Amount").AsInt32();
        b.Create.Table("AfterColumn").WithColumn("Amount").AsInt32().WithCheckConstraint("ck_column", Column("Amount").GreaterThan(Value(0)));
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var p = new SQLiteTransformationProvider(new SQLiteDialect(), connection, null, null);
        b.Apply(p);
        foreach (var table in new[] { "Direct", "AfterColumn" })
        {
            Assert.That(p.Insert(table, ["Amount"], [1]), Is.EqualTo(1));
            Assert.Catch(() => p.Insert(table, ["Amount"], [-1]));
        }
    }
}
