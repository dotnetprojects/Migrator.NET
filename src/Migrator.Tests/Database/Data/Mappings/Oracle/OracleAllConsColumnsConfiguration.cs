using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common;

namespace Migrator.Tests.Database.Data.Mappings.Oracle;

public class OracleAllConsColumnsConfiguration(FluentMappingBuilder fluentMappingBuilder)
    : EntityConfiguration<AllConsColumns>(fluentMappingBuilder)
{
    public override void ConfigureEntity()
    {
        _EntityMappingBuilder!.HasTableName("ALL_CONS_COLUMNS");

        _EntityMappingBuilder.Property(x => x.ColumnName)
            .HasColumnName("COLUMN_NAME");

        _EntityMappingBuilder.Property(x => x.ConstraintName)
            .HasColumnName("CONSTRAINT_NAME");

        _EntityMappingBuilder.Property(x => x.Owner)
            .HasColumnName("OWNER");

        _EntityMappingBuilder.Property(x => x.Position)
            .HasColumnName("POSITION");

        _EntityMappingBuilder.Property(x => x.TableName)
            .HasColumnName("TABLE_NAME");
    }
}