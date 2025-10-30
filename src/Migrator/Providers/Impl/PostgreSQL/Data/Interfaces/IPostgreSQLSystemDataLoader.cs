using System.Collections.Generic;
using DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Models;

namespace DotNetProjects.Migrator.Providers.Impl.PostgreSQL.Data.Interfaces;

public interface IPostgreSQLSystemDataLoader
{
    /// <summary>
    /// Gets column infos.
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="schemaName"></param>
    /// <returns></returns>
    List<ColumnInfo> GetColumnInfos(string tableName, string schemaName = "public");

    /// <summary>
    /// Gets table constraints.
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="schemaName"></param>
    /// <returns></returns>
    List<TableConstraint> GetTableConstraints(string tableName, string schemaName = "public");
}