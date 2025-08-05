using System;

namespace Migrator.Tests.Database.GuidServices.Interfaces;

public interface IGuidService
{
    /// <summary>
    /// Creates a new database friendly Guid depending on the given database type.
    /// </summary>
    /// <param name="databaseType"></param>
    /// <returns></returns>
    Guid NewGuid();
}