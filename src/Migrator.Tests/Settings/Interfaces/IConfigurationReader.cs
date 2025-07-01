using Migrator.Tests.Settings.Models;

namespace Migrator.Tests.Settings.Interfaces;

public interface IConfigurationReader
{
    DatabaseConnectionConfig GetDatabaseConnectionConfigById(string id);
}