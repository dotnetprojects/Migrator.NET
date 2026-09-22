using System.Threading.Tasks;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.SQLServer;

[TestFixture]
[Category("SQLServer")]
public class SQLServerTransformationProvider_ConstraintExistsTests : Generic_ConstraintExistsBase
{
    [Test]
    public void QualifiedNamesKeepColumnsIndexesAndConstraintsInTheirSchema()
    {
        Provider.ExecuteNonQuery("CREATE SCHEMA [audit.region]");
        const string table = "[audit.region].[O'Brien]";
        Provider.AddTable(table, new DotNetProjects.Migrator.Framework.Column("Id", System.Data.DbType.Int32),
            new DotNetProjects.Migrator.Framework.UniqueConstraint("UQ ' name", "Id"));
        Assert.That(Provider.TableExists(table), Is.True);
        Assert.That(Provider.GetColumns(table).Length, Is.EqualTo(1));
        Assert.That(Provider.GetIndexes(table).Length, Is.EqualTo(1));
        Assert.That(Provider.ConstraintExists(table, "UQ ' name"), Is.True);
        Provider.RemoveConstraint(table, "UQ ' name");
        Assert.That(Provider.GetIndexes(table), Is.Empty);
        Provider.RemoveTable(table);
        Assert.That(Provider.TableExists(table), Is.False);
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginSQLServerTransactionAsync();
    }
}
