using System.Collections.Generic;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common.Interfaces;
using Migrator.Tests.Database.Data.Mappings.Oracle;

namespace DotNetProjects.Migrator.Framework.Data.Common;

public class MappingSchemaFactory() : IMappingSchemaFactory
{
    public MappingSchema CreateOracleMappingSchema()
    {
        var fluentMappingBuilder = new FluentMappingBuilder();

        var configs = new List<IEntityConfiguration>
        {
            new OracleAllConsColumnsConfiguration(fluentMappingBuilder),
            new OracleAllConstraintsConfiguration(fluentMappingBuilder),
            new OracleAllTabColumnsConfiguration(fluentMappingBuilder),
            new OracleAllTabColumnsConfiguration(fluentMappingBuilder),
            new OracleAllUsersConfiguration(fluentMappingBuilder),
            new OracleDBADataFilesConfiguration(fluentMappingBuilder),
            new OracleVSessionConfiguration(fluentMappingBuilder),
        };

        return Configure(fluentMappingBuilder, configs);
    }

    private static MappingSchema Configure(FluentMappingBuilder fluentMappingBuilder, IEnumerable<IEntityConfiguration> entityConfigurations)
    {
        foreach (var config in entityConfigurations)
        {
            config.ConfigureEntity();
        }

        fluentMappingBuilder.Build();

        return fluentMappingBuilder.MappingSchema;
    }
}
