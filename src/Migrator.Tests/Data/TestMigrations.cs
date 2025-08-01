using Migrator.Framework;

namespace Migrator.Tests.Data;

[Migration(1)]
public class FirstTestMigration : Migration
{
    public override void Up()
    {
    }

    public override void Down()
    {
    }
}

[Migration(2)]
public class SecondTestMigration : IMigration
{
    public string Name
    {
        get { return StringUtils.ToHumanName(GetType().Name); }
    }

    /// <summary>
    /// Defines tranformations to port the database to the current version.
    /// </summary>
    public void Up()
    {
    }

    /// <summary>
    /// This is run after the Up transaction has been committed
    /// </summary>
    public virtual void AfterUp()
    {
    }

    /// <summary>
    /// Defines transformations to revert things done in <c>Up</c>.
    /// </summary>
    public void Down()
    {
    }

    /// <summary>
    /// This is run after the Down transaction has been committed
    /// </summary>
    public virtual void AfterDown()
    {
    }

    /// <summary>
    /// Represents the database.
    /// <see cref="ITransformationProvider"></see>.
    /// </summary>
    /// <seealso cref="ITransformationProvider">Migration.Framework.ITransformationProvider</seealso>
    public ITransformationProvider Database { get; set; }

    /// <summary>
    /// This gets called once on the first migration object.
    /// </summary>
    public virtual void InitializeOnce(string[] args)
    {
    }
}