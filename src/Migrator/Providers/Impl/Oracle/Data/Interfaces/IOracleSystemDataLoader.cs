using System.Collections.Generic;
using DotNetProjects.Migrator.Providers.Impl.Oracle.Models;
using DotNetProjects.Migrator.Providers.Models;

namespace DotNetProjects.Migrator.Providers.Impl.Oracle.Data.Interfaces;

public interface IOracleSystemDataLoader
{
    /// <summary>
    /// Gets <see cref="ForeignKeyConstraintItem"/>s for given table name.
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    List<ForeignKeyConstraintItem> GetForeignKeyConstraintItems(string tableName);

    /// <summary>
    /// Gets the USER_TAB_IDENTITY_COLS records for the given table name.
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    List<UserTabIdentityCols> GetUserTabIdentityCols(string tableName);

    /// <summary>
    /// Gets the primary key items from user_constraints and user_cons_columns
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    List<PrimaryKeyItem> GetPrimaryKeyItems(string tableName);
}