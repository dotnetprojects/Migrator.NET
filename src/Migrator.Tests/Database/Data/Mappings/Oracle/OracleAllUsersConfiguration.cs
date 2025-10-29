using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common;

namespace Migrator.Tests.Database.Data.Mappings.Oracle;

public class OracleAllUsersConfiguration(FluentMappingBuilder fluentMappingBuilder)
    : EntityConfiguration<AllUsers>(fluentMappingBuilder)
{
    public override void ConfigureEntity()
    {
        _EntityMappingBuilder!.HasTableName("ALL_USERS");

        _EntityMappingBuilder.Property(x => x.UserName)
            .HasColumnName("USERNAME");
    }
}
