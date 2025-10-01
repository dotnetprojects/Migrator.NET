using System.Data;
using DotNetProjects.Migrator.Framework;

namespace Migrator.Tests.Providers.Base;

public abstract class TransformationProviderSimpleBase : TransformationProviderBase
{
    public void AddDefaultTable()
    {
        Provider.AddTable("TestTwo",
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKey),
            new Column("TestId", DbType.Int32)
        );
    }

    public void AddTable()
    {
        Provider.AddTable("Test",
            new Column("Id", DbType.Int32, ColumnProperty.NotNull),
            new Column("Title", DbType.String, 100, ColumnProperty.Null),
            new Column("name", DbType.String, 50, ColumnProperty.Null),
            new Column("blobVal", DbType.Binary, ColumnProperty.Null),
            new Column("boolVal", DbType.Boolean, ColumnProperty.Null),
            new Column("bigstring", DbType.String, 50000, ColumnProperty.Null)
        );
    }

    public void AddTableWithPrimaryKey()
    {
        Provider.AddTable("Test",
            new Column("Id", DbType.Int32, ColumnProperty.PrimaryKeyWithIdentity),
            new Column("Title", DbType.String, 100, ColumnProperty.Null),
            new Column("name", DbType.String, 50, ColumnProperty.NotNull),
            new Column("blobVal", DbType.Binary),
            new Column("boolVal", DbType.Boolean),
            new Column("bigstring", DbType.String, 50000)
        );
    }

    public void AddPrimaryKey()
    {
        Provider.AddPrimaryKey("PK_Test", "Test", "Id");
    }
}
