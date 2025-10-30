using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common;

namespace Migrator.Tests.Database.Data.Mappings.Oracle;

public class OracleDBADataFilesConfiguration(FluentMappingBuilder fluentMappingBuilder)
    : EntityConfiguration<DBADataFiles>(fluentMappingBuilder)
{
    public override void ConfigureEntity()
    {
        _EntityMappingBuilder!.HasTableName("DBA_DATA_FILES");

        _EntityMappingBuilder.Property(x => x.FileName)
            .HasColumnName("FILE_NAME");

        _EntityMappingBuilder.Property(x => x.TablespaceName)
            .HasColumnName("TABLESPACE_NAME");
    }
}
