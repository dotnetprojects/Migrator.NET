using System.Collections.Generic;
using System.Data;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using DotNetProjects.Migrator.Providers.Impl.Mysql;
using DotNetProjects.Migrator.Providers.Impl.SqlServer;
using NUnit.Framework;

namespace Migrator.Tests;

[Category("Unit")]
public class ColumnWithPrimaryKeySqlTests
{
    private sealed class RecordingMySqlProvider() : MySqlTransformationProvider(new MysqlDialect(), (IDbConnection)null, null, null)
    {
        public List<string> Statements { get; } = [];
        public override bool TableExists(string table) => true;
        public override Column[] GetColumns(string table) => [new Column("Value", DbType.String)];
        public override TableConstraint[] GetTableConstraints(string table) => [];
        public override int ExecuteNonQuery(string sql) { Statements.Add(sql); return 0; }
    }

    private sealed class RecordingSqlServerProvider() : SqlServerTransformationProvider(new SqlServerDialect(), (IDbConnection)null, null, null, null)
    {
        public List<string> Statements { get; } = [];
        public override bool TableExists(string table) => true;
        public override Column[] GetColumns(string table) => [new Column("Value", DbType.String)];
        public override TableConstraint[] GetTableConstraints(string table) => [];
        public override int ExecuteNonQuery(string sql) { Statements.Add(sql); return 0; }
    }

    [Test]
    public void MySqlAddsAutoIncrementAndKeyInOneStatement()
    {
        using var provider = new RecordingMySqlProvider();
        provider.AddColumn("Settings", new Column("Id", DbType.Int32) { IsIdentity = true }, new PrimaryKeyConstraint("PK_Settings", "Id"));
        Assert.That(provider.Statements, Has.Count.EqualTo(1));
        Assert.That(provider.Statements[0], Does.Contain("AUTO_INCREMENT").IgnoreCase.And.Contain("PRIMARY KEY").IgnoreCase);
        Assert.That(provider.Statements[0], Does.Contain(", ADD CONSTRAINT"));
    }

    [Test]
    public void SqlServerUsesItsNonclusteredPrimaryKeyImplementation()
    {
        using var provider = new RecordingSqlServerProvider();
        var column = new Column("Id", DbType.Int32) { IsIdentity = true };
        provider.AddColumn("Settings", column, new PrimaryKeyConstraint("PK_Settings", "Id") { NonClustered = true });
        Assert.That(provider.Statements, Has.Count.EqualTo(2));
        Assert.That(provider.Statements[0], Does.Contain("IDENTITY").IgnoreCase);
        Assert.That(provider.Statements[1], Does.Contain("PRIMARY KEY NONCLUSTERED").IgnoreCase);
        Assert.That(column.IsNullable, Is.True);
    }
}
