using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common;

namespace Migrator.Tests.Database.Data.Mappings.Oracle;

public class OracleVSessionConfiguration(FluentMappingBuilder fluentMappingBuilder)
    : EntityConfiguration<VSession>(fluentMappingBuilder)
{
    public override void ConfigureEntity()
    {
        _EntityMappingBuilder!.HasTableName("V$SESSION");

        _EntityMappingBuilder.Property(x => x.SerialHashTag)
            .HasColumnName("SERIAL#");

        _EntityMappingBuilder.Property(x => x.SID)
            .HasColumnName("SID");

        _EntityMappingBuilder.Property(x => x.UserName)
            .HasColumnName("USERNAME");
    }
}