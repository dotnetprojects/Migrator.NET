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
           new Column(columnName1Target,DbType.Int32,null)        );

        Provider.ChangeColumn(tableNameSource, new Column(columnName1Target,DbType.Int32){IsNullable = false});
    }

    [Test]
    public void DefaultValue_ConvertStringToNotNull_DoesNotThrow()
    {
        const string tableNameSource = "SourceTable";
        const string columnName1Target = "TargetColumn1";

        Provider.AddTable(tableNameSource,
            new Column(columnName1Target,DbType.String,32){IsNullable = false}        );

        Provider.ChangeColumn(tableNameSource, new Column(columnName1Target,DbType.String));
    }

    [Test]
    public void RemoveColumnDefaultValue_DoesNotThrow()
    {
        const string tableNameSource = "TableName";
        const string columnName1 = "ColumnName1";

        Provider.AddTable(tableNameSource,
            new Column(columnName1, DbType.Int32) { DefaultValue = 10, IsNullable = false}        );

        Provider.RemoveColumnDefaultValue(tableNameSource, columnName1);

        Provider.ChangeColumn(tableNameSource, new Column(columnName1,DbType.Int32));
    }
}