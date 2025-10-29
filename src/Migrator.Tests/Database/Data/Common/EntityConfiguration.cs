using LinqToDB.Mapping;
using Migrator.Tests.Database.Data.Common.Interfaces;

namespace Migrator.Tests.Database.Data.Common;

public abstract class EntityConfiguration<T>(FluentMappingBuilder fluentMappingBuilder) : IEntityConfiguration where T : class
{
    protected EntityMappingBuilder<T> _EntityMappingBuilder = fluentMappingBuilder.Entity<T>();
    protected FluentMappingBuilder _FluentMappingBuilder = fluentMappingBuilder;

    /// <summary>
    /// Configures the entity in the fluent migrator of Linq2db
    /// </summary>
    public abstract void ConfigureEntity();
}