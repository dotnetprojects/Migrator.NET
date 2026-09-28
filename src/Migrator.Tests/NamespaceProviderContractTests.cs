using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator;
using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Providers;
using NSubstitute;
using NUnit.Framework;

namespace Migrator.Tests;

// Covers every public provider selector, including legacy aliases and Ingres.
// The live suite separately verifies that emitted commands work on real engines.
public class NamespaceProviderContractTests
{
    public static IEnumerable<ProviderTypes> Providers => Enum.GetValues<ProviderTypes>().Where(t => t != ProviderTypes.none);

    [TestCaseSource(nameof(Providers))]
    public void NamespaceContractForDdlAndCatalogLookups(ProviderTypes type)
    {
        foreach (var mode in new[] { "unqualified", "qualified", "default" })
        {
            var commands = new List<string>();
            var connection = Substitute.For<IDbConnection>();
            connection.State.Returns(ConnectionState.Open);
            connection.CreateCommand().Returns(_ =>
            {
                var command = Substitute.For<IDbCommand>();
                var parameters = new List<IDbDataParameter>();
                var collection = Substitute.For<IDataParameterCollection>();
                collection.Add(Arg.Any<object>()).Returns(c => { parameters.Add((IDbDataParameter)c[0]); return parameters.Count - 1; });
                command.Parameters.Returns(collection);
                command.CreateParameter().Returns(_ => Substitute.For<IDbDataParameter>());
                void Record() => commands.Add(command.CommandText + " " + string.Join(" ", parameters.Select(p => p.Value)));
                command.ExecuteNonQuery().Returns(_ => { Record(); return 1; });
                command.ExecuteScalar().Returns(_ => { Record(); return 0; });
                command.ExecuteReader().Returns(_ => { Record(); return new DataTable().CreateDataReader(); });
                command.ExecuteReader(Arg.Any<CommandBehavior>()).Returns(_ => { Record(); return new DataTable().CreateDataReader(); });
                return command;
            });
            using var provider = ProviderFactory.Create(type, connection, mode == "default" ? "tenant" : null);
            var table = mode == "qualified" ? "tenant.items" : "items";
            if (type == ProviderTypes.Firebird && mode != "unqualified")
            {
                Assert.Throws<NotSupportedException>(() => provider.AddTable(table, new Column("id", DbType.Int32)));
                Assert.Throws<NotSupportedException>(() => provider.TableExists(table));
                Assert.That(commands, Is.Empty);
                continue;
            }
            provider.AddTable(table, new Column("id", DbType.Int32));
            Assert.That(commands.Single().ToLowerInvariant(), Does.Contain("items"), type + "/" + mode);
            if (mode != "unqualified") Assert.That(commands.Single().ToLowerInvariant(), Does.Contain("tenant"));
            commands.Clear();
            provider.TableExists(table);
            Assert.That(commands, Is.Not.Empty, type + "/TableExists");
            if (mode != "unqualified") Assert.That(string.Join(" ", commands).ToLowerInvariant(), Does.Contain("tenant"), type + "/TableExists");
            commands.Clear();
            provider.ViewExists(table);
            if (mode != "unqualified") Assert.That(string.Join(" ", commands).ToLowerInvariant(), Does.Contain("tenant"), type + "/ViewExists");
            commands.Clear();
            provider.GetTables(mode == "unqualified" ? null : "tenant").ToArray();
            if (mode != "unqualified") Assert.That(string.Join(" ", commands).ToLowerInvariant(), Does.Contain("tenant"), type + "/GetTables");
        }
    }

    [TestCaseSource(nameof(Providers))]
    public void DefaultNamespaceDoesNotOverrideExplicitNamespace(ProviderTypes type)
    {
        var connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open);
        using var provider = ProviderFactory.Create(type, connection, "first");
        if (type == ProviderTypes.Firebird)
            Assert.Throws<NotSupportedException>(() => provider.QuoteTableNameIfRequired("second.items"));
        else
        {
            var parts = SqlIdentifier.Parse(provider.QuoteTableNameIfRequired("second.items"));
            Assert.That(parts.Select(p => p.Value), Is.EqualTo(new[] { "second", "items" }));
        }
    }
}
