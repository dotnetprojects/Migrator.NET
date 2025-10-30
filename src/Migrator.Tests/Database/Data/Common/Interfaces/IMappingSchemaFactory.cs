using LinqToDB.Mapping;

namespace Migrator.Tests.Database.Data.Common.Interfaces;

public interface IMappingSchemaFactory
{
    MappingSchema CreateOracleMappingSchema();
}