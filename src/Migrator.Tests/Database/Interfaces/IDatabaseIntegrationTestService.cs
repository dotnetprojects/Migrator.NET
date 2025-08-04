using System.Threading;
using System.Threading.Tasks;
using Migrator.Tests.Database.Models;
using Migrator.Tests.Settings.Models;

namespace Migrator.Tests.Database.Interfaces;

public interface IDatabaseIntegrationTestService
{
    /// <summary>
    /// Creates a new test database. The database name contains a timestamp and some random alphanumeric chars to increase uniqueness of the name.
    /// It also removes old databases that could be leftovers from broken unit tests.
    /// </summary>
    /// <param name="databaseConnectionConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<DatabaseInfo> CreateTestDatabaseAsync(DatabaseConnectionConfig databaseConnectionConfig, CancellationToken cancellationToken);

    /// <summary>
    /// Drops a test database. The <see cref="DatabaseInfo"/> should hold the <see cref="DatabaseConnectionConfig"/> of the user with elevated privileges and the
    /// Oracle: Schema should hold the name of the user (in Oracle the schema is equal to user)
    /// </summary>
    /// <param name="databaseInfo"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DropDatabaseAsync(DatabaseInfo databaseInfo, CancellationToken cancellationToken);
}