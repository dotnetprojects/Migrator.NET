using System.Data;
using DotNetProjects.Migrator.Framework;
using Migrator.Tests.Providers.Base;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Generic;

public abstract class Generic_DefaultValueTestsBase : TransformationProviderBase
{
    [Test]
    public void DefaultValue_Null_Success()
    {
        const string tableNameSource = "SourceTable";
        const string columnName1Target = "TargetColumn1";

        Provider.AddTable(tableNameSource,
           new Column(columnName1Target, DbType.Int32, ColumnProperty.Null, null)
        );

        Provider.ChangeColumn(tableNameSource, new Column(columnName1Target, DbType.Int32, ColumnProperty.NotNull));
    }

    [Test]
    public void DefaultValue_ConvertStringToNotNull_DoesNotThrow()
    {
        const string tableNameSource = "SourceTable";
        const string columnName1Target = "TargetColumn1";

        Provider.AddTable(tableNameSource,
            new Column(columnName1Target, DbType.String, 32, ColumnProperty.NotNull)
        );

        Provider.ChangeColumn(tableNameSource, new Column(columnName1Target, DbType.String, ColumnProperty.Null));
    }
}