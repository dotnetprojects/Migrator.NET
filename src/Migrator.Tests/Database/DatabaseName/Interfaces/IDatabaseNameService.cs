using System;

namespace Migrator.Tests.Database.DatabaseName.Interfaces;

/// <summary>
/// Used for integration tests. During integration tests we need to create unique database names for parallel testing. 
/// </summary>
public interface IDatabaseNameService
{
    /// <summary>
    /// Reads the date time from the date part of the database or user name (in Oracle we use the user name/schema name).
    /// </summary>
    /// <param name="databaseName"></param>
    /// <returns></returns>
    DateTime? ReadTimeStampFromString(string name);

    /// <summary>
    /// Creates a database name
    /// </summary>
    /// <returns></returns>
    string CreateDatabaseName();
}