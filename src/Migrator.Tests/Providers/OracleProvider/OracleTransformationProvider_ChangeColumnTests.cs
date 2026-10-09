using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Generic;
using NUnit.Framework;

namespace Migrator.Tests.Providers.OracleProvider;

[TestFixture]
[Category("Oracle")]
public class OracleTransformationProvider_ChangeColumn_Tests : Generic_ChangeColumnTestsBase
{
    [SetUp]
    public async Task SetUpAsync()
    {
        await BeginOracleTransactionAsync();
    }

    [TestCase(DbType.String)]
    [TestCase(DbType.AnsiString)]
    [TestCase(DbType.Binary)]
    public void ChangeColumn_LobNullabilityPreservesDataAndSupportsRepeatedChanges(DbType type)
    {
        Provider.AddTable("Test", new Column("Status", type, int.MaxValue));
        object value = type == DbType.Binary ? new byte[] { 1, 2, 3 } : "pending";
        Provider.Insert("Test", ["Status"], [value]);

        foreach (var nullable in new[] { false, false, true, true })
        {
            Provider.ChangeColumn("Test", new Column("Status", type, int.MaxValue) { IsNullable = nullable });
            var actual = Provider.ReadLegacyColumn("Test", "Status");
            Assert.That(actual.IsNullable, Is.EqualTo(nullable));
            Assert.That(actual.Type, Is.EqualTo(type));
            Assert.That(actual.Size, Is.EqualTo(int.MaxValue));
            if (!nullable)
            {
                Assert.Catch<DbException>(() => Provider.Insert("Test", ["Status"], [null]));
            }
            using var command = Provider.CreateCommand();
            using var reader = Provider.Select(command, "Test", ["Status"]);
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetValue(0), Is.EqualTo(value));
        }

        Provider.Insert("Test", ["Status"], [null]);
    }
}
