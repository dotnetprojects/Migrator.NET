using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using DotNetProjects.Migrator.Framework;
using Migrator.Framework.Support;

namespace Migrator.Framework;

/// <summary>
/// A set of extension methods for the transformation provider to make it easier to
/// build many-to-many joining tables (takes care of adding the joining table and foreign
/// key constraints as necessary.
/// <remarks>This functionality was useful when bootstrapping a number of projects a few years ago, but
/// now that most changes are brown-field I'm thinking of removing these methods as it's easier to maintain
/// code that creates the tables etc. directly within migration.</remarks>
/// </summary>
public static class JoiningTableTransformationProviderExtensions
{
    public static ITransformationProvider AddManyToManyJoiningTable(this ITransformationProvider database, string schema, string lhsTableName, string lhsKey, string rhsTableName, string rhsKey)
    {
        var joiningTable = GetNameOfJoiningTable(lhsTableName, rhsTableName);

        return AddManyToManyJoiningTable(database, schema, lhsTableName, lhsKey, rhsTableName, rhsKey, joiningTable);
    }

    private static string GetNameOfJoiningTable(string lhsTableName, string rhsTableName)
    {
        return (Inflector.Singularize(lhsTableName) ?? lhsTableName) + (Inflector.Pluralize(rhsTableName) ?? rhsTableName);
    }

    public static ITransformationProvider AddManyToManyJoiningTable(this ITransformationProvider database, string schema, string lhsTableName, string lhsKey, string rhsTableName, string rhsKey, string joiningTableName)
    {
        var joiningTableWithSchema = TransformationProviderUtility.FormatTableName(schema, joiningTableName);

        var joinLhsKey = Inflector.Singularize(lhsTableName) + "Id";
        var joinRhsKey = Inflector.Singularize(rhsTableName) + "Id";

        database.AddTable(joiningTableWithSchema,
                                            new Column(joinLhsKey, DbType.Guid, ColumnProperty.NotNull),
                                            new Column(joinRhsKey, DbType.Guid, ColumnProperty.NotNull));

        var pkName = "PK_" + joiningTableName;

        pkName = ShortenKeyNameToBeSuitableForOracle(pkName);

        database.AddPrimaryKey(pkName, joiningTableWithSchema, joinLhsKey, joinRhsKey);

        var lhsTableNameWithSchema = TransformationProviderUtility.FormatTableName(schema, lhsTableName);
        var rhsTableNameWithSchema = TransformationProviderUtility.FormatTableName(schema, rhsTableName);

        var lhsFkName = TransformationProviderUtility.CreateForeignKeyName(lhsTableName, joiningTableName);
        database.AddForeignKey(lhsFkName, joiningTableWithSchema, joinLhsKey, lhsTableNameWithSchema, lhsKey, ForeignKeyConstraintType.NoAction);

        var rhsFkName = TransformationProviderUtility.CreateForeignKeyName(rhsTableName, joiningTableName);
        database.AddForeignKey(rhsFkName, joiningTableWithSchema, joinRhsKey, rhsTableNameWithSchema, rhsKey, ForeignKeyConstraintType.NoAction);

        return database;
    }

    private static string ShortenKeyNameToBeSuitableForOracle(string pkName)
    {
        return TransformationProviderUtility.AdjustNameToSize(pkName, TransformationProviderUtility.MaxLengthForForeignKeyInOracle, false);
    }

    public static ITransformationProvider RemoveManyToManyJoiningTable(this ITransformationProvider database, string schema, string lhsTableName, string rhsTableName)
    {
        var joiningTable = GetNameOfJoiningTable(lhsTableName, rhsTableName);
        return RemoveManyToManyJoiningTable(database, schema, lhsTableName, rhsTableName, joiningTable);
    }

    public static ITransformationProvider RemoveManyToManyJoiningTable(this ITransformationProvider database, string schema, string lhsTableName, string rhsTableName, string joiningTableName)
    {
        var joiningTableNameWithSchema = TransformationProviderUtility.FormatTableName(schema, joiningTableName);
        var lhsFkName = TransformationProviderUtility.CreateForeignKeyName(lhsTableName, joiningTableName);
        var rhsFkName = TransformationProviderUtility.CreateForeignKeyName(rhsTableName, joiningTableName);

        database.RemoveForeignKey(joiningTableNameWithSchema, lhsFkName);
        database.RemoveForeignKey(joiningTableNameWithSchema, rhsFkName);
        database.RemoveTable(joiningTableNameWithSchema);

        return database;
    }
}
