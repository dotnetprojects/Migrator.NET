namespace Migrator.Tests.Database.Parsers.Interfaces;

public interface ILinq2DBNameToDatabaseServerTypeParser
{
    /// <summary>
    /// Parses the Linq2Db provider name to <see cref="DatabaseProviderType"/>.
    /// </summary>
    /// <param name="linq2DbName"></param>
    /// <returns></returns>
    DatabaseProviderType Parse(string linq2DbName);
}