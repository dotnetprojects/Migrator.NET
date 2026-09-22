using System.Data;
using DotNetProjects.Migrator.Framework;

namespace Migrator.Tests.Providers.Base;

public abstract class TransformationProviderSimpleBase : TransformationProviderBase
{
    public void AddDefaultTable()
    {
        Provider.AddTable("TestTwo",
            new Column("Id",DbType.Int32){IsNullable = false},
            new Column("TestId", DbType.Int32)
,new PrimaryKeyConstraint("PK_" + "TestTwo", "Id")        );
    }

    public void AddTable()
    {
        Provider.AddTable("Test",
            new Column("Id",DbType.Int32){IsNullable = false},
            new Column("Title",DbType.String,100),
            new Column("name",DbType.String,50),
            new Column("blobVal",DbType.Binary),
            new Column("boolVal",DbType.Boolean),
            new Column("bigstring",DbType.String,50000)        );
    }

    public void AddTableWithPrimaryKey()
    {
        Provider.AddTable("Test",
            new Column("Id",DbType.Int32){IsNullable = false,IsIdentity = true},
            new Column("Title",DbType.String,100),
            new Column("name",DbType.String,50){IsNullable = false},
            new Column("blobVal", DbType.Binary),
            new Column("boolVal", DbType.Boolean),
            new Column("bigstring", DbType.String, 50000)
,new PrimaryKeyConstraint("PK_" + "Test", "Id")        );
    }

    public void AddPrimaryKey()
    {
        Provider.AddPrimaryKey("PK_Test", "Test", "Id");
    }
}
