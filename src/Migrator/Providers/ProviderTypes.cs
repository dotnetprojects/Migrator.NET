using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Migrator.Providers;

public enum ProviderTypes
{
    none,
    SqlServer2005,
    SqlServerCe,
    SqlServer,
    Mysql,
    MariaDB,
    SQLite,
    MonoSQLite,
    PostgreSQL82,
    PostgreSQL,
    Oracle,
    MsOracle,
    IBM_DB2,
    IBM_Informix,
    Firebird,
    Ingres,
    Sybase,
}
