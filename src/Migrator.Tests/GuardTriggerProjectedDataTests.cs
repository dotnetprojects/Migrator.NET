using DotNetProjects.Migrator.Framework;
using DotNetProjects.Migrator.Framework.Fluent;
using NUnit.Framework;
using static DotNetProjects.Migrator.Framework.SqlExpression;

namespace Migrator.Tests;

// The fixture declarations and connections are shared with the direct-SQL and
// provider data API cases. INSERT SELECT enters the stack in the projection PR.
public partial class GuardTriggerProviderTests
{
    [Test]
    public void DatabaseIndependentProjectedInsertIsAtomicUnderTheGuard()
    {
        var guard = RelatedApiGuard(TriggerEvent.Insert);
        var schema = new MigrationBuilder(); schema.Create.GuardTrigger(guard); schema.Apply(provider);
        SelectQuery Query(SqlExpression predicate = null) => new(parent, "p",
            [new(Column("Id", "p").Add(Value(2)), "Id"), new(Column("Id", "p"), "ParentId"), new(Parameter(20), "Amount")], predicate);
        var blocked = new MigrationBuilder(); blocked.InsertFromQuery(table, ["Id", "ParentId", "Amount"], Query());
        Assert.That(Assert.Catch(() => blocked.Apply(provider)).ToString(), Does.Contain(guard.Message));
        AssertApiRows((1, 1, 10), (2, 2, 10));
        provider.InsertFromQuery(table, ["Id", "ParentId", "Amount"], Query(Column("Finalized", "p").EqualTo(Parameter(0))));
        AssertApiRows((1, 1, 10), (2, 2, 10), (3, 1, 20));
        provider.Delete(table, ["Id"], [3]);
        DropGuardThroughApi(guard);
        blocked.Apply(provider);
        AssertApiRows((1, 1, 10), (2, 2, 10), (3, 1, 20), (4, 2, 20));
    }
}
