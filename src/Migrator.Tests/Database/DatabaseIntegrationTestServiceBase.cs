using System;
using System.Threading;
using System.Threading.Tasks;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Database.Models;
using Migrator.Tests.Settings.Models;

namespace Migrator.Tests.Database;

public abstract class DatabaseIntegrationTestServiceBase(IDatabaseNameService databaseNameService) : IDatabaseIntegrationTestService
{
    /// <summary>
    /// Deletes all integration test databases older than the given time span.
    /// </summary>
    // TODO CK time span!
    protected readonly TimeSpan _MinTimeSpanBeforeDatabaseDeletion = TimeSpan.FromMinutes(1); // TimeSpan.FromMinutes(60);

    protected IDatabaseNameService DatabaseNameService { get; private set; } = databaseNameService;

    abstract public Task<DatabaseInfo> CreateTestDatabaseAsync(DatabaseConnectionConfig databaseConnectionConfig, CancellationToken cancellationToken);

    abstract public Task DropDatabaseAsync(DatabaseInfo databaseInfo, CancellationToken cancellationToken);

    protected DateTime ReadTimeStampFromDatabaseName(string name)
    {
        var creationDate = DatabaseNameService.ReadTimeStampFromString(name);

        if (!creationDate.HasValue)
        {
            throw new Exception("You tried to drop a database that was not created by this service. For safety reasons we deny your request.");
        }

        return creationDate.Value;
    }
}