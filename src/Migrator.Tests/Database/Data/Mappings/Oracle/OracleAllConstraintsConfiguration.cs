using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common;

namespace Migrator.Tests.Database.Data.Mappings.Oracle;

public class OracleAllConstraintsConfiguration(FluentMappingBuilder fluentMappingBuilder)
    : EntityConfiguration<AllConstraints>(fluentMappingBuilder)
{
    public override void ConfigureEntity()
    {
        _EntityMappingBuilder!.HasTableName("ALL_CONSTRAINTS");

        _EntityMappingBuilder.Property(x => x.ConstraintName)
            .HasColumnName("CONSTRAINT_NAME");

        _EntityMappingBuilder.Property(x => x.RConstraintName)
            .HasColumnName("R_CONSTRAINT_NAME");

        _EntityMappingBuilder.Property(x => x.ROwner)
            .HasColumnName("R_OWNER");

        _EntityMappingBuilder.Property(x => x.ConstraintType)
            .HasColumnName("CONSTRAINT_TYPE");

        _EntityMappingBuilder.Property(x => x.Owner)
            .HasColumnName("OWNER");

        _EntityMappingBuilder.Property(x => x.Status)
            .HasColumnName("STATUS");

        _EntityMappingBuilder.Property(x => x.TableName)
            .HasColumnName("TABLE_NAME");
    }
}
