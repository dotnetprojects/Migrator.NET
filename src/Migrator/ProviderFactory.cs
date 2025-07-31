#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Migrator.Framework;
using Migrator.Providers;
using Migrator.Providers.Impl.DB2;
using Migrator.Providers.Impl.Firebird;
using Migrator.Providers.Impl.Informix;
using Migrator.Providers.Impl.Ingres;
using Migrator.Providers.Impl.Sybase;
using Migrator.Providers.Mysql;
using Migrator.Providers.Oracle;
using Migrator.Providers.PostgreSQL;
using Migrator.Providers.SQLite;
using Migrator.Providers.SqlServer;

namespace Migrator
{
    /// <summary>
    /// Handles loading Provider implementations
    /// </summary>
    public class ProviderFactory
    {
        static ProviderFactory()
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="providerType"></param>
        /// <param name="connectionString"></param>
        /// <param name="defaultSchema"></param>
        /// <param name="scope"></param>
        /// <param name="providerName">for Example: System.Data.SqlClient</param>
        /// <returns></returns>
        public static ITransformationProvider Create(ProviderTypes providerType, string connectionString, string defaultSchema, string scope = "default", string providerName = "")
        {
            var dialectInstance = DialectForProvider(providerType);

            return dialectInstance.NewProviderForDialect(connectionString, defaultSchema, scope, providerName);
        }

        public static ITransformationProvider Create(ProviderTypes providerType, IDbConnection connection, string defaultSchema, string scope = "default", string providerName = "")
        {
            var dialectInstance = DialectForProvider(providerType);

            return dialectInstance.NewProviderForDialect(connection, defaultSchema, scope, providerName);
        }

        public static Dialect DialectForProvider(ProviderTypes providerType)
        {
            switch (providerType)
            {
                case ProviderTypes.SQLite:
                    return (Dialect)Activator.CreateInstance(typeof(SQLiteDialect));
                case ProviderTypes.MonoSQLite:
                    return (Dialect)Activator.CreateInstance(typeof(SQLiteMonoDialect));
                case ProviderTypes.Mysql:
                    return (Dialect)Activator.CreateInstance(typeof(MysqlDialect));
                case ProviderTypes.MariaDB:
                    return (Dialect)Activator.CreateInstance(typeof(MariaDBDialect));
                case ProviderTypes.Oracle:
                    return (Dialect)Activator.CreateInstance(typeof(OracleDialect));
                case ProviderTypes.PostgreSQL:
                    return (Dialect)Activator.CreateInstance(typeof(PostgreSQLDialect));
                case ProviderTypes.PostgreSQL82:
                    return (Dialect)Activator.CreateInstance(typeof(PostgreSQL82Dialect));
                case ProviderTypes.SqlServer:
                    return (Dialect)Activator.CreateInstance(typeof(SqlServerDialect));
                case ProviderTypes.SqlServer2005:
                    return (Dialect)Activator.CreateInstance(typeof(SqlServer2005Dialect));
                case ProviderTypes.SqlServerCe:
                    return (Dialect)Activator.CreateInstance(typeof(SqlServerCeDialect));
                case ProviderTypes.MsOracle:
                    return (Dialect)Activator.CreateInstance(typeof(MsOracleDialect));
                case ProviderTypes.IBM_DB2:
                    return (Dialect)Activator.CreateInstance(typeof(DB2Dialect));
                case ProviderTypes.IBM_Informix:
                    return (Dialect)Activator.CreateInstance(typeof(InformixDialect));
                case ProviderTypes.Firebird:
                    return (Dialect)Activator.CreateInstance(typeof(FirebirdDialect));
                case ProviderTypes.Ingres:
                    return (Dialect)Activator.CreateInstance(typeof(IngresDialect));
                case ProviderTypes.Sybase:
                    return (Dialect)Activator.CreateInstance(typeof(SybaseDialect));
            }

            return null;
        }
    }
}