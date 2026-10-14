using System;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using NUnit.Framework;
using DtoColumn = System.ComponentModel.DataAnnotations.Schema.ColumnAttribute;

namespace Migrator.Tests.Providers.Live;

[TestFixture("SQLite", ProviderTypes.SQLite, Category = "SQLite")]
[TestFixture("SQLServer", ProviderTypes.SqlServer, Category = "SQLServer")]
[TestFixture("PostgreSQL", ProviderTypes.PostgreSQL, Category = "PostgreSQL")]
[TestFixture("Oracle", ProviderTypes.Oracle, Category = "Oracle")]
[TestFixture("MySQL", ProviderTypes.Mysql, Category = "MySQL")]
[TestFixture("MariaDB", ProviderTypes.MariaDB, Category = "MariaDB")]
[TestFixture("Firebird", ProviderTypes.Firebird, Category = "Firebird")]
[TestFixture("Db2", ProviderTypes.IBM_DB2, Category = "Db2")]
[TestFixture("Informix", ProviderTypes.IBM_Informix, Category = "Informix")]
[TestFixture("Sybase", ProviderTypes.Sybase, Category = "Sybase")]
[TestFixture("Hana", ProviderTypes.Hana, Category = "Hana")]
[NonParallelizable]
public class DtoSelectIntegrationTests(string database, ProviderTypes providerType) : LiveProviderFixture(database, providerType)
{
    public enum Status { New = 1, Archived = 2 }
    public class Row
    {
        [DtoColumn("id")] public int Id { get; set; }
        [DtoColumn("active")] public bool Active { get; set; }
        [DtoColumn("payload")] public string Payload { get; set; }
        [DtoColumn("optional")] public int? Optional { get; set; }
        [DtoColumn("status")] public Status State { get; set; }
    }

    [Test]
    public void MapsDtosAndFiltersUsingNativeDriverParameters()
    {
        var table = "dto_" + Guid.NewGuid().ToString("N")[..12];
        Provider.AddTable(table, new Column("id", DbType.Int32) { IsNullable = false },
            new Column("active", DbType.Boolean) { IsNullable = false }, new Column("payload", DbType.String, 100),
            new Column("optional", DbType.Int32), new Column("status", DbType.Int32) { IsNullable = false });
        var columns = new[] { "id", "active", "payload", "optional", "status" };
        try
        {
            Provider.Insert(table, columns, new object[] { 1, true, "O'Brien", null, 1 });
            Provider.Insert(table, columns, new object[] { 2, false, "other", 7, 2 });
            Provider.Insert(table, columns, new object[] { 3, true, "third", 9, 1 });
            var minimum = 1;
            var state = Status.New;
            var rows = Provider.Select<Row>(table, x => x.Active && x.State == state && (x.Id > minimum || x.Optional == null));
            Assert.That(rows.Select(x => x.Id), Is.EquivalentTo(new[] { 1, 3 }));
            Assert.That(rows.Single(x => x.Id == 1).Payload, Is.EqualTo("O'Brien"));
            Assert.That(rows.Single(x => x.Id == 1).Optional, Is.Null);
            Assert.That(rows.Single(x => x.Id == 3).Optional, Is.EqualTo(9));
            var injection = "'; DROP TABLE " + table + "; --";
            Assert.That(Provider.Select<Row>(table, x => x.Payload == injection), Is.Empty);
            Assert.That(Provider.Select<Row>(table).Count, Is.EqualTo(3));
            var literal = "literal%_~[b]\\tail";
            Assert.That(Provider.Update<Row>(table, x => new Row { Payload = literal, Optional = null }, x => x.Id == 3), Is.EqualTo(1));
            Assert.That(Provider.Select<Row>(table, x => x.Payload.Contains("%_~[b]\\")).Single().Id, Is.EqualTo(3));
            Assert.That(Provider.Select<Row>(table, x => x.Payload.StartsWith("literal%_")).Single().Id, Is.EqualTo(3));
            Assert.That(Provider.Select<Row>(table, x => x.Payload.EndsWith("[b]\\tail")).Single().Id, Is.EqualTo(3));
            Assert.That(Provider.Delete<Row>(table, x => x.Payload.Contains("%_~") && x.Id == 3), Is.EqualTo(1));
            Assert.That(Provider.Select<Row>(table).Count, Is.EqualTo(2));
        }
        finally { Provider.RemoveTable(table); }
    }
}
