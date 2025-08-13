using System;
using System.Data;
using DotNetProjects.Migrator.Framework;
using NUnit.Framework;

namespace Migrator.Tests.Providers.Base;

public abstract class TransformationProviderSimpleBase : TransformationProviderBase
{
    [TearDown]
    public virtual void TearDown()
    {
        DropTestTables();

        Provider?.Rollback();
    }

    protected void DropTestTables()
    {
        // Because MySql doesn't support schema transaction
        // we got to remove the tables manually... sad...
        try
        {
            Provider.RemoveTable("TestTwo");
        }
        catch (Exception)
        {
        }
        try
        {
            Provider.RemoveTable("Test");
        }
        catch (Exception)
        {
        }
        try
        {
            Provider.RemoveTable("SchemaInfo");
        }
        catch (Exception)
        {
        }
    }

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
}
