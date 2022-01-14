using System.Configuration;

namespace Migrator.TestsNeu
{
	internal class Helper
	{

		public static ConnectionStringSettings GetConnectionStringByName(string name)
		{
			ConnectionStringSettingsCollection connections = ConfigurationManager.ConnectionStrings;
			var cca = ConfigurationManager.ConnectionStrings[2];
			string connectionString = cca.ConnectionString;

			if (connections.Count != 0)
			{
				foreach (ConnectionStringSettings connection in connections)
				{
					if (connection.Name == name)
						return connection;
				}
			}
			return null;
		}
		public static  ConnectionStringSettings GetConnectionStringSQLServer { get { return GetConnectionStringByName("SqlServerConnectionString"); } }
		public static ConnectionStringSettings GetConnectionStringSQLServerOrigin { get { return GetConnectionStringByName("SQLServerConnectionString"); } }
		public static ConnectionStringSettings GetConnectionStringPostgreSQL{ get { return GetConnectionStringByName("PostgresConnectionString"); } }
		public static ConnectionStringSettings GetConnectionStringSQLite { get { return GetConnectionStringByName("SqliteServerConnectionString"); } }
	}
}
