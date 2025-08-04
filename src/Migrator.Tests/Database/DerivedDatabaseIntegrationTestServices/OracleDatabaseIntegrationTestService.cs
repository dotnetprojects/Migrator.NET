using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using Mapster;
using Migrator.Tests.Database.DatabaseName.Interfaces;
using Migrator.Tests.Database.Interfaces;
using Migrator.Tests.Database.Models;
using Migrator.Tests.Settings.Models;
using Oracle.ManagedDataAccess.Client;

namespace Migrator.Tests.Database.DerivedDatabaseIntegrationTestServices;

public class OracleDatabaseIntegrationTestService(
    TimeProvider timeProvider,
    IDatabaseNameService databaseNameService
    // IImportExportMappingSchemaFactory importExportMappingSchemaFactory
    )
        : DatabaseIntegrationTestServiceBase(databaseNameService), IDatabaseIntegrationTestService
{
    private const string UserStringKey = "User Id";
    private const string PasswordStringKey = "Password";
    private const string ReplaceString = "RandomStringThatIsNotQuotedByTheBuilderDoNotChange";
    // private readonly IImportExportMappingSchemaFactory _importExportMappingSchemaFactory = importExportMappingSchemaFactory;

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
        DataConnection context;

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

        List<string> userNames;

        var dataOptions = new DataOptions().UseOracle(databaseConnectionConfig.ConnectionString);

        using (context = new DataConnection(dataOptions))
        {
            userNames = await context.QueryToListAsync<string>("SELECT username FROM all_users", cancellationToken);
        }

        var toBeDeletedUsers = userNames.Where(x =>
        {
            var creationDate = DatabaseNameService.ReadTimeStampFromString(x);

            return creationDate.HasValue && creationDate.Value < timeProvider.GetUtcNow().Subtract(MinTimeSpanBeforeDatabaseDeletion);
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

        using (context = new DataConnection(dataOptions))
        {
            await context.ExecuteAsync($"CREATE USER \"{tempUserName}\" IDENTIFIED BY \"{tempUserName}\"", cancellationToken);

            var privileges = new[]
            {
                "CONNECT",
                "CREATE SESSION",
                "RESOURCE",
                "UNLIMITED TABLESPACE"
            };

            await context.ExecuteAsync($"GRANT {string.Join(", ", privileges)} TO \"{tempUserName}\"", cancellationToken);
            await context.ExecuteAsync($"GRANT SELECT ON SYS.V_$SESSION TO \"{tempUserName}\"", cancellationToken);
        }

        connectionStringBuilder.Add(UserStringKey, ReplaceString);
        connectionStringBuilder.Add(PasswordStringKey, ReplaceString);

        tempDatabaseConnectionConfig.ConnectionString = connectionStringBuilder.ConnectionString;
        tempDatabaseConnectionConfig.ConnectionString = tempDatabaseConnectionConfig.ConnectionString.Replace(ReplaceString, $"\"{tempUserName}\"");

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
        var creationDate = ReadTimeStampFromDatabaseName(databaseInfo.SchemaName);

        var dataOptions = new DataOptions().UseOracle(databaseInfo.DatabaseConnectionConfigMaster.ConnectionString);
        // .UseMappingSchema(_importExportMappingSchemaFactory.CreateOracleMappingSchema());

        using var context = new DataConnection(dataOptions);

        // var vSessions = await context.GetTable<VSession>()
        //     .Where(x => x.UserName == databaseInfo.SchemaName)
        //     .ToListAsync(cancellationToken);

        // await Parallel.ForEachAsync(
        //     vSessions,
        //     new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
        //     async (x, cancellationTokenInner) =>
        //     {
        //         using var killSessionContext = new DataConnection(dataOptions);

        //         var killStatement = $"ALTER SYSTEM KILL SESSION '{x.SID},{x.SerialHashTag}' IMMEDIATE";
        //         try
        //         {
        //             await killSessionContext.ExecuteAsync(killStatement, cancellationToken);

        //             // Oracle does not close the session immediately as they pretend so we need to wait a while
        //             // Since this happens only in very rare cases we accept waiting for a while. 
        //             // If nobody connects to the database this will never happen.
        //             await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        //         }
        //         catch
        //         {
        //             // Most probably killed by another parallel running integration test. If not, the DROP USER exception will show the details.
        //         }
        //     });

        try
        {
            await context.ExecuteAsync($"DROP USER \"{databaseInfo.SchemaName}\" CASCADE", cancellationToken);
        }
        catch
        {
            await Task.Delay(2000, cancellationToken);

            // In next Linq2db version this can be replaced by ...FromSql().First();
            // https://github.com/linq2db/linq2db/issues/2779
            // TODO CK create issue in Redmine and refer to it here
            var countList = await context.QueryToListAsync<int>($"SELECT COUNT(*) FROM all_users WHERE username = '{databaseInfo.SchemaName}'", cancellationToken);
            var count = countList.First();

            if (count == 1)
            {
                throw;
            }
            else
            {
                // The user was removed by another asynchronously running test that kicked in earlier.
                // That's ok for us as we have achieved the goal.
            }
        }
    }
}