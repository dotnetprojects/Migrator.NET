using System.Data;

namespace DotNetProjects.Migrator.Framework.SchemaBuilder;

public interface IColumnOptions
{
    SchemaBuilder OfType(DbType dbType);

    SchemaBuilder WithSize(int size);

    IForeignKeyOptions AsForeignKey();
}