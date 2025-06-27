using System.Data;

using Migrator.Framework;

namespace Migrator.Providers.Impl.DB2
{
    public class DB2Dialect : Dialect
    {
        public DB2Dialect()
        {
            this.RegisterColumnType(DbType.AnsiStringFixedLength, "CHAR(255)");
            this.RegisterColumnType(DbType.AnsiStringFixedLength, 255, "CHAR($l)");
            this.RegisterColumnType(DbType.AnsiStringFixedLength, 65535, "TEXT");
            this.RegisterColumnType(DbType.AnsiStringFixedLength, 16777215, "MEDIUMTEXT");
            this.RegisterColumnType(DbType.AnsiString, "VARCHAR(255)");
            this.RegisterColumnType(DbType.AnsiString, 255, "VARCHAR($l)");
            this.RegisterColumnType(DbType.AnsiString, 256, "VARCHAR(255)");
            this.RegisterColumnType(DbType.AnsiString, 65535, "TEXT");
            this.RegisterColumnType(DbType.AnsiString, 16777215, "MEDIUMTEXT");
            this.RegisterColumnType(DbType.Binary, "LONGBLOB");
            this.RegisterColumnType(DbType.Binary, 127, "TINYBLOB");
            this.RegisterColumnType(DbType.Binary, 65535, "BLOB");
            this.RegisterColumnType(DbType.Binary, 16777215, "MEDIUMBLOB");
            this.RegisterColumnType(DbType.Boolean, "TINYINT(1)");
            this.RegisterColumnType(DbType.Byte, "TINYINT UNSIGNED");
            this.RegisterColumnType(DbType.Currency, "MONEY");
            this.RegisterColumnType(DbType.Date, "DATE");
            this.RegisterColumnType(DbType.DateTime, "DATETIME");
            this.RegisterColumnType(DbType.DateTimeOffset, "DATETIME");
            this.RegisterColumnType(DbType.Decimal, "NUMERIC(19,5)");
            this.RegisterColumnType(DbType.Decimal, 19, "NUMERIC(19, $l)");
            this.RegisterColumnType(DbType.Double, "DOUBLE");
            this.RegisterColumnType(DbType.Guid, "VARCHAR(40)");
            this.RegisterColumnType(DbType.Int16, "SMALLINT");
            this.RegisterColumnType(DbType.Int32, "INTEGER");
            this.RegisterColumnType(DbType.Int64, "BIGINT");
            this.RegisterColumnType(DbType.Single, "FLOAT");
            this.RegisterColumnType(DbType.StringFixedLength, "CHAR(255)");
            this.RegisterColumnType(DbType.StringFixedLength, 255, "CHAR($l)");
            this.RegisterColumnType(DbType.StringFixedLength, 65535, "TEXT");
            this.RegisterColumnType(DbType.StringFixedLength, 16777215, "MEDIUMTEXT");
            this.RegisterColumnType(DbType.String, "VARCHAR(255)");
            this.RegisterColumnType(DbType.String, 255, "VARCHAR($l)");
            this.RegisterColumnType(DbType.String, 256, "VARCHAR(255)");
            this.RegisterColumnType(DbType.String, 65535, "TEXT");
            this.RegisterColumnType(DbType.String, 16777215, "MEDIUMTEXT");
            this.RegisterColumnType(DbType.String, 1073741823, "LONGTEXT");
            this.RegisterColumnType(DbType.Time, "TIME");

            this.RegisterProperty(ColumnProperty.Unsigned, "UNSIGNED");
            this.RegisterProperty(ColumnProperty.Identity, "AUTO_INCREMENT");

            this.RegisterUnsignedCompatible(DbType.Int16);
            this.RegisterUnsignedCompatible(DbType.Int32);
            this.RegisterUnsignedCompatible(DbType.Int64);
            this.RegisterUnsignedCompatible(DbType.Decimal);
            this.RegisterUnsignedCompatible(DbType.Double);
            this.RegisterUnsignedCompatible(DbType.Single);

            this.AddReservedWords("KEY");
        }

        public override ITransformationProvider GetTransformationProvider(Dialect dialect, string connectionString,
            string defaultSchema, string scope, string providerName)
        {
            return new DB2TransformationProvider(dialect, connectionString, scope, providerName);
        }

        public override ITransformationProvider GetTransformationProvider(Dialect dialect, IDbConnection connection,
            string defaultSchema,
            string scope, string providerName)
        {
            return new DB2TransformationProvider(dialect, connection, scope, providerName);
        }
    }
}
