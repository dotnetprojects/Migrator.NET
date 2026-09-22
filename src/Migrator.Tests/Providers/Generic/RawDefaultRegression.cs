using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

internal static class RawDefaultRegression
{
    internal static void AssertRoundTrip(ITransformationProvider provider)
    {
        provider.AddTable("RawDefaultsSource", new Column("Id", DbType.Int32),
            new Column("ExpressionValue", DbType.String, 50) { DefaultValue = RawSql.Insert("LOWER('ABC')") },
            new Column("LiteralValue", DbType.String, 50) { DefaultValue = "LOWER('ABC')" });
        var columns = provider.GetColumns("RawDefaultsSource");
        Assert.That(columns.Single(c => c.Name.Equals("ExpressionValue", StringComparison.OrdinalIgnoreCase)).DefaultValue, Is.TypeOf<RawSql>());
        provider.AddTable("RawDefaultsCopy", columns);
        var builder = new MigrationBuilder();
        builder.Create.Table("RawDefaultsFluent").WithColumn("Id").AsInt32()
            .WithColumn("ExpressionValue").AsString(50).WithDefaultValue(RawSql.Insert("LOWER('ABC')"))
            .WithColumn("LiteralValue").AsString(50).WithDefaultValue("LOWER('ABC')");
        builder.Apply(provider);
        foreach (var table in new[] { "RawDefaultsSource", "RawDefaultsCopy", "RawDefaultsFluent" })
        {
            provider.Insert(table, ["Id"], [1]);
            var quoted = provider.QuoteTableNameIfRequired(table);
            Assert.That(provider.ExecuteScalar("SELECT " + provider.QuoteColumnNameIfRequired("ExpressionValue") + " FROM " + quoted), Is.EqualTo("abc"));
            Assert.That(provider.ExecuteScalar("SELECT " + provider.QuoteColumnNameIfRequired("LiteralValue") + " FROM " + quoted), Is.EqualTo("LOWER('ABC')"));
        }
    }
}
