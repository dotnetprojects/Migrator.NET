using DotNetProjects.Migrator.Framework;

namespace Migrator.Providers;

public class ForeignKeyConstraintMapper
{
    public string SqlForConstraint(ForeignKeyConstraintType constraint)
    {
        switch (constraint)
        {
            case ForeignKeyConstraintType.Cascade:
                return "CASCADE";
            case ForeignKeyConstraintType.Restrict:
                return "RESTRICT";
            case ForeignKeyConstraintType.SetDefault:
                return "SET DEFAULT";
            case ForeignKeyConstraintType.SetNull:
                return "SET NULL";
            default:
                return "NO ACTION";
        }
    }
}