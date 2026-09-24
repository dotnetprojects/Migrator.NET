using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("Unit")]
public class DeleteDuplicateRowsValidationTests
{
    [Test]
    public void UnsupportedProviderIsRejectedBeforeUsingItsConnection()
    {
        using var provider = new MySqlTransformationProvider(new MysqlDialect(), (IDbConnection)null, null, null);
        Assert.Throws<NotSupportedException>(() => provider.DeleteDuplicateRows("Data", ["K"], DuplicateRowRetention.Any));
        var builder = new MigrationBuilder();
        builder.Delete.DuplicateRows().FromTable("Data").ByColumns("K").KeepAny();
        Assert.Throws<NotSupportedException>(() => builder.Apply(provider));
    }

    [Test]
    public void UnknownOptionsAreRejectedBeforeUsingTheConnection()
    {
        using var provider = new MySqlTransformationProvider(new MysqlDialect(), (IDbConnection)null, null, null);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.DeleteDuplicateRows("Data", ["K"], (DuplicateRowRetention)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.DeleteDuplicateRows("Data", ["K"], DuplicateRowRetention.Any, (DuplicateNullHandling)99));
    }
}
