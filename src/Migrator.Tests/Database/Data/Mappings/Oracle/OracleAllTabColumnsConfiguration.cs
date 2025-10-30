using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common;

namespace Migrator.Tests.Database.Data.Mappings.Oracle;

public class OracleAllTabColumnsConfiguration(FluentMappingBuilder fluentMappingBuilder)
    : EntityConfiguration<AllTabColumns>(fluentMappingBuilder)
{
    public override void ConfigureEntity()
    {
        _EntityMappingBuilder!.HasTableName("ALL_TAB_COLUMNS");

        _EntityMappingBuilder.Property(x => x.ColumnName)
            .HasColumnName("COLUMN_NAME");

        _EntityMappingBuilder.Property(x => x.DataDefault)
            .HasColumnName("DATA_DEFAULT");

        _EntityMappingBuilder.Property(x => x.DataLength)
            .HasColumnName("DATA_LENGTH");

        _EntityMappingBuilder.Property(x => x.DataType)
            .HasColumnName("DATA_TYPE");

        _EntityMappingBuilder.Property(x => x.IdentityColumn)
            .HasColumnName("IDENTITY_COLUMN");

        _EntityMappingBuilder.Property(x => x.Nullable)
            .HasColumnName("NULLABLE");

        _EntityMappingBuilder.Property(x => x.Owner)
            .HasColumnName("OWNER");

        _EntityMappingBuilder.Property(x => x.TableName)
            .HasColumnName("TABLE_NAME");
    }
}