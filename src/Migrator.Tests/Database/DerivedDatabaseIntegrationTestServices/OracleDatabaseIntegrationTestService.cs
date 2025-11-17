using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetProjects.Migrator.Framework.Data.Common;
using DotNetProjects.Migrator.Framework.Data.Models.Oracle;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Mapster;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Database.Models;
using Migrator.Tests.Settings.Models;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Tests.Database.DerivedDatabaseIntegrationTestServices;


/// <summary>
/// We use the tablespace users since the server container is recreated before the test runs (once per github workflow run)
/// </summary>
/// <param name="timeProvider"></param>
/// <param name="databaseNameService"></param>
public class OracleDatabaseIntegrationTestService(
    TimeProvider timeProvider,
    IDatabaseNameService databaseNameService)
        : DatabaseIntegrationTestServiceBase(databaseNameService), IDatabaseIntegrationTestService
{
    private const string UserStringKey = "User Id";
    private const string PasswordStringKey = "Password";
    private const string ReplaceString = "RandomStringThatIsNotQuotedByTheBuilderDoNotChange";
    private readonly MappingSchema _mappingSchema = new MappingSchemaFactory().CreateOracleMappingSchema();

    /// <summary>
    /// Creates an oracle database for test purposes.
    /// </summary>
    /// <remarks>
    /// For the creation of the Oracle user used in this method follow these steps: 
    ///
    /// Use a SYSDBA user, connect or switch to the default PDB. 
    /// On the free docker container the name of the default PDB is "FREEPDB1" use it as the service name or alternatively switch containers. For installations other than the "FREE" Oracle
    /// Docker image find out the (default) PDB and switch to it then create grant privileges listed below. Having all set you can create a connection string using the newly created user 
    /// and password and add it to appsettings.Development (for dev environment)
    /// <list type="bullet"> 
    /// <item><description>ALTER SESSION SET CONTAINER = FREEPDB1</description></item>
    /// <item><description>CREATE USER myuser IDENTIFIED BY mypassword</description></item>
    /// <item><description>GRANT CREATE USER TO myuser</description></item>
    /// <item><description>GRANT DROP USER TO myuser</description></item>
    /// <item><description>GRANT CREATE SESSION TO myuser WITH ADMIN OPTION</description></item>
    /// <item><description>GRANT RESOURCE TO myuser WITH ADMIN OPTION</description></item>
    /// <item><description>GRANT CONNECT TO myuser WITH ADMIN OPTION</description></item>
    /// <item><description>GRANT UNLIMITED TABLESPACE TO myuser with ADMIN OPTION</description></item>
    /// <item><description>GRANT SELECT ON V_$SESSION TO myuser with GRANT OPTION</description></item>
    /// <item><description>GRANT ALTER SYSTEM TO myuser</description></item>
    /// </list>
    /// Having all set you can create a connection string using the newly created user and password and add it into appsettings.development
    /// </remarks>
    /// <param name="databaseConnectionConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override async Task<DatabaseInfo> CreateTestDatabaseAsync(DatabaseConnectionConfig databaseConnectionConfig, CancellationToken cancellationToken)
    {
        var tempDatabaseConnectionConfig = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>();

        var connectionStringBuilder = new OracleConnectionStringBuilder()
        {
            ConnectionString = tempDatabaseConnectionConfig.ConnectionString
        };

        if (!connectionStringBuilder.TryGetValue(UserStringKey, out var user))
        {
            throw new Exception($"Cannot find key '{UserStringKey}'");
        }

        if (!connectionStringBuilder.TryGetValue(PasswordStringKey, out var password))
        {
            throw new Exception($"Cannot find key '{PasswordStringKey}'");
        }

        var tempUserName = DatabaseNameService.CreateDatabaseName();

        var dataOptions = new DataOptions().UseOracle(databaseConnectionConfig.ConnectionString)
            .UseMappingSchema(_mappingSchema);

        using var context = new DataConnection(dataOptions);

        var userNames = await context.GetTable<AllUsers>().Select(x => x.UserName).ToListAsync(cancellationToken);

        var toBeDeletedUsers = userNames.Where(x =>
        {
            var creationDate = DatabaseNameService.ReadTimeStampFromString(x);

            return creationDate.HasValue && creationDate.Value < timeProvider.GetUtcNow().Subtract(_MinTimeSpanBeforeDatabaseDeletion);
        }).ToList();

        await Parallel.ForEachAsync(
            toBeDeletedUsers,
            new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
            async (x, cancellationTokenInner) =>
            {
                var databaseInfoToBeDeleted = new DatabaseInfo
                {
                    DatabaseConnectionConfig = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>(),
                    DatabaseConnectionConfigMaster = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>(),
                    SchemaName = x
                };

                await DropDatabaseAsync(databaseInfoToBeDeleted, cancellationTokenInner);
            });

        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"CREATE USER \"{tempUserName}\" IDENTIFIED BY \"{tempUserName}\"");
        stringBuilder.AppendLine($"DEFAULT TABLESPACE users");
        stringBuilder.AppendLine($"TEMPORARY TABLESPACE TEMP");
        stringBuilder.AppendLine($"QUOTA UNLIMITED ON users");

        await context.ExecuteAsync(stringBuilder.ToString(), cancellationToken);

        var privileges = new[]
        {
            "CONNECT",
            "CREATE SESSION",
            "RESOURCE",
            "UNLIMITED TABLESPACE"
        };

        await context.ExecuteAsync($"GRANT {string.Join(", ", privileges)} TO \"{tempUserName}\"", cancellationToken);
        await context.ExecuteAsync($"GRANT SELECT ON SYS.GV_$SESSION TO \"{tempUserName}\"", cancellationToken);

        connectionStringBuilder.Add(UserStringKey, ReplaceString);
        connectionStringBuilder.Add(PasswordStringKey, ReplaceString);

        tempDatabaseConnectionConfig.ConnectionString = connectionStringBuilder.ConnectionString;
        tempDatabaseConnectionConfig.ConnectionString = tempDatabaseConnectionConfig.ConnectionString.Replace(ReplaceString, $"\"{tempUserName}\"");
        tempDatabaseConnectionConfig.Schema = tempUserName;

        var databaseInfo = new DatabaseInfo
        {
            DatabaseConnectionConfigMaster = databaseConnectionConfig.Adapt<DatabaseConnectionConfig>(),
            DatabaseConnectionConfig = tempDatabaseConnectionConfig,
            SchemaName = tempUserName,
        };

        return databaseInfo;
    }

    public override async Task DropDatabaseAsync(DatabaseInfo databaseInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(databaseInfo);

        var creationDate = ReadTimeStampFromDatabaseName(databaseInfo.SchemaName);

        var dataOptions = new DataOptions().UseOracle(databaseInfo.DatabaseConnectionConfigMaster.ConnectionString)
            .UseMappingSchema(_mappingSchema);

        using var context = new DataConnection(dataOptions);

        var maxAttempts = 4;
        var delayBetweenAttempts = TimeSpan.FromSeconds(1);

        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var vSessions = await context.GetTable<VSession>()
                  .Where(x => x.UserName == databaseInfo.SchemaName)
                  .ToListAsync(cancellationToken);

                foreach (var session in vSessions)
                {
                    var killStatement = $"ALTER SYSTEM KILL SESSION '{session.SID},{session.SerialHashTag}' IMMEDIATE";
                    await context.ExecuteAsync(killStatement, cancellationToken);
                }

                var userExists = context.GetTable<AllUsers>().Any(x => x.UserName == databaseInfo.SchemaName);

                if (!userExists)
                {
                    break;
                }

                await context.ExecuteAsync($"DROP USER \"{databaseInfo.SchemaName}\" CASCADE", cancellationToken);
            }
            catch
            {
                if (i + 1 == maxAttempts)
                {
                    throw;
                }

                var userExists = await context.GetTable<AllUsers>().AnyAsync(x => x.UserName == databaseInfo.SchemaName, token: cancellationToken);

                if (!userExists)
                {
                    break;
                }

                await Task.Delay(delayBetweenAttempts, cancellationToken);

                delayBetweenAttempts = delayBetweenAttempts.Add(TimeSpan.FromSeconds(1));
            }
        }

        await context.ExecuteAsync($"PURGE RECYCLEBIN", cancellationToken);
    }
}
