using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Migrator.Tests.Settings.Interfaces;
using Migrator.Tests.Settings.Models;

namespace Migrator.Tests.Settings;

/// <summary>
/// Reads the configuration from appsettings.
/// </summary>
public class ConfigurationReader() : IConfigurationReader
{
    private const string AspnetCoreVariableString = "ASPNETCORE_ENVIRONMENT";

    /// <summary>
    /// Gets the database connection config by its ID.
    /// </summary>
    /// <param name="id">Use one of the IDs in <see cref="ConnectionIdentifiers"/></param>
    /// <returns></returns>
    public DatabaseConnectionConfig GetDatabaseConnectionConfigById(string id)
    {
        var configurationRoot = GetConfigurationRoot();
        var aspNetCoreVariable = GetAspNetCoreEnvironmentVariable();

        var databaseConnectionConfigs = configurationRoot.GetSection("DatabaseConnectionConfigs")
            .Get<List<DatabaseConnectionConfig>>() ?? throw new KeyNotFoundException();

        return databaseConnectionConfigs.Single(x => x.Id == id);
    }

    /// <summary>
    /// Gets the configuration root. Currently it is not used for production therefore we do not use appsettings.json.
    /// Your personal appsettings.Development.json will be used if your ASPNETCORE_ENVIRONMENT env variable is set to "Development".
    /// </summary>
    /// <returns></returns>
    public IConfigurationRoot GetConfigurationRoot()
    {
        var aspNetCoreVariableName = GetAspNetCoreEnvironmentVariable();

        return new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{aspNetCoreVariableName}.json", optional: true, reloadOnChange: false)
            .Build();
    }

    private static string GetAspNetCoreEnvironmentVariable()
    {
        var aspNetCoreVariable = Environment.GetEnvironmentVariable(AspnetCoreVariableString, EnvironmentVariableTarget.Process);

        if (string.IsNullOrEmpty(aspNetCoreVariable))
        {
            aspNetCoreVariable = Environment.GetEnvironmentVariable(AspnetCoreVariableString, EnvironmentVariableTarget.User);
        }
        else if (string.IsNullOrEmpty(aspNetCoreVariable))
        {
            aspNetCoreVariable = Environment.GetEnvironmentVariable(AspnetCoreVariableString, EnvironmentVariableTarget.Machine);
        }

        if (string.IsNullOrWhiteSpace(aspNetCoreVariable))
        {
            throw new Exception($"The environment variable '{AspnetCoreVariableString}' is not set.");
        }

        return aspNetCoreVariable;
    }
}